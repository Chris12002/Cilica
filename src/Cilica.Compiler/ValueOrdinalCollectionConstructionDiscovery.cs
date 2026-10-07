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
        private (IrMethodId Getter, ObjectTypeId Owner)? OrdinalCollectionConstruction(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count is not (1 or 2)
                || target.Signature.Parameters[^1] is not TypeSignature.Generic) return null;
            bool dictionary = target.DeclaringType.Definition == resolver.ResolveType(library, "System.Collections.Generic", "Dictionary`2").Value;
            if (!dictionary && target.DeclaringType.Definition != resolver.ResolveType(library, "System.Collections.Generic", "HashSet`1").Value) return null;
            int arity = dictionary ? 2 : 1;
            int parameterCount = target.Signature.Parameters.Count;
            int offset = instruction.Offset;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Ordinal collection metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Ordinal collection metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName != ".ctor" || input.HasBody) return null; // Enumerable construction retains its ordinary body.
            bool firstParameter = parameterCount == 1 || input.Signature.Parameters.Count == 2 && (dictionary
                ? input.Signature.Parameters[0] is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                : input.Signature.Parameters[0] is TypeSignature.Generic source && source.Definition.Kind == SignatureTypeKind.Class
                    && source.Definition.Definition == Require(resolver.ResolveType(library, "System.Collections.Generic", "IEnumerable`1"), work, offset)
                    && source.Arguments.Count == 1 && source.Arguments[0] is TypeSignature.Variable { IsMethod: false, Index: 0 });
            var comparer = Require(resolver.ResolveType(library, "System.Collections.Generic", "IEqualityComparer`1"), work, offset);
            if (instruction.OpCode != "newobj" || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != arity || target.DeclaringType.Arguments.Count != arity || target.MethodArguments.Count != 0
                || target.DeclaringType.Arguments[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName) || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters.Count != parameterCount
                || !firstParameter
                || input.Signature.Parameters[^1] is not TypeSignature.Generic contract
                || contract.Definition.Kind != SignatureTypeKind.Class || contract.Definition.Definition != comparer
                || contract.Arguments.Count != 1 || contract.Arguments[0] is not TypeSignature.Variable { IsMethod: false, Index: 0 })
                Fail(work, "Ordinal collection construction requires an exact HashSet<string> or Dictionary<string, TValue> newobj contract and a directly proven StringComparer.Ordinal getter.", offset);
            var helper = Require(resolver.ResolveClosedDeclaration(target.DeclaringType, "CreateOrdinal", parameterCount, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, helper.Handle, dictionary ? LibraryIntrinsicCapability.DictionaryOrdinalConstruction : LibraryIntrinsicCapability.HashSetOrdinalConstruction), work, offset);
            var body = Read(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != arity || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig) || body.Implementation != MethodImplAttributes.IL
                || body.Signature.Parameters.Count != parameterCount
                || !body.Signature.Parameters.SequenceEqual(input.Signature.Parameters)
                || body.Signature.ReturnType is not TypeSignature.Generic result || result.Definition.Kind != SignatureTypeKind.Class
                || result.Definition.Definition != target.DeclaringType.Definition || result.Arguments.Count != arity
                || result.Arguments.Where((argument, index) => argument is not TypeSignature.Variable { IsMethod: false } variable || variable.Index != index).Any())
                Fail(work, "Ordinal collection construction requires the exact managed comparer factory.", offset);
            var getter = OrdinalComparerGetter(work, offset);
            target = helper;
            return (getter, IncludeReference(target.DeclaringType, work, offset));
        }
    }
}
