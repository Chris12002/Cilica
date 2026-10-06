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
        private ValueIntrinsicCall? ArrayCopyStoreCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var owner = resolver.ResolveType(library, "Cilica.Runtime", "ArrayInterface`1").Value;
            if (owner is null || owner != target.DeclaringType.Definition) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Array copy store metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Array copy store metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (input.DeclaredName is not ("CopyStore" or "CopyCompatible")) return null;
            bool compatible = input.DeclaredName == "CopyCompatible";
            if (work.Assembly != library || instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0)
                Fail(work, "Array copy stores are private to the selected core implementation.", offset);
            if (input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 1
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive result || result.Code != (compatible ? PrimitiveTypeCode.Boolean : PrimitiveTypeCode.Void)
                || input.Signature.Parameters.Count != (compatible ? 2 : 3)
                || input.Signature.Parameters[0] is not TypeSignature.Vector { Element: TypeSignature.Variable { IsMethod: false, Index: 0 } }
                || (compatible ? input.Signature.Parameters[1] is not TypeSignature.Vector { Element: TypeSignature.Variable { IsMethod: false, Index: 0 } }
                    : input.Signature.Parameters[1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                        || input.Signature.Parameters[2] is not TypeSignature.Variable { IsMethod: false, Index: 0 }))
                Fail(work, "Invalid selected array copy store declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, compatible ? LibraryIntrinsicCapability.ArrayCopyCompatible : LibraryIntrinsicCapability.ArrayCopyStore), work, offset);
            return new(compatible ? ValueIntrinsic.ArrayCopyCompatible : ValueIntrinsic.ArrayCopyStore, Vector(target.DeclaringType.Arguments[0], work, offset), compatible ? 2 : 3);
        }
    }
}
