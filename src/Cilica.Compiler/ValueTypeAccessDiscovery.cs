using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<(string Image, int Token), OperandTypeUses> _sourceTypeUses = [];

        private readonly Dictionary<ObjectTypeId, TypeAccessInput> _typeAccess = [];
        private readonly Dictionary<ObjectTypeId, TypeBaseInput> _typeBases = [];

        private TypeBaseInput TypeBase(ResolvedType type, Work? work, int offset)
        {
            var id = ObjectModelBuilder.Id(type);
            if (_snapshots.TryGetValue(id, out var structural)) return new(structural.BaseType);
            if (_typeBases.TryGetValue(id, out var cached)) return cached;
            var declaration = Require(resolver.ReadTypeBase(type, _metadata), work, offset);
            // As with lexical facts, bounded caching is independent of admission.
            // Uncached declarations charge the existing shared work ledger on each read.
            if (_typeBases.Count < ObjectModelLimits.MaximumTypes) _typeBases.Add(id, declaration);
            return declaration;
        }

        private TypeAccessInput TypeAccess(ResolvedType type, Work? work, int offset)
        {
            var id = ObjectModelBuilder.Id(type);
            if (_snapshots.TryGetValue(id, out var structural))
                return new(structural.Source, structural.OwnerGenericArity, structural.ExternallyVisible, structural.LexicalOwners);
            if (_typeAccess.TryGetValue(id, out var cached)) return cached;
            var access = Require(resolver.ReadTypeAccess(type, _metadata), work, offset);
            // Cache size and metadata work stay bounded independently. Additional
            // declarations are checked without retention, charging every read.
            if (_typeAccess.Count < ObjectModelLimits.MaximumTypes) _typeAccess.Add(id, access);
            return access;
        }

        private void CheckOperandTypeUses(Work work, MethodInstruction instruction)
        {
            if (instruction.OpCode is not ("call" or "callvirt" or "newobj" or "ldftn" or "ldvirtftn"
                or "ldfld" or "ldflda" or "stfld" or "ldsfld" or "ldsflda" or "stsfld"
                or "newarr" or "ldelem" or "stelem" or "ldelema" or "initobj" or "ldobj" or "stobj" or "cpobj"
                or "box" or "unbox" or "unbox.any" or "isinst" or "castclass" or "ldtoken" or "constrained.")) return;
            int token = instruction.OpCode switch {
                "call" or "callvirt" or "newobj" or "ldftn" or "ldvirtftn" => Token(work, instruction, 0x06, 0x0a, 0x2b),
                "ldfld" or "ldflda" or "stfld" or "ldsfld" or "ldsflda" or "stsfld" => Token(work, instruction, 0x04, 0x0a),
                "ldtoken" => Token(work, instruction, 0x01, 0x02, 0x1b, 0x04, 0x06, 0x0a, 0x2b),
                _ => Token(work, instruction, 0x01, 0x02, 0x1b)
            };
            CheckTypeUseToken(work, token, instruction.Offset);
        }

        private void CheckTypeUseToken(Work work, int token, int offset)
        {
            var key = (work.Assembly.ContentHash, token);
            if (!_sourceTypeUses.TryGetValue(key, out var uses))
            {
                uses = Require(resolver.ReadOperandTypeUses(work.Resolved, MetadataTokens.EntityHandle(token), _metadata), work, offset);
                _sourceTypeUses.Add(key, uses);
            }
            CheckSourceTypeUses(uses.Signatures, work, offset);
        }

        // Type-name visibility is a property of declaration nesting. Generic arity
        // affects closed member identity, but does not hide an assembly-internal
        // declaration from another generic method in the same image. Member access
        // continues to use its existing (more restricted) closed-owner rules.
        private static bool SourceTypeVisible(ObjectMethodBinding caller, ObjectTypeId target,
            ImmutableArray<LexicalTypeOwner> chain)
        {
            if (caller.Owner.ContentHash != target.ContentHash || chain.IsDefaultOrEmpty
                || !ObjectLexicalAccess.Valid(target, chain)) return false;
            for (int i = 0; i < chain.Length - 1; ++i)
            {
                switch (chain[i].Visibility)
                {
                    case TypeAttributes.NestedPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem: break;
                    case TypeAttributes.NestedPrivate:
                        int parent = chain[i + 1].Token;
                        if (caller.Owner.Token != parent && !caller.LexicalOwners.Any(o => o.Token == parent)) return false;
                        break;
                    default: return false;
                }
            }
            return true;
        }

        private void CheckSourceTypeUses(IEnumerable<TypeSignature> signatures, Work work, int offset)
        {
            foreach (var signature in signatures)
            {
                var pending = new Stack<TypeSignature>();
                pending.Push(signature); int count = 0;
                while (pending.TryPop(out var item))
                {
                    if (++count > 4096) Fail(work, "Source type access exceeds the signature-node limit.", offset, DiagnosticIds.TranslationLimit);
                    switch (item)
                    {
                        case TypeSignature.Named named:
                            var declaration = TypeAccess(named.Definition, work, offset);
                            if (declaration.ExternallyVisible || declaration.Source.ContentHash == work.Binding.Owner.ContentHash
                                && declaration.Source.Token == work.Binding.Owner.Token) break;
                            if (EnumScalar(named, work, offset))
                            {
                                CheckEnumDeclarationAccess(named.Definition, work, offset); // An explicit name cannot borrow opaque argument authority.
                                break;
                            }
                            if (!SourceTypeVisible(work.Binding, ObjectTypeId.From(declaration.Source), declaration.LexicalOwners))
                                Fail(work, "Explicit type use requires lexical declaring scope or an externally visible type. Type: "
                                    + declaration.Source.Name + ".", offset);
                            break;
                        case TypeSignature.Generic generic:
                            pending.Push(generic.Definition);
                            foreach (var argument in generic.Arguments) pending.Push(argument);
                            break;
                        case TypeSignature.Vector vector: pending.Push(vector.Element); break;
                        case TypeSignature.ByReference reference: pending.Push(reference.Element); break;
                        // Variables carry their already-bound argument; they do not spell its
                        // private name in this image. Closed binding still checks indices/constraints.
                        case TypeSignature.Variable or TypeSignature.Primitive: break;
                    }
                }
            }
        }
    }
}
