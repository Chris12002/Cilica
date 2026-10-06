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
        private ValueIntrinsicCall? OrdinalStringCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count != 3) return null;
            var text = resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String).Value;
            if (text is null || target.DeclaringType.Definition != text) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Ordinal string metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Ordinal string metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (input.DeclaredName != "Equals") return null;
            var comparison = Require(resolver.ResolveType(library, "System", "StringComparison"), work, offset);
            if (instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 0
                || input.DeclaredName != "Equals" || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Boolean }
                || input.Signature.Parameters.Count != 3
                || input.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Parameters[1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || input.Signature.Parameters[2] is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType } mode || mode.Definition != comparison
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall)
                Fail(work, "Only the exact selected ordinal String.Equals declaration is implemented.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.StringOrdinalEqual), work, offset);
            var metadata = Snapshot(comparison, work, offset);
            var enumBase = Require(resolver.ResolveType(library, "System", "Enum"), work, offset);
            var fields = metadata.Fields.Where(f => (f.Attributes & FieldAttributes.Static) == 0).ToArray();
            if (metadata.IsReferenceAssembly || metadata.OwnerGenericArity != 0 || metadata.HasModuleInitializer || metadata.HasTypeInitializer
                || metadata.BaseType is not TypeSignature.Named parent || parent.Definition != enumBase
                || (metadata.Attributes & (TypeAttributes.VisibilityMask | TypeAttributes.Sealed)) != (TypeAttributes.Public | TypeAttributes.Sealed)
                || fields.Length != 1 || fields[0].Name != "value__" || fields[0].HasExplicitStorage
                || fields[0].Signature is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                || fields[0].Attributes != (FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName))
                Fail(work, "StringComparison requires its exact public Int32 enum storage contract.", offset);
            return new(ValueIntrinsic.StringOrdinalEqual, IncludeString(work, offset), 3);
        }
    }
}
