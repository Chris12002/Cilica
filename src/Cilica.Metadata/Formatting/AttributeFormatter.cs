using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Formatting;

/// <summary>
/// Formats custom attributes with decoded arguments. Decoding an enum argument
/// needs the enum's underlying type. Enums defined in the inspected module are
/// resolved here; others need dependency resolution (milestone slice 1c), so
/// those attributes show their raw blob instead.
/// </summary>
internal sealed class AttributeFormatter : ICustomAttributeTypeProvider<AttributeFormatter.ArgumentType>
{
    private readonly MetadataReader _reader;
    private readonly NameFormatter _names;
    private Dictionary<string, TypeDefinitionHandle>? _definitionsBySerializedName;

    public AttributeFormatter(MetadataReader reader, NameFormatter names)
    {
        _reader = reader;
        _names = names;
    }

    internal sealed record ArgumentType(
        string Name,
        EntityHandle Handle = default,
        PrimitiveTypeCode? Primitive = null,
        bool IsSystemType = false,
        bool IsNullType = false);

    private sealed class UnresolvedEnumException : Exception;

    public string Format(CustomAttributeHandle handle, string prefix = "")
    {
        CustomAttribute attribute = _reader.GetCustomAttribute(handle);
        string type = _names.AttributeTypeName(attribute.Constructor);
        string arguments;
        try
        {
            CustomAttributeValue<ArgumentType> value = attribute.DecodeValue(this);
            IEnumerable<string> fixedArguments = value.FixedArguments.Select(FormatArgument);
            IEnumerable<string> namedArguments = value.NamedArguments.Select(
                argument => argument.Name + " = " + FormatArgument(new(argument.Type, argument.Value)));
            arguments = string.Join(", ", fixedArguments.Concat(namedArguments));
        }
        catch (UnresolvedEnumException)
        {
            arguments = "undecoded blob " + Invariant.Hex(_reader.GetBlobBytes(attribute.Value));
        }
        return "[" + prefix + type + (arguments.Length == 0 ? "" : "(" + arguments + ")") + "]";
    }

    private static string FormatArgument(CustomAttributeTypedArgument<ArgumentType> argument)
    {
        object? value = argument.Value;
        switch (value)
        {
            case null:
                return "null";
            case ImmutableArray<CustomAttributeTypedArgument<ArgumentType>> elements:
                return "[" + string.Join(", ", elements.Select(FormatArgument)) + "]";
            case ArgumentType type:
                return type.IsNullType ? "null" : "typeof(" + type.Name + ")";
        }
        bool isEnum = argument.Type.Primitive is null && !argument.Type.IsSystemType && value is not string;
        return isEnum
            ? argument.Type.Name + "(" + Invariant.Literal(value) + ")"
            : Invariant.Literal(value);
    }

    // ICustomAttributeTypeProvider

    public ArgumentType GetPrimitiveType(PrimitiveTypeCode typeCode) =>
        new(NameFormatter.PrimitiveName(typeCode), Primitive: typeCode);

