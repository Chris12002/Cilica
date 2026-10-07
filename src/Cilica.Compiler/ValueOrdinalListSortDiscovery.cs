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
        private OrdinalListSortCall? OrdinalListSort(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count != 1
                || target.Signature.Parameters[0] is not TypeSignature.Generic
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System.Collections.Generic", "List`1").Value) return null;
            int offset = instruction.Offset;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Ordinal sort metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Ordinal sort metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName != "Sort" || input.HasBody) return null; // Comparison<T> retains its ordinary body.
            var comparer = Require(resolver.ResolveType(library, "System.Collections.Generic", "IComparer`1"), work, offset);
            if (instruction.OpCode != "callvirt" || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || target.DeclaringType.Arguments[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig) || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] is not TypeSignature.Generic contract
                || contract.Definition.Kind != SignatureTypeKind.Class || contract.Definition.Definition != comparer
                || contract.Arguments.Count != 1 || contract.Arguments[0] is not TypeSignature.Variable { IsMethod: false, Index: 0 })
                Fail(work, "List.Sort(IComparer<T>) currently requires the exact List<string> callvirt contract and a directly proven StringComparer.Ordinal getter.", offset);
            var helper = Require(resolver.ResolveClosedDeclaration(target.DeclaringType, "SortOrdinal", 0, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, helper.Handle, LibraryIntrinsicCapability.ListOrdinalSort), work, offset);
            var body = Read(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 1 || body.Signature.Header != 0x20 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig) || body.Implementation != MethodImplAttributes.IL
                || body.Signature.Parameters.Count != 0 || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void })
                Fail(work, "Ordinal list sort requires the exact managed no-argument bridge.", offset);
            var getter = OrdinalComparerGetter(work, offset);
            target = helper;
            return new(getter, ClosedId(target.DeclaringType, work, offset));
        }
    }
}
