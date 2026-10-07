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
        private bool OrdinalPrefix(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count != 2
                || target.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Boolean }
                || target.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || target.Signature.Parameters[1] is not TypeSignature.Named
                || target.DeclaringType.Definition != resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String).Value) return false;
            int offset = instruction.Offset;
            MethodDeclarationInput Declaration(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Ordinal prefix metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Ordinal prefix metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Declaration(target);
            if (input.DeclaredName != "StartsWith") return false;
            var comparison = Require(resolver.ResolveType(library, "System", "StringComparison"), work, offset);
            if (instruction.OpCode is not ("call" or "callvirt") || input.IsReferenceAssembly || input.HasBody
                || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 0
                || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.Parameters[1] is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType } mode || mode.Definition != comparison)
                Fail(work, "Ordinal prefix requires the exact selected StartsWith(String,StringComparison) call declaration; function pointers require later lowering.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.StringOrdinalPrefix), work, offset);
            ValidateStringComparison(comparison, work, offset);
            var helper = Require(resolver.ResolveClosedDeclaration(target.DeclaringType, "StartsWithOrdinal", 1, _metadata), work, offset);
            var body = Declaration(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 0 || body.Signature.Header != 0x20 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig) || body.Implementation != MethodImplAttributes.IL
                || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Boolean }
                || body.Signature.Parameters.Count != 1 || body.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String })
                Fail(work, "Ordinal prefix helper requires its exact managed instance signature.", offset);
            target = helper;
            return true;
        }
    }
}
