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
        private ObjectTypeId? CharacterStringConstructor(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count != 1
                || target.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || target.DeclaringType.Definition != resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String).Value) return null;
            int offset = instruction.Offset;
            MethodDeclarationInput Declaration(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Character-string constructor metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Character-string constructor metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Declaration(target);
            if (input.DeclaredName != ".ctor") return null;
            bool Characters(TypeSignature signature) => signature is TypeSignature.Vector { Element: TypeSignature.Primitive { Code: PrimitiveTypeCode.Char } };
            if (instruction.OpCode != "newobj" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters.Count != 1 || !Characters(input.Signature.Parameters[0]))
                Fail(work, "Character-string construction requires the exact selected Char[] newobj declaration.", offset);
            var owner = target.DeclaringType;
            var factory = Require(resolver.ResolveClosedDeclaration(owner, "CopyCharacters", 1, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, factory.Handle, LibraryIntrinsicCapability.StringCharacterConstruction), work, offset);
            var body = Declaration(factory);
            if (body.IsReferenceAssembly || body.HasTypeInitializer || body.HasModuleInitializer || body.OwnerGenericArity != 0
                || body.Signature.Header != 0 || body.Signature.GenericArity != 0 || body.Signature.Parameters.Count != 1
                || !Characters(body.Signature.Parameters[0]) || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || !body.HasBody || body.Implementation != MethodImplAttributes.IL)
                Fail(work, "Character-string factory requires its exact selected managed copy declaration.", offset);
            target = factory;
            return IncludeString(work, offset);
        }
    }
}
