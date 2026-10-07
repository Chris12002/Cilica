using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
namespace Cilica.Metadata.Resolution;

public sealed record EnumConstantInput(int Token, string Name, int Value);
public sealed record EnumConstantsInput(ImmutableArray<EnumConstantInput> Constants)
{
    public const int MaximumConstants = 4096;
    public const int MaximumNameCodeUnits = 4096;
    public const int MaximumTotalNameCodeUnits = 65536;
}

public sealed partial class AssemblyResolver
{
    /// <summary>Formatting initially excludes custom attributes and duplicate-value aliases.</summary>
    public ResolutionResult<EnumConstantsInput> ReadInt32EnumFormattingConstants(ResolvedType type, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(budget);
        var attributes = Bind(type.Assembly, type.Handle, () => {
            InspectMember(budget);
            if (type.Assembly.Image.Reader.GetTypeDefinition(type.Handle).GetCustomAttributes().Count != 0)
                throw Failure(DiagnosticIds.ExecutionUnsupported, "Enum formatting with custom attributes, including FlagsAttribute, requires later name-selection support.");
            return new EnumConstantsInput([]);
        });
        if (!attributes.Success) return attributes;
        var constants = ReadInt32EnumConstants(type, budget);
        if (!constants.Success) return constants;
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var value in constants.Value!.Constants)
            if (!seen.Add(value.Value)) return ResolutionResult<EnumConstantsInput>.Failed(DiagnosticIds.ExecutionUnsupported,
                "Enum formatting with duplicate-value aliases requires later name-selection support.");
        return constants;
    }

    /// <summary>Reads bounded Int32 constant rows; selected Enum shape is validated by the compiler.</summary>
    public ResolutionResult<EnumConstantsInput> ReadInt32EnumConstants(ResolvedType type, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(budget);
        return Bind(type.Assembly, type.Handle, () => {
            var reader = type.Assembly.Image.Reader;
            InspectMember(budget);
            var owner = reader.GetTypeDefinition(type.Handle);
            if (owner.GetFields().Count > EnumConstantsInput.MaximumConstants) throw Failure(DiagnosticIds.TranslationLimit, "Enum constant field limit exceeded.");
            var result = ImmutableArray.CreateBuilder<EnumConstantInput>(); int units = 0;
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var decoder = new SignatureDecoder(this, type.Assembly);
            foreach (var handle in owner.GetFields())
            {
                InspectMember(budget); var field = reader.GetFieldDefinition(handle);
                if ((field.Attributes & FieldAttributes.Static) == 0) continue;
                string name = reader.GetString(field.Name);
                if (name.Length is 0 or > EnumConstantsInput.MaximumNameCodeUnits || (units += name.Length) > EnumConstantsInput.MaximumTotalNameCodeUnits)
                    throw Failure(DiagnosticIds.TranslationLimit, "Enum constant name limit exceeded.");
                if (!names.Add(name) || field.GetDeclaringType() != type.Handle
                    || field.Attributes != (FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault)
                    || field.GetOffset() != -1 || field.GetRelativeVirtualAddress() != 0 || !field.GetMarshallingDescriptor().IsNil
                    || decoder.Field(field.Signature) is not TypeSignature.Named literal || literal.Definition != type
                    || field.GetDefaultValue().IsNil)
                    throw Failure(DiagnosticIds.ExecutionUnsupported, "Enum constants require exact literal declarations with unique bounded names.");
                InspectMember(budget); var constant = reader.GetConstant(field.GetDefaultValue());
                var blob = reader.GetBlobReader(constant.Value);
                if (constant.Parent != handle || constant.TypeCode != ConstantTypeCode.Int32 || blob.Length != 4)
                    throw Failure(DiagnosticIds.ExecutionUnsupported, "Enum constants require exact Int32 constant rows.");
                result.Add(new(MetadataTokens.GetToken(handle), name, blob.ReadInt32()));
            }
            return new EnumConstantsInput(result.ToImmutable());
        });
    }
}
