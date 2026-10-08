using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;

public sealed record EnumValuesConstruction(int TokenOffset, int HandleOffset, ObjectTypeId ArrayType, ImmutableArray<int> Values);

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private MethodDeclarationInput EnumValuesDeclaration(Work work, ResolvedMethod target, int offset)
        {
            if (_metadata.Remaining == 0) Fail(work, "Enum values declaration budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Enum values declaration budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            return input;
        }
        private bool IsEnumValues(Work work, ResolvedMethod target, int offset) =>
            target.DeclaringType.Definition == resolver.ResolveType(library, "System", "Enum").Value
            && EnumValuesDeclaration(work, target, offset).DeclaredName == "GetValues";

        private void RejectDynamicEnumValues(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (integers64 && IsEnumValues(work, target, instruction.Offset))
                Fail(work, "Enum.GetValues requires one uninterrupted typeof(Int32-enum)/GetValues sequence; dynamic Type values and aliases require later support.", instruction.Offset);
        }
        private ImmutableDictionary<int, EnumValuesConstruction> EnumValues(Work work)
        {
            var result = ImmutableDictionary.CreateBuilder<int, EnumValuesConstruction>();
            var instructions = work.Input.Instructions;
            var boundaries = instructions.SelectMany(i => i.Targets).ToHashSet();
            foreach (var region in work.Regions)
            {
                boundaries.UnionWith(new[] { region.TryStart, region.TryEnd, region.HandlerStart, region.HandlerEnd });
                if (region.FilterStart is { } filter) boundaries.Add(filter);
            }
            for (int index = 0; index + 2 < instructions.Length; ++index)
            {
                var token = instructions[index]; var handleCall = instructions[index + 1]; var valuesCall = instructions[index + 2];
                if (token.OpCode != "ldtoken" || handleCall.OpCode != "call" || valuesCall.OpCode != "call") continue;
                var target = Require(resolver.ResolveClosedCall(work.Resolved,
                    MetadataTokens.EntityHandle(Token(work, valuesCall, 0x06, 0x0a, 0x2b)), _metadata), work, valuesCall.Offset);
                if (!IsEnumValues(work, target, valuesCall.Offset)) continue;
                if (boundaries.Contains(handleCall.Offset) || boundaries.Contains(valuesCall.Offset))
                    Fail(work, "Enum.GetValues typeof sequence cannot cross control-flow or exception-region boundaries.", valuesCall.Offset);
                foreach (var instruction in new[] { token, handleCall, valuesCall }) CheckOperandTypeUses(work, instruction);
                var element = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                    MetadataTokens.EntityHandle(Token(work, token, 0x01, 0x02, 0x1b))), work, token.Offset);
                CheckScalarOperandAccess(element, work, token.Offset);
                if (!EnumScalar(element, work, token.Offset) || element is not TypeSignature.Named)
                    Fail(work, "Enum.GetValues currently requires a directly named, admitted Int32 enum.", token.Offset);
                var named = (TypeSignature.Named)element;
                var handle = Require(resolver.ResolveClosedCall(work.Resolved,
                    MetadataTokens.EntityHandle(Token(work, handleCall, 0x06, 0x0a)), _metadata), work, handleCall.Offset);
                var type = Require(resolver.ResolveType(library, "System", "Type"), work, token.Offset);
                var array = Require(resolver.ResolveType(library, "System", "Array"), work, token.Offset);
                var runtimeHandle = Require(resolver.ResolveType(library, "System", "RuntimeTypeHandle"), work, token.Offset);
                void Declaration(ResolvedMethod method, bool fromHandle, int offset)
                {
                    var input = EnumValuesDeclaration(work, method, offset);
                    if (method.DeclaringType.Definition.Assembly != library
                        || fromHandle && method.DeclaringType.Definition != type
                        || method.DeclaringType.Arguments.Count != 0 || method.MethodArguments.Count != 0
                        || input.DeclaredName != (fromHandle ? "GetTypeFromHandle" : "GetValues")
                        || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                        || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer
                        || input.HasBody != fromHandle || input.Implementation != (fromHandle ? MethodImplAttributes.IL : MethodImplAttributes.InternalCall)
                        || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                        || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] is not TypeSignature.Named parameter
                        || parameter.Definition != (fromHandle ? runtimeHandle : type)
                        || parameter.Kind != (fromHandle ? SignatureTypeKind.ValueType : SignatureTypeKind.Class)
                        || input.Signature.ReturnType is not TypeSignature.Named returned || returned.Definition != (fromHandle ? type : array)
                        || returned.Kind != SignatureTypeKind.Class)
                        Fail(work, "Enum values require exact granted GetTypeFromHandle and GetValues declarations.", offset);
                    _ = Require(resolver.AuthorizeLibraryIntrinsic(library, method.Handle, LibraryIntrinsicCapability.EnumValues), work, offset);
                }
                Declaration(handle, true, handleCall.Offset); Declaration(target, false, valuesCall.Offset);
                var constants = EnumConstants(named.Definition, work);
                if (constants.Length > 256) Fail(work, "Enum.GetValues supports at most 256 constant entries per construction.", valuesCall.Offset, DiagnosticIds.TranslationLimit);
                _instructions += constants.Length * 3 + 2;
                if (_instructions > ProgramLimits.MaximumInstructions) Fail(work, "Enum values expanded instruction limit exceeded.", valuesCall.Offset, DiagnosticIds.TranslationLimit);
                var vector = Vector(element, work, token.Offset);
                result.Add(valuesCall.Offset, new(token.Offset, handleCall.Offset, vector,
                    [.. constants.OrderBy(c => unchecked((uint)c.Value)).Select(c => c.Value)]));
            }
            return result.ToImmutable();
        }
    }
}
