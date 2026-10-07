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
        private readonly HashSet<ResolvedType> _enumFormatting = [];
        private ImmutableArray<EnumConstantInput> EnumFormattingConstants(ResolvedType type, Work work)
        {
            if (_enumFormatting.Contains(type)) return _enumConstants[type];
            if (!_enumConstants.ContainsKey(type) && _enumConstants.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Enum formatting type limit exceeded.", code: DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadInt32EnumFormattingConstants(type, _metadata), work);
            _enumFormatting.Add(type); _enumConstants[type] = input.Constants;
            _enumMetadataRequired = true;
            return input.Constants;
        }
        private ValueIntrinsicCall? EnumTextCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "EnumText`1").Value) return null;
            if (_metadata.Remaining == 0) Fail(work, "Enum formatting metadata limit exceeded.", instruction.Offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, instruction.Offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Enum formatting metadata limit exceeded.", instruction.Offset, DiagnosticIds.TranslationLimit);
            string name = input.DeclaredName;
            if (name is not ("Bits" or "Count" or "ValueAt" or "NameAt")) return null;
            bool text = name == "NameAt", count = name == "Count", bits = name == "Bits";
            if (work.Assembly != library || instruction.OpCode != "call" || input.IsReferenceAssembly || input.HasBody
                || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 1
                || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Signature.ReturnType != new TypeSignature.Primitive(text ? PrimitiveTypeCode.String : PrimitiveTypeCode.Int32)
                || input.Signature.Parameters.Count != (count ? 0 : 1)
                || !count && input.Signature.Parameters[0] != new TypeSignature.Primitive(bits ? PrimitiveTypeCode.Object : PrimitiveTypeCode.Int32)
                || !EnumScalar(target.DeclaringType.Arguments[0], work, instruction.Offset))
                Fail(work, "Enum formatting metadata needs an exact private owned declaration and Int32 enum.", instruction.Offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.EnumFormatting), work, instruction.Offset);
            var receiver = FormattingReceiver(target.DeclaringType.Arguments[0], work, instruction.Offset);
            var constants = EnumFormattingConstants(receiver.Definition, work);
            _ = IncludeString(work, instruction.Offset);
            return new(bits ? ValueIntrinsic.EnumBits : count ? ValueIntrinsic.EnumCount : text ? ValueIntrinsic.EnumNameAt : ValueIntrinsic.EnumValueAt,
                Include(receiver, work, instruction.Offset), count ? 0 : 1) { ConstantValue = constants.Length };
        }
    }
}
