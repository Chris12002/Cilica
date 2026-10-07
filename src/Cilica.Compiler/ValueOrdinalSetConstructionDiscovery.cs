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
        private (IrMethodId Getter, ObjectTypeId Owner)? OrdinalSetConstruction(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count != 1
                || target.Signature.Parameters[0] is not TypeSignature.Generic
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System.Collections.Generic", "HashSet`1").Value) return null;
            int offset = instruction.Offset;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Ordinal set metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Ordinal set metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName != ".ctor" || input.HasBody) return null; // Enumerable construction retains its ordinary body.
            var comparer = Require(resolver.ResolveType(library, "System.Collections.Generic", "IEqualityComparer`1"), work, offset);
            if (instruction.OpCode != "newobj" || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || target.DeclaringType.Arguments[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName) || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] is not TypeSignature.Generic contract
                || contract.Definition.Kind != SignatureTypeKind.Class || contract.Definition.Definition != comparer
                || contract.Arguments.Count != 1 || contract.Arguments[0] is not TypeSignature.Variable { IsMethod: false, Index: 0 })
                Fail(work, "HashSet comparer construction currently requires the exact HashSet<string> newobj contract and a directly proven StringComparer.Ordinal getter.", offset);
            var helper = Require(resolver.ResolveClosedDeclaration(target.DeclaringType, "CreateOrdinal", 1, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, helper.Handle, LibraryIntrinsicCapability.HashSetOrdinalConstruction), work, offset);
            var body = Read(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 1 || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig) || body.Implementation != MethodImplAttributes.IL
                || body.Signature.Parameters.Count != 1 || !body.Signature.Parameters[0].Equals(input.Signature.Parameters[0])
                || body.Signature.ReturnType is not TypeSignature.Generic result || result.Definition.Kind != SignatureTypeKind.Class
                || result.Definition.Definition != target.DeclaringType.Definition || result.Arguments.Count != 1
                || result.Arguments[0] is not TypeSignature.Variable { IsMethod: false, Index: 0 })
                Fail(work, "Ordinal set construction requires the exact managed comparer factory.", offset);
            var getter = OrdinalComparerGetter(work, offset);
            target = helper;
            return (getter, IncludeReference(target.DeclaringType, work, offset));
        }
    }
}
