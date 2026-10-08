using System.Collections.Generic;
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
        private ConstrainedValueCall? ConstrainedEnumText(Work work, MethodInstruction instruction, ResolvedMethod target,
            IReadOnlyDictionary<int, MethodInstruction> constrained, out ResolvedMethod? bridge)
        {
            bridge = null;
            if (!integers64 || instruction.OpCode != "callvirt" || !constrained.TryGetValue(instruction.Offset, out var prefix)
                || target.DeclaringType.Definition != resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object).Value
                || target.Signature.Parameters.Count != 0 || target.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }) return null;
            int offset = instruction.Offset;
            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                MetadataTokens.EntityHandle(Token(work, prefix, 0x01, 0x02, 0x1b))), work, prefix.Offset);
            if (!EnumScalar(signature, work, offset) || signature is not TypeSignature.Named named) return null;
            CheckScalarOperandAccess(signature, work, offset);
            MethodDeclarationInput Declaration(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Enum text metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var input = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Enum text metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                return input;
            }
            var source = Declaration(target);
            if (source.DeclaredName != "ToString" || source.IsReferenceAssembly || source.HasBody || source.HasTypeInitializer || source.HasModuleInitializer
                || source.OwnerGenericArity != 0 || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || source.Signature.Header != 0x20 || source.Signature.GenericArity != 0
                || source.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot)
                || source.Implementation != MethodImplAttributes.InternalCall)
                Fail(work, "Enum text requires the exact selected constrained Object.ToString declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.EnumFormatting), work, offset);
            _ = EnumFormattingConstants(named.Definition, work);
            // Table entries use the existing validated enum payload descriptor; no box is allocated.
            _ = ScalarBoxOperand(new TypeSignature.Named(named.Definition, SignatureTypeKind.ValueType), work, offset);
            var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "EnumDefaultText`1"), work, offset);
            bridge = Require(resolver.ResolveClosedDeclaration(new(helper, new([new TypeSignature.Named(named.Definition, SignatureTypeKind.ValueType)])), "Default", 1, _metadata), work, offset);
            var body = Declaration(bridge);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer || body.OwnerGenericArity != 1
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || body.Implementation != MethodImplAttributes.IL || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || body.Signature.Parameters.Count != 1 || body.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 })
                Fail(work, "Enum text requires its exact managed Int32/string bridge.", offset);
            return new(prefix.Offset, ObjectModelBuilder.Id(named.Definition));
        }
    }
}
