using System.Collections.Generic;
using System.Collections.Immutable;
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
        private bool _enumMetadataRequired;
        private readonly Dictionary<ResolvedType, ImmutableArray<EnumConstantInput>> _enumConstants = [];
        private ImmutableArray<EnumConstantInput> EnumConstants(ResolvedType type, Work work)
        {
            if (_enumConstants.TryGetValue(type, out var known)) return known;
            if (_enumConstants.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Enum metadata type limit exceeded.", code: DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadInt32EnumConstants(type, _metadata), work);
            _enumConstants.Add(type, input.Constants); return input.Constants;
        }
        private ValueIntrinsicCall? EnumQueryCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "EnumMetadata").Value) return null;
            if (_metadata.Remaining == 0) Fail(work, "Enum query metadata limit exceeded.", instruction.Offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, instruction.Offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Enum query metadata limit exceeded.", instruction.Offset, DiagnosticIds.TranslationLimit);
            bool message = input.DeclaredName == "ErrorMessage";
            var facts = RuntimeTypes(work, instruction.Offset);
            if (work.Assembly != library || instruction.OpCode != "call" || input.DeclaredName is not ("Query" or "ErrorMessage")
                || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer || input.HasBody
                || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Signature.Parameters.Count != 2 || input.Signature.Parameters[0] is not TypeSignature.Named type
                || ObjectModelBuilder.Id(type.Definition) != facts.Type
                || input.Signature.Parameters[1] != new TypeSignature.Primitive(PrimitiveTypeCode.Object)
                || input.Signature.ReturnType != new TypeSignature.Primitive(message ? PrimitiveTypeCode.String : PrimitiveTypeCode.Int32))
                Fail(work, "Enum metadata queries require exact selected declarations and an owned-library caller.", instruction.Offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle,
                message ? LibraryIntrinsicCapability.EnumIsDefinedErrorMessage : LibraryIntrinsicCapability.EnumIsDefinedQuery), work, instruction.Offset);
            _ = IncludeString(work, instruction.Offset); _enumMetadataRequired = true;
            return new(message ? ValueIntrinsic.EnumErrorMessage : ValueIntrinsic.EnumQuery, facts.Implementation, 2) { RuntimeTypeBase = facts.Type };
        }
    }
}
