using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ObjectTypeId? _typeInitializationException;
        private ValueIntrinsicCall? TypeInitializationExceptionCall(Work work, MethodInstruction instruction, ResolvedMethod target,
            ObjectTypeId owner, MethodDeclarationInput input)
        {
            if (!integers64) return null;
            int offset = instruction.Offset;
            var signature = input.Signature;
            bool ExceptionType(TypeSignature type) => type is TypeSignature.Named { Kind: SignatureTypeKind.Class } named
                && ObjectModelBuilder.Id(named.Definition) == _exceptionBase;
            if (owner == _typeInitializationException && instruction.OpCode == "newobj" && input.DeclaredName == ".ctor")
            {
                if (input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                    || signature.Header != 0x20 || signature.Parameters.Count != 2
                    || signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                    || !ExceptionType(signature.Parameters[1]) || signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void })
                    Fail(work, "TypeInitializationException requires the exact public (String, Exception) constructor.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.TypeInitializationExceptionConstructor), work, offset);
                _ = IncludeString(work, offset);
                return new(ValueIntrinsic.NewTypeInitializationException, owner, 2);
            }
            if (instruction.OpCode == "callvirt" && signature.Header == 0x20 && signature.Parameters.Count == 0
                && input.Attributes == (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName))
            {
                if (owner == _exceptionBase && input.DeclaredName == "get_InnerException" && ExceptionType(signature.ReturnType))
                {
                    _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ExceptionInnerException), work, offset);
                    return new(ValueIntrinsic.ExceptionInnerException, owner, 1);
                }
                if (owner == _typeInitializationException && input.DeclaredName == "get_TypeName"
                    && signature.ReturnType is TypeSignature.Primitive { Code: PrimitiveTypeCode.String })
                {
                    _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ExceptionTypeName), work, offset);
                    _ = IncludeString(work, offset);
                    return new(ValueIntrinsic.ExceptionTypeName, owner, 1);
                }
            }
            return null;
        }
    }
}
