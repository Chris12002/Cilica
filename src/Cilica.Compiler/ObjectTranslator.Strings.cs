using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ObjectTranslator
{
    private enum StringIntrinsic { None, Length, CodeUnit, Equal, NotEqual, Concat }

    private sealed partial class Translation
    {
        private readonly Dictionary<(IrMethodId Method, int Offset), ImmutableArray<ushort>> _literalSites = [];
        private readonly HashSet<string> _literalContents = new(StringComparer.Ordinal);
        private readonly Dictionary<IrMethodId, MethodDeclarationInput> _stringDeclarations = [];
        private int _literalCodeUnits;

        private ObjectTypeId IncludeString(Work? caller, int offset)
        {
            try { return Include(_strings.Require()); }
            catch (ObjectStringType.Failure failure)
            { throw new Failure(failure.Diagnostic with { Location = Location(caller, offset), Message = failure.Diagnostic.Message + Path(caller, offset) }); }
        }

        private ImmutableArray<ushort> Literal(Work caller, MethodInstruction instruction)
        {
            var key = (caller.Binding.Method, instruction.Offset);
            if (_literalSites.TryGetValue(key, out var cached)) return cached;
            var read = resolver.ReadUserString(caller.Resolved.DeclaringType.Definition.Assembly, instruction.IntegerOperand ?? 0,
                Math.Min(ObjectStringLimits.MaximumLiteralCodeUnits, ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalCodeUnits));
            if (read.Value is not { } literal)
                throw new Failure(read.Diagnostics[0] with { Location = Location(caller, instruction.Offset), Message = read.Diagnostics[0].Message + Path(caller, instruction.Offset) });
            _literalCodeUnits += literal.CodeUnits.Length;
            string content = string.Create(literal.CodeUnits.Length, literal.CodeUnits, static (target, source) =>
            {
                for (int i = 0; i < source.Length; i++) target[i] = (char)source[i];
            });
            if (_literalContents.Add(content) && _literalContents.Count > ObjectStringLimits.MaximumDistinctLiterals)
                Fail(caller, "Distinct string literal limit exceeded.", instruction.Offset, DiagnosticIds.TranslationLimit);
            _literalSites.Add(key, literal.CodeUnits); return literal.CodeUnits;
        }

        private CallTarget StringCall(Work caller, MethodInstruction instruction, ResolvedMethod target)
        {
            var text = IncludeString(caller, instruction.Offset); var textType = IrType.Reference(text);
            var owner = target.DeclaringType.Definition;
            var id = new IrMethodId(owner.Assembly.ContentHash, MetadataTokens.GetToken(target.Handle));
            if (!_stringDeclarations.TryGetValue(id, out var input))
            {
                if (_methods.Count + _interfaces.Count + _stringDeclarations.Count >= ProgramLimits.MaximumMethods)
                    Fail(caller, "Executable methods, interface declarations and intrinsics share the method limit.", instruction.Offset, DiagnosticIds.TranslationLimit);
                if (_metadata.Remaining == 0) Fail(caller, "String intrinsic metadata budget exhausted.", instruction.Offset, DiagnosticIds.TranslationLimit);
                var read = resolver.ReadMethodDeclarationInput(owner.Assembly, target.Handle, new(MaximumMembers: _metadata.Remaining));
                if (read.Value is not { } declaration)
                    throw new Failure(read.Diagnostics[0] with { Location = Location(caller, instruction.Offset), Message = read.Diagnostics[0].Message + Path(caller, instruction.Offset) });
                input = declaration;
                if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(caller, "String intrinsic metadata budget exhausted.", instruction.Offset, DiagnosticIds.TranslationLimit);
                _stringDeclarations.Add(id, input);
            }
            const MethodAttributes allowed = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
            if (target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0 || input.OwnerGenericArity != 0
                || input.Signature.GenericArity != 0 || !input.OwnerExternallyVisible || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer
                || (input.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public || (input.Attributes & ~allowed) != 0
                || (input.Implementation & MethodImplAttributes.CodeTypeMask) is not (MethodImplAttributes.IL or MethodImplAttributes.Runtime)
                || (input.Implementation & (MethodImplAttributes.ManagedMask | MethodImplAttributes.Synchronized)) != 0)
                Fail(caller, "String intrinsic declaration has unsupported owner, generic, access or implementation flags.", instruction.Offset);
            bool Primitive(TypeSignature type, PrimitiveTypeCode code) => type is TypeSignature.Primitive p && p.Code == code;
            bool Signature(bool instance, PrimitiveTypeCode returned, params PrimitiveTypeCode[] parameters) =>
                input.Signature.Header == (instance ? 0x20 : 0)
                && ((input.Attributes & MethodAttributes.Static) == 0) == instance
                && Primitive(input.Signature.ReturnType, returned) && input.Signature.Parameters.Count == parameters.Length
                && input.Signature.Parameters.Select((p, i) => Primitive(p, parameters[i])).All(p => p);
            var intrinsic = input.DeclaredName switch
            {
                "get_Length" when Signature(true, PrimitiveTypeCode.Int32) => StringIntrinsic.Length,
                "get_Chars" when Signature(true, PrimitiveTypeCode.Char, PrimitiveTypeCode.Int32) => StringIntrinsic.CodeUnit,
                "op_Equality" or "Equals" when Signature(false, PrimitiveTypeCode.Boolean, PrimitiveTypeCode.String, PrimitiveTypeCode.String) => StringIntrinsic.Equal,
                "op_Inequality" when Signature(false, PrimitiveTypeCode.Boolean, PrimitiveTypeCode.String, PrimitiveTypeCode.String) => StringIntrinsic.NotEqual,
                "Concat" when Signature(false, PrimitiveTypeCode.String, PrimitiveTypeCode.String, PrimitiveTypeCode.String) => StringIntrinsic.Concat,
                _ => StringIntrinsic.None,
            };
            bool receiver = intrinsic is StringIntrinsic.Length or StringIntrinsic.CodeUnit;
            if (intrinsic == StringIntrinsic.None || instruction.OpCode != (receiver ? "callvirt" : "call"))
                Fail(caller, "Unsupported String member or call form: " + input.DeclaredName
                    + ". Supported: callvirt Length/Chars; call Equals(string,string), op_Equality, op_Inequality and Concat(string,string).", instruction.Offset);
            var capability = intrinsic switch
            {
                StringIntrinsic.Length => LibraryIntrinsicCapability.StringLength,
                StringIntrinsic.CodeUnit => LibraryIntrinsicCapability.StringCodeUnit,
                StringIntrinsic.Equal => LibraryIntrinsicCapability.StringEqual,
                StringIntrinsic.NotEqual => LibraryIntrinsicCapability.StringNotEqual,
                StringIntrinsic.Concat => LibraryIntrinsicCapability.StringConcat,
                _ => throw new InvalidOperationException("Unvalidated string intrinsic."),
            };
            var permission = resolver.AuthorizeLibraryIntrinsic(owner.Assembly, target.Handle, capability);
            if (!permission.Success) throw new Failure(permission.Diagnostics[0] with { Location = Location(caller, instruction.Offset) });
            ImmutableArray<IrType> parameters = intrinsic switch
            {
                StringIntrinsic.Length => [textType], StringIntrinsic.CodeUnit => [textType, IrType.Int32], _ => [textType, textType],
            };
            return new(id, text, parameters, intrinsic == StringIntrinsic.Concat ? textType : IrType.Int32, false, intrinsic);
        }
    }
}
