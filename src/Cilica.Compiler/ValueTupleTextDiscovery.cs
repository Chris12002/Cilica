using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private void TupleElementText(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "TupleElementText`1").Value) return;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Tuple text metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Tuple text metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (work.Assembly != library || instruction.OpCode != "call" || input.DeclaredName != "Render"
                || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] != new TypeSignature.ByReference(new TypeSignature.Variable(false, 0))
                || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.String))
                Fail(work, "Tuple text requires the exact private owned by-reference declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.TupleElementText), work, offset);
            var argument = target.DeclaringType.Arguments[0]; CheckArgumentAccess(argument, work, offset);
            var normalized = OperandSignature(argument, work, offset);
            if (normalized is TypeSignature.Primitive { Code: PrimitiveTypeCode.String or PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 } primitive)
            {
                var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "TuplePrimitiveText"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(helper, SignatureList<TypeSignature>.Empty), primitive.Code.ToString(), 1, _metadata), work, offset);
                return;
            }
            if (IsNullable(argument, work, offset) || EnumScalar(normalized, work, offset))
                Fail(work, "Nullable and enum tuple component text require later lowering.", offset);
            var receiver = normalized switch {
                TypeSignature.Named { Kind: SignatureTypeKind.ValueType } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                _ => null };
            if (receiver is null)
                Fail(work, "Tuple component text requires admitted 32/64-bit integers, strings or named value ToString overrides; other component types require later lowering.", offset);
            var obj = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset);
            var declaration = Require(resolver.ResolveClosedDeclaration(new(obj, SignatureList<TypeSignature>.Empty), "ToString", 0, _metadata), work, offset);
            target = Require(resolver.ResolveConstrainedValueTarget(declaration, receiver!, library, _metadata), work, offset).Implementation;
        }
    }
}
