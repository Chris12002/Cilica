using System.Linq;
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
        private enum OrdinalTextBridgeKind { Prefix, Compare }
        private OrdinalTextBridgeKind? OrdinalTextBridge(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count is not (2 or 3)
                || target.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Boolean or PrimitiveTypeCode.Int32 }
                || target.DeclaringType.Definition != resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String).Value) return null;
            int offset = instruction.Offset;
            MethodDeclarationInput Declaration(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Ordinal text metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Ordinal text metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Declaration(target);
            bool prefix = input.DeclaredName == "StartsWith";
            if (!prefix && input.DeclaredName != "Compare") return null;
            int parameters = prefix ? 2 : 3;
            var comparison = Require(resolver.ResolveType(library, "System", "StringComparison"), work, offset);
            var flags = MethodAttributes.Public | MethodAttributes.HideBySig | (prefix ? 0 : MethodAttributes.Static);
            var resultCode = prefix ? PrimitiveTypeCode.Boolean : PrimitiveTypeCode.Int32;
            if ((prefix ? instruction.OpCode is not ("call" or "callvirt") : instruction.OpCode != "call")
                || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 0
                || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.Signature.Header != (prefix ? 0x20 : 0) || input.Signature.GenericArity != 0
                || input.Attributes != flags || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType != new TypeSignature.Primitive(resultCode) || input.Signature.Parameters.Count != parameters
                || input.Signature.Parameters.Take(parameters-1).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String })
                || input.Signature.Parameters[^1] is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType } mode || mode.Definition != comparison)
                Fail(work, "Ordinal text requires the exact selected StartsWith/Compare declaration; function pointers require later lowering.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle,
                prefix ? LibraryIntrinsicCapability.StringOrdinalPrefix : LibraryIntrinsicCapability.StringOrdinalCompare), work, offset);
            ValidateStringComparison(comparison, work, offset);
            var helper = Require(resolver.ResolveClosedDeclaration(target.DeclaringType, prefix ? "StartsWithOrdinal" : "CompareOrdinal", parameters-1, _metadata), work, offset);
            var body = Declaration(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 0 || body.Signature.Header != (prefix ? 0x20 : 0) || body.Signature.GenericArity != 0
                || body.Attributes != flags || body.Implementation != MethodImplAttributes.IL
                || body.Signature.ReturnType != new TypeSignature.Primitive(resultCode)
                || body.Signature.Parameters.Count != parameters-1 || body.Signature.Parameters.Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }))
                Fail(work, "Ordinal text helper requires its exact managed signature.", offset);
            target = helper;
            return prefix ? OrdinalTextBridgeKind.Prefix : OrdinalTextBridgeKind.Compare;
        }
    }
}
