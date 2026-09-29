using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private sealed partial class ReachabilityWalker
    {
        private sealed record ConstrainedPrefix(Instruction Instruction, bool SupportedSequence);

        private Dictionary<int, ConstrainedPrefix> ConstrainedPrefixes(int id, List<Instruction> instructions)
        {
            var result = new Dictionary<int, ConstrainedPrefix>();
            var prefixes = new List<Instruction>();
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Code.Flow == FlowKind.Prefix) { prefixes.Add(instruction); continue; }
                Instruction? constraint = prefixes.FirstOrDefault(p => p.Code.Name == "constrained.");
                if (constraint is not null)
                {
                    bool valid = true;
                    if (prefixes.Count(p => p.Code.Name == "constrained.") != 1)
                    {
                        Issue(id, constraint.Offset, DiagnosticIds.InvalidOperand, "Duplicate constrained. prefix in one instruction sequence.");
                        valid = false;
                    }
                    if (instruction.Code.Name is "call" or "ldftn")
                    {
                        Issue(id, constraint.Offset, DiagnosticIds.ReachabilityIncomplete,
                            "Constrained static-interface call/ldftn requires a later dispatch profile.");
                        valid = false;
                    }
                    else if (instruction.Code.Name != "callvirt")
                    {
                        Issue(id, constraint.Offset, DiagnosticIds.InvalidOperand,
                            "constrained. must prefix callvirt, or a supported static-interface call/ldftn.");
                        valid = false;
                    }
                    if (prefixes.Any(p => p.Code.Name is not ("constrained." or "tail.")))
                    {
                        Issue(id, constraint.Offset, DiagnosticIds.ReachabilityIncomplete,
                            "This combination of constrained. and other prefixes is outside the instance-call profile.");
                        valid = false;
                    }
                    result.Add(instruction.Offset, new(constraint, valid));
                }
                prefixes.Clear();
            }
            return result;
        }

        private void ConstrainedCall(int id, ResolvedAssembly assembly, Instruction instruction, BindingContext context,
            ResolvedMethod target, ConstrainedPrefix prefix)
        {
            ConstructedType? constraint = null;
            int? constraintId = null;
            Guard(id, prefix.Instruction.Offset, () =>
            {
                TypeSignature signature = InContext(new SignatureDecoder(_resolver, assembly).Type(Token(prefix.Instruction.Token)), context);
                Signature(id, signature, DependencyKind.ConstrainedType, prefix.Instruction.Offset);
                ConstructedType? named = signature switch
                {
                    TypeSignature.Primitive p => PrimitiveReceiver(id, p.Code, DependencyKind.ConstrainedType, prefix.Instruction.Offset, required: true),
                    TypeSignature.Named n => OpenType(n.Definition),
                    TypeSignature.Generic g => new(g.Definition.Definition, g.Arguments),
                    _ => null,
                };
                if (named is null || named.Arguments.Any(ContainsVariable))
                {
                    Issue(id, prefix.Instruction.Offset, DiagnosticIds.ReachabilityIncomplete,
                        "Constrained receiver requires a closed named type; open variables, primitives, arrays and other forms need additional receiver mapping.");
                    return;
                }
                // Signature discovery can stop at a graph limit. Do not manufacture
                // receiver evidence for a type that was not actually included.
                if (!_ids.TryGetValue(new ReachableEntity.Type(named), out int typeId)) return;
                constraint = named;
                constraintId = typeId;
            });
            Add(new ReachableEntity.Method(target), id, DependencyKind.DispatchDeclaration, instruction.Offset, constraintType: constraintId);
            if (!prefix.SupportedSequence || constraint is null) return;
            MethodDefinition definition = MethodDefinition(target);
            if ((definition.Attributes & MethodAttributes.Static) != 0 || (target.Signature.Header & 0x20) == 0)
            {
                Issue(id, instruction.Offset, DiagnosticIds.InvalidOperand, "Constrained callvirt requires an instance method.");
                return;
            }
            RegisterDispatch(new(id, instruction.Offset, target, constraint, constraintId));
        }

        private static bool ContainsVariable(TypeSignature type) => type switch
        {
            TypeSignature.Variable => true,
            TypeSignature.Generic g => g.Arguments.Any(ContainsVariable),
            TypeSignature.Vector v => ContainsVariable(v.Element),
            TypeSignature.Array a => ContainsVariable(a.Element),
            TypeSignature.Pointer p => ContainsVariable(p.Element),
            TypeSignature.ByReference r => ContainsVariable(r.Element),
            TypeSignature.Pinned p => ContainsVariable(p.Element),
            TypeSignature.Modified m => ContainsVariable(m.Element),
            TypeSignature.FunctionPointer f => ContainsVariable(f.Signature.ReturnType) || f.Signature.Parameters.Any(ContainsVariable),
            _ => false,
        };
    }
}
