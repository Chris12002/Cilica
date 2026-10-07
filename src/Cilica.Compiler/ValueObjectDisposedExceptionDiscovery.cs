using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ValueIntrinsicCall? ObjectDisposedExceptionCall(Work work, MethodInstruction instruction, ResolvedMethod target,
            ObjectTypeId owner, MethodDeclarationInput input)
        {
            if (!integers64 || !_exceptionResources.Any(r => r.Type == owner && r.Kind == ExceptionResourceKind.ObjectDisposed)) return null;
            static bool Primitive(TypeSignature type, PrimitiveTypeCode code) => type is TypeSignature.Primitive p && p.Code == code;
            int offset = instruction.Offset; var signature = input.Signature;
            if (instruction.OpCode == "newobj" && input.DeclaredName == ".ctor")
            {
                if (input.HasBody || input.Implementation != MethodImplAttributes.InternalCall
                    || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                    || signature.Header != 0x20 || signature.GenericArity != 0 || !Primitive(signature.ReturnType, PrimitiveTypeCode.Void)
                    || signature.Parameters.Count is not (1 or 2) || signature.Parameters.Any(p => !Primitive(p, PrimitiveTypeCode.String)))
                    Fail(work, "ObjectDisposedException requires an exact selected (String) or (String, String) constructor.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ObjectDisposedConstructor), work, offset);
                _ = IncludeString(work, offset); return new(ValueIntrinsic.NewObjectDisposedException, owner, signature.Parameters.Count);
            }
            if (instruction.OpCode == "callvirt" && input.DeclaredName == "get_ObjectName")
            {
                if (input.HasBody || input.Implementation != MethodImplAttributes.InternalCall
                    || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName)
                    || signature.Header != 0x20 || signature.GenericArity != 0 || signature.Parameters.Count != 0
                    || !Primitive(signature.ReturnType, PrimitiveTypeCode.String))
                    Fail(work, "ObjectDisposedException.ObjectName requires the exact selected getter.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ExceptionObjectName), work, offset);
                _ = IncludeString(work, offset); return new(ValueIntrinsic.ExceptionObjectName, owner, 1);
            }
            return null;
        }
    }
}
