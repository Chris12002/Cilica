using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private void DictionaryKeyText(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "DictionaryKeyText`1").Value) return;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Dictionary key text metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Dictionary key text metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (work.Assembly != library || instruction.OpCode != "call" || input.DeclaredName != "Render"
                || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] != new TypeSignature.Variable(false, 0)
                || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.String))
                Fail(work, "Dictionary key text requires the exact private owned declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.DictionaryKeyText), work, offset);
            var argument = target.DeclaringType.Arguments[0]; CheckArgumentAccess(argument, work, offset);
            if (argument is TypeSignature.Primitive primitive)
            {
                string? method = primitive.Code switch {
                    PrimitiveTypeCode.Int32 => "Int32Invariant", PrimitiveTypeCode.UInt32 => "UInt32Invariant",
                    PrimitiveTypeCode.Int64 => "Int64Invariant", PrimitiveTypeCode.UInt64 => "UInt64Invariant",
                    PrimitiveTypeCode.String => "Render", _ => null };
                if (method is null) Fail(work, "Dictionary key text requires an admitted integer, string or ordinary named value.", offset);
                var helper = Require(resolver.ResolveType(library, primitive.Code == PrimitiveTypeCode.String ? "Cilica.Runtime" : "Cilica.Library",
                    primitive.Code == PrimitiveTypeCode.String ? "StringKeyText" : "IntegerFormatting"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(helper, SignatureList<TypeSignature>.Empty), method!, 1, _metadata), work, offset);
            }
            else if (OperandSignature(argument, work, offset) is TypeSignature.Named { Kind: SignatureTypeKind.Class }
                or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class })
            {
                var (_, implementation) = ReferenceKeyTextTarget(argument, work, offset);
                if (implementation.DeclaringType.Definition != resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object).Value)
                    target = implementation;
                else
                {
                    var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "DefaultReferenceKeyText`1"), work, offset);
                    target = Require(resolver.ResolveClosedDeclaration(new(helper, new([argument])), "Render", 1, _metadata), work, offset);
                }
            }
            else
            {
                _ = FormattingReceiver(argument, work, offset);
                var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "BoxedKeyText`1"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(helper, new([argument])), "Render", 1, _metadata), work, offset);
            }
        }
        private (ConstructedType Receiver, ResolvedMethod Implementation) ReferenceKeyTextTarget(TypeSignature signature, Work work, int offset)
        {
            var receiver = OperandSignature(signature, work, offset) switch {
                TypeSignature.Named { Kind: SignatureTypeKind.Class } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                _ => null };
            if (receiver is null || (SnapshotClosed(receiver, work, offset).Attributes & (TypeAttributes.Sealed | TypeAttributes.Interface)) != TypeAttributes.Sealed)
                Fail(work, "Reference dictionary key text requires an exact sealed ordinary class; polymorphic keys require later formatting dispatch.", offset);
            var formattable = Require(resolver.ResolveType(library, "System", "IFormattable"), work, offset);
            var pending = new Queue<ConstructedType>(); pending.Enqueue(receiver!);
            var visited = new HashSet<ObjectTypeId>();
            while (pending.TryDequeue(out var type))
            {
                if (!visited.Add(ClosedId(type, work, offset))) continue;
                if (visited.Count > ObjectModelLimits.MaximumTypes)
                    Fail(work, "Reference key formatting hierarchy exceeds its limit.", offset, DiagnosticIds.TranslationLimit);
                if (type.Definition == formattable)
                    Fail(work, "Reference dictionary key text for IFormattable requires provider dispatch before execution.", offset);
                var shape = SnapshotClosed(type, work, offset);
                foreach (var item in shape.Interfaces.Concat(shape.BaseType is { } parent ? new[] { parent } : []))
                    pending.Enqueue(item switch {
                        TypeSignature.Named named => new(named.Definition, SignatureList<TypeSignature>.Empty),
                        TypeSignature.Generic generic => new(generic.Definition.Definition, generic.Arguments),
                        _ => throw new System.ArgumentException("Invalid reference key formatting hierarchy.") });
            }
            var obj = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset);
            var declaration = Require(resolver.ResolveClosedDeclaration(new(obj, SignatureList<TypeSignature>.Empty), "ToString", 0, _metadata), work, offset);
            return (receiver!, ResolveVirtual(declaration, receiver!, work, offset).Implementation);
        }

        private ImmutableArray<ushort>? ReferenceKeyTypeName(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "DefaultReferenceKeyText`1").Value)
                return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Reference key name metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Reference key name metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            if (input.DeclaredName != "Name") return null;
            if (work.Assembly != library || work.Resolved.DeclaringType != target.DeclaringType || instruction.OpCode != "call"
                || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer || input.HasBody
                || input.OwnerGenericArity != 1 || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Attributes != (MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Signature.Parameters.Count != 0 || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.String))
                Fail(work, "Reference key names require the exact private selected declaration and closed-owner caller.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ReferenceKeyTypeName), work, offset);
            var signature = target.DeclaringType.Arguments[0]; CheckArgumentAccess(signature, work, offset);
            var (receiver, implementation) = ReferenceKeyTextTarget(signature, work, offset);
            if (implementation.DeclaringType.Definition != resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object).Value)
                Fail(work, "Reference key type names require the inherited default Object.ToString implementation.", offset);
            var source = SnapshotClosed(receiver, work, offset).Source;
            string name = source.RuntimeName ?? source.Name.Replace('/', '+');
            if (name.Length > ObjectStringLimits.MaximumLiteralCodeUnits || name.Length > ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits)
                Fail(work, "Reference key name exceeds the string literal budget.", offset, DiagnosticIds.TranslationLimit);
            _literalUnits += name.Length; _ = IncludeString(work, offset);
            return [.. name.Select(c => (ushort)c)];
        }
    }
}
