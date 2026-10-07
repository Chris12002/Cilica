using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ExceptionMessageConstruction? TryExceptionMessageConstruction(ResolvedMethod constructor, Work work, int offset, MethodInstruction[] instructions)
        {
            if (instructions.Length != 7 || instructions[0].OpCode != "ldarg.0" || instructions[1].OpCode != "ldstr"
                || instructions[2].OpCode != "ldarg.1" || instructions[3].OpCode != "ldstr"
                || instructions[4].OpCode != "call" || instructions[5].OpCode != "call" || instructions[6].OpCode != "ret") return null;
            var concat = Require(resolver.ResolveClosedCall(constructor, MetadataTokens.EntityHandle(instructions[4].IntegerOperand ?? 0), _metadata), work, offset);
            var text = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String), work, offset);
            if (concat.DeclaringType.Definition != text || concat.DeclaringType.Arguments.Count != 0 || concat.MethodArguments.Count != 0) return null;
            var declaration = ExceptionDeclaration(concat, work, offset);
            if (declaration.DeclaredName != "Concat" || !declaration.HasBody || declaration.Implementation != MethodImplAttributes.IL
                || declaration.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || declaration.Signature.Header != 0 || declaration.Signature.GenericArity != 0
                || declaration.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || declaration.Signature.Parameters.Count != 3 || declaration.Signature.Parameters.Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String })) return null;
            var parent = Snapshot(constructor.DeclaringType.Definition, work, offset).BaseType as TypeSignature.Named;
            var baseCall = Require(resolver.ResolveClosedCall(constructor, MetadataTokens.EntityHandle(instructions[5].IntegerOperand ?? 0), _metadata), work, offset);
            var baseId = ClosedId(baseCall.DeclaringType, work, offset);
            if (parent is null || parent.Definition != baseCall.DeclaringType.Definition
                || !(baseId == _exceptionBase || _exceptionResources.Any(r => r.Type == baseId && r.Kind == ExceptionResourceKind.InvalidOperation))
                || baseCall.Signature.Parameters.Count != 1) return null;
            ValidateExceptionConstructor(baseCall, work, offset, 1);
            ImmutableArray<ushort> Literal(int index)
            {
                if (_literalUnits >= ObjectStringLimits.MaximumTotalLiteralCodeUnits) Fail(work, "Exception message literal budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                var literal = Require(resolver.ReadUserString(constructor.DeclaringType.Definition.Assembly, instructions[index].IntegerOperand ?? 0,
                    System.Math.Min(ObjectStringLimits.MaximumLiteralCodeUnits, ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits)), work, offset);
                _literalUnits += literal.CodeUnits.Length; return literal.CodeUnits;
            }
            var prefix = Literal(1); var suffix = Literal(3);
            var helper = Discover(concat, work, offset);
            var storage = IrType.Reference(IncludeString(work, offset));
            if (helper.Binding.Kind != ObjectMethodKind.Static || helper.ReturnType != storage
                || helper.Parameters.Length != 3 || helper.Parameters.Any(p => p != storage))
                Fail(work, "Exception message concatenation requires the selected three-string managed helper.", offset);
            return new(helper.Binding.Method, prefix, suffix);
        }
    }
}
