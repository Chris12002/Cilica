using System.Collections.Generic;
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
        private ObjectTypeId? BoxedFormat(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var text = resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String).Value;
            if (text is null || target.DeclaringType.Definition != text || target.Signature.Parameters.Count != 2
                || target.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || target.Signature.Parameters[1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Object }) return null;
            int offset = instruction.Offset;
            _ = FormattingDeclaration(work, instruction, target, false);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.BoxedStringFormat), work, offset);
            var previous = work.Input.Instructions.LastOrDefault(i => i.Offset + i.Length == offset);
            if (previous is null || previous.OpCode != "box")
                Fail(work, "String.Format currently requires one directly boxed named value with an ordinary ToString override and no IFormattable contract.", offset);
            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                MetadataTokens.EntityHandle(Token(work, previous!, 0x01, 0x02, 0x1b))), work, offset);
            var receiver = FormattingReceiver(signature, work, offset);
            var named = new TypeSignature.Named(receiver.Definition, SignatureTypeKind.ValueType);
            signature = receiver.Arguments.Count == 0 ? named : new TypeSignature.Generic(named, receiver.Arguments);
            var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "BoxedFormatting`1"), work, offset);
            target = Require(resolver.ResolveClosedDeclaration(new(helper, new([signature])), "Format", 2, _metadata), work, offset);
            return Include(receiver, work, offset);
        }
        private ObjectTypeId? BoxedRender(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var helper = resolver.ResolveType(library, "Cilica.Runtime", "BoxedText`1").Value;
            if (helper is null || target.DeclaringType.Definition != helper) return null;
            int offset = instruction.Offset;
            if (work.Assembly != library) Fail(work, "Boxed text rendering is private to the selected core implementation.", offset);
            _ = FormattingDeclaration(work, instruction, target, true);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.BoxedTextRender), work, offset);
            var receiver = FormattingReceiver(target.DeclaringType.Arguments[0], work, offset);
            var obj = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset);
            var method = Require(resolver.ResolveClosedDeclaration(new(obj, SignatureList<TypeSignature>.Empty), "ToString", 0, _metadata), work, offset);
            target = Require(resolver.ResolveConstrainedValueTarget(method, receiver, library, _metadata), work, offset).Implementation;
            return Include(receiver, work, offset);
        }
        private MethodDeclarationInput FormattingDeclaration(Work work, MethodInstruction instruction, ResolvedMethod target, bool render)
        {
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Boxed formatting metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Boxed formatting metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (instruction.OpCode != "call" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || target.MethodArguments.Count != 0 || target.DeclaringType.Arguments.Count != (render ? 1 : 0)
                || input.OwnerGenericArity != (render ? 1 : 0) || input.DeclaredName != (render ? "Render" : "Format")
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != ((render ? MethodAttributes.Assembly : MethodAttributes.Public) | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Parameters.Count != (render ? 1 : 2)
                || input.Signature.Parameters[^1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Object }
                || !render && input.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String })
                Fail(work, "Invalid selected boxed formatting declaration.", offset);
            return input;
        }
        private ConstructedType FormattingReceiver(TypeSignature signature, Work work, int offset)
        {
            var receiver = signature switch {
                TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                _ => null };
            if (receiver is null) Fail(work, "Boxed formatting requires an exact named inline value; primitive/reference formatting requires later lowering.", offset);
            if (!Inline(receiver!.Definition, work, offset)) Fail(work, "Boxed formatting requires an inline value receiver.", offset);
            var formattable = Require(resolver.ResolveType(library, "System", "IFormattable"), work, offset);
            var pending = new Queue<ConstructedType>(); pending.Enqueue(receiver!);
            var visited = new HashSet<ObjectTypeId>();
            while (pending.TryDequeue(out var type))
            {
                if (!visited.Add(ClosedId(type, work, offset))) continue;
                if (visited.Count > ObjectModelLimits.MaximumTypes) Fail(work, "Formatting interface closure exceeds its limit.", offset, DiagnosticIds.TranslationLimit);
                if (type.Definition == formattable) Fail(work, "Boxed formatting of IFormattable values requires provider/format dispatch before execution.", offset);
                foreach (var contract in SnapshotClosed(type, work, offset).Interfaces)
                    pending.Enqueue(contract switch {
                        TypeSignature.Named named => new(named.Definition, SignatureList<TypeSignature>.Empty),
                        TypeSignature.Generic generic => new(generic.Definition.Definition, generic.Arguments),
                        _ => throw new System.ArgumentException("Invalid formatting interface declaration.") });
            }
            return receiver!;
        }
    }
}