    public ArgumentType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        // Core/reference libraries can define System.Type locally instead of
        // referring to it. This controls attribute decoding, never compiler trust.
        TypeDefinition definition = reader.GetTypeDefinition(handle);
        bool isSystemType = definition.GetDeclaringType().IsNil
            && reader.StringComparer.Equals(definition.Namespace, "System")
            && reader.StringComparer.Equals(definition.Name, "Type");
        return new(_names.TypeDefinitionName(handle), handle, IsSystemType: isSystemType);
    }

    public ArgumentType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        TypeReference reference = reader.GetTypeReference(handle);
        bool isSystemType = reader.StringComparer.Equals(reference.Namespace, "System")
            && reader.StringComparer.Equals(reference.Name, "Type");
        return new(_names.TypeReferenceName(handle), handle, IsSystemType: isSystemType);
    }

    public ArgumentType GetSZArrayType(ArgumentType elementType) => new(elementType.Name + "[]");

    public ArgumentType GetSystemType() => new("System.Type", IsSystemType: true);

    public bool IsSystemType(ArgumentType type) => type.IsSystemType;

    public ArgumentType GetTypeFromSerializedName(string name) =>
        name is null ? new("", IsNullType: true) : new(name);

    public PrimitiveTypeCode GetUnderlyingEnumType(ArgumentType type)
    {
        TypeDefinitionHandle definition = type.Handle.Kind == HandleKind.TypeDefinition
            ? (TypeDefinitionHandle)type.Handle
            : FindDefinitionBySerializedName(type.Name);
        if (definition.IsNil)
        {
            throw new UnresolvedEnumException();
        }
        foreach (FieldDefinitionHandle fieldHandle in _reader.GetTypeDefinition(definition).GetFields())
        {
            FieldDefinition field = _reader.GetFieldDefinition(fieldHandle);
            if ((field.Attributes & System.Reflection.FieldAttributes.Static) == 0)
            {
                return field.DecodeSignature(new UnderlyingTypeProvider(), null);
            }
        }
        throw new BadImageFormatException($"Enum type {type.Name} has no instance field for its underlying value.");
    }

    private TypeDefinitionHandle FindDefinitionBySerializedName(string serializedName)
    {
        _definitionsBySerializedName ??= BuildSerializedNameIndex();
        // A serialized name may be assembly-qualified; only local definitions can be resolved here.
        int comma = serializedName.IndexOf(',', StringComparison.Ordinal);
        string typeName = comma < 0 ? serializedName : serializedName[..comma].Trim();
        return _definitionsBySerializedName.GetValueOrDefault(typeName);
    }

    private Dictionary<string, TypeDefinitionHandle> BuildSerializedNameIndex()
    {
        var index = new Dictionary<string, TypeDefinitionHandle>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle handle in _reader.TypeDefinitions)
        {
            // Serialized names separate nested types with '+'.
            index.TryAdd(_names.TypeDefinitionName(handle).Replace('/', '+'), handle);
        }
        return index;
    }

    /// <summary>Decodes an enum's value field, which must have a primitive integral type.</summary>
    private sealed class UnderlyingTypeProvider : ISignatureTypeProvider<PrimitiveTypeCode, object?>
    {
        public PrimitiveTypeCode GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode;

        private static PrimitiveTypeCode Invalid() =>
            throw new BadImageFormatException("An enum's underlying type must be a primitive integral type.");

        public PrimitiveTypeCode GetArrayType(PrimitiveTypeCode elementType, ArrayShape shape) => Invalid();
        public PrimitiveTypeCode GetByReferenceType(PrimitiveTypeCode elementType) => Invalid();
        public PrimitiveTypeCode GetFunctionPointerType(MethodSignature<PrimitiveTypeCode> signature) => Invalid();
        public PrimitiveTypeCode GetGenericInstantiation(PrimitiveTypeCode genericType, ImmutableArray<PrimitiveTypeCode> typeArguments) => Invalid();
        public PrimitiveTypeCode GetGenericMethodParameter(object? genericContext, int index) => Invalid();
        public PrimitiveTypeCode GetGenericTypeParameter(object? genericContext, int index) => Invalid();
        public PrimitiveTypeCode GetModifiedType(PrimitiveTypeCode modifier, PrimitiveTypeCode unmodifiedType, bool isRequired) => unmodifiedType;
        public PrimitiveTypeCode GetPinnedType(PrimitiveTypeCode elementType) => Invalid();
        public PrimitiveTypeCode GetPointerType(PrimitiveTypeCode elementType) => Invalid();
        public PrimitiveTypeCode GetSZArrayType(PrimitiveTypeCode elementType) => Invalid();
        public PrimitiveTypeCode GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => Invalid();
        public PrimitiveTypeCode GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => Invalid();
        public PrimitiveTypeCode GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => Invalid();
    }
}
