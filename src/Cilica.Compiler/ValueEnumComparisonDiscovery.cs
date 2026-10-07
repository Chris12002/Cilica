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
        private ConstrainedValueCall? EnumComparison(Work work, MethodInstruction instruction, ResolvedMethod target,
            IReadOnlyDictionary<int, MethodInstruction> constrained)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System", "Enum").Value) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Enum comparison metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Enum comparison metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (input.DeclaredName != "CompareTo") return null;
            if (instruction.OpCode != "callvirt" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot) || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Object })
                Fail(work, "Enum comparison requires the exact selected CompareTo(Object) callvirt declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.EnumCompareTo), work, offset);
            if (!constrained.TryGetValue(offset, out var prefix))
                Fail(work, "Enum.CompareTo requires a directly constrained Int32 enum receiver; boxed receivers require later lowering.", offset);
            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                MetadataTokens.EntityHandle(Token(work, prefix!, 0x01, 0x02, 0x1b))), work, prefix!.Offset);
            if (!EnumScalar(signature, work, offset) || signature is not TypeSignature.Named)
                Fail(work, "Enum.CompareTo requires a directly constrained Int32 enum receiver; other widths require later lowering.", offset);
            CheckScalarOperandAccess(signature, work, offset);
            // The original box instruction registers a payload layout when needed.
            // A literal-null comparison needs only the validated scalar identity.
            return new(prefix.Offset, ObjectModelBuilder.Id(((TypeSignature.Named)signature).Definition));
        }
    }
}
