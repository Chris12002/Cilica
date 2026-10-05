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
        private ValueIntrinsicCall? IntegerCacheCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var owner = resolver.ResolveType(library, "Cilica.Runtime", "IntegerTextCache").Value;
            if (owner is null || owner != target.DeclaringType.Definition) return null;
            int offset = instruction.Offset;
            if (work.Assembly != library || instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0)
                Fail(work, "Integer text-cache intrinsics are private to the selected core implementation.", offset);
            if (_metadata.Remaining == 0) Fail(work, "Integer cache declaration exceeds its metadata budget.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Integer cache declaration exceeds its metadata budget.", offset, DiagnosticIds.TranslationLimit);
            bool publish = input.DeclaredName == "Publish";
            if (input.DeclaredName is not ("Find" or "Publish") || input.IsReferenceAssembly || input.OwnerGenericArity != 0
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Parameters.Count != (publish ? 2 : 1)
                || input.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.UInt32 }
                || publish && input.Signature.Parameters[1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String })
                Fail(work, "Invalid selected integer text-cache declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, publish ? LibraryIntrinsicCapability.IntegerTextCachePublish : LibraryIntrinsicCapability.IntegerTextCacheFind), work, offset);
            return new(publish ? ValueIntrinsic.IntegerTextPublish : ValueIntrinsic.IntegerTextFind, IncludeString(work, offset), publish ? 2 : 1);
        }
    }
}
