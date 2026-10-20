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
        private ValueIntrinsicCall? StringConstructionCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var owner = resolver.ResolveType(library, "Cilica.Runtime", "StringConstruction").Value;
            if (owner is null || owner != target.DeclaringType.Definition) return null;
            int offset = instruction.Offset;
            if (work.Assembly != library || instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0)
                Fail(work, "String construction intrinsics are private to the selected core implementation.", offset);
            if (_metadata.Remaining == 0) Fail(work, "String construction metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "String construction metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            bool character = input.DeclaredName == "FromCharacter";
            bool slice = input.DeclaredName == "FromSlice";
            bool parameters = slice
                ? input.Signature.Parameters.Count == 3
                    && input.Signature.Parameters[0] is TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                    && input.Signature.Parameters[1] is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                    && input.Signature.Parameters[2] is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                : input.Signature.Parameters.Count == 1 && (character
                    ? input.Signature.Parameters[0] is TypeSignature.Primitive { Code: PrimitiveTypeCode.Char }
                    : input.Signature.Parameters[0] is TypeSignature.Vector { Element: TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } });
            if ((!character && !slice && input.DeclaredName != "FromCodeUnits") || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 0
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || !parameters)
                Fail(work, "Invalid selected string construction declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, slice ? LibraryIntrinsicCapability.StringFromSlice
                : character ? LibraryIntrinsicCapability.StringFromCharacter : LibraryIntrinsicCapability.StringFromCodeUnits), work, offset);
            foreach (var parameter in input.Signature.Parameters) _ = Storage(parameter, work, offset);
            return new(slice ? ValueIntrinsic.StringFromSlice : character ? ValueIntrinsic.StringFromCharacter : ValueIntrinsic.StringFromCodeUnits,
                IncludeString(work, offset), input.Signature.Parameters.Count);
        }
    }
}
