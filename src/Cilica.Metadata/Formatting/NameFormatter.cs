using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;

namespace Cilica.Metadata.Formatting;

/// <summary>
/// Formats metadata entities and signatures in ILASM-like notation.
/// Types defined in the inspected module have no scope prefix; referenced
/// types carry their resolution scope, for example <c>[netstandard]System.String</c>.
/// </summary>
internal sealed class NameFormatter : ISignatureTypeProvider<string, GenericContext>
{
    // Valid metadata never nests this deeply; malformed metadata can contain cycles.
    private const int MaximumDepth = 64;

    private readonly MetadataReader _reader;
    private int _depth;

    public NameFormatter(MetadataReader reader)
    {
        _reader = reader;
    }

    public string TypeDefinitionName(TypeDefinitionHandle handle)
    {
        using var guard = Enter();
        TypeDefinition definition = _reader.GetTypeDefinition(handle);
        string name = _reader.GetString(definition.Name);
        TypeDefinitionHandle declaring = definition.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return TypeDefinitionName(declaring) + "/" + name;
        }
        return Qualify(_reader.GetString(definition.Namespace), name);
    }

    public string TypeReferenceName(TypeReferenceHandle handle)
    {
        using var guard = Enter();
        TypeReference reference = _reader.GetTypeReference(handle);
        string name = _reader.GetString(reference.Name);
        EntityHandle scope = reference.ResolutionScope;
        switch (scope.Kind)
        {
            case HandleKind.TypeReference:
                return TypeReferenceName((TypeReferenceHandle)scope) + "/" + name;
            case HandleKind.AssemblyReference:
                string assembly = _reader.GetString(_reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
                return "[" + assembly + "]" + Qualify(_reader.GetString(reference.Namespace), name);
            case HandleKind.ModuleReference:
                string module = _reader.GetString(_reader.GetModuleReference((ModuleReferenceHandle)scope).Name);
                return "[.module " + module + "]" + Qualify(_reader.GetString(reference.Namespace), name);
            default:
                // Nil scope (resolved through exported types) or the current module.
                return Qualify(_reader.GetString(reference.Namespace), name);
        }
    }

    public string ExportedTypeName(ExportedTypeHandle handle)
    {
        using var guard = Enter();
        ExportedType exported = _reader.GetExportedType(handle);
        string name = _reader.GetString(exported.Name);
        if (exported.Implementation.Kind == HandleKind.ExportedType)
        {
            return ExportedTypeName((ExportedTypeHandle)exported.Implementation) + "/" + name;
        }
        return Qualify(_reader.GetString(exported.Namespace), name);
    }

    /// <summary>Formats a TypeDef, TypeRef or TypeSpec handle.</summary>
    public string TypeName(EntityHandle handle, GenericContext context) => handle.Kind switch
    {
        HandleKind.TypeDefinition => TypeDefinitionName((TypeDefinitionHandle)handle),
        HandleKind.TypeReference => TypeReferenceName((TypeReferenceHandle)handle),
        HandleKind.TypeSpecification => GetTypeFromSpecification(_reader, context, (TypeSpecificationHandle)handle, 0),
        _ => throw new BadImageFormatException($"Expected a type handle but found {handle.Kind}."),
    };

    /// <summary>Formats a method handle as <c>Type::Name(parameter types)</c>.</summary>
    public string MethodName(EntityHandle handle) => MethodName(handle, GenericContext.Empty);

    private string MethodName(EntityHandle handle, GenericContext context)
    {
        using var guard = Enter();
        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
            {
                MethodDefinition method = _reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                TypeDefinitionHandle declaring = method.GetDeclaringType();
                var methodContext = new GenericContext(
                    GenericParameterNames(_reader.GetTypeDefinition(declaring).GetGenericParameters()),
                    GenericParameterNames(method.GetGenericParameters()));
                MethodSignature<string> signature = method.DecodeSignature(this, methodContext);
                return TypeDefinitionName(declaring) + "::" + _reader.GetString(method.Name) + ParameterTypes(signature);
            }
            case HandleKind.MemberReference:
            {
                MemberReference member = _reader.GetMemberReference((MemberReferenceHandle)handle);
                string parent = MemberParentName(member.Parent);
                string name = _reader.GetString(member.Name);
                if (member.GetKind() != MemberReferenceKind.Method)
                {
                    throw new BadImageFormatException("Expected a method reference but found a field reference.");
                }
                return parent + "::" + name + ParameterTypes(member.DecodeMethodSignature(this, context));
            }
            case HandleKind.MethodSpecification:
            {
                MethodSpecification specification = _reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                ImmutableArray<string> arguments = specification.DecodeSignature(this, context);
                return MethodName(specification.Method, context) + "<" + string.Join(", ", arguments) + ">";
            }
            default:
                throw new BadImageFormatException($"Expected a method handle but found {handle.Kind}.");
        }
    }

    /// <summary>The declaring type of an attribute constructor.</summary>
    public string AttributeTypeName(EntityHandle constructor)
    {
        switch (constructor.Kind)
        {
            case HandleKind.MethodDefinition:
                return TypeDefinitionName(_reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType());
            case HandleKind.MemberReference:
                return MemberParentName(_reader.GetMemberReference((MemberReferenceHandle)constructor).Parent);
            default:
                throw new BadImageFormatException($"Unexpected attribute constructor handle {constructor.Kind}.");
        }
    }

    private string MemberParentName(EntityHandle parent) => parent.Kind switch
    {
        HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification =>
            TypeName(parent, GenericContext.Empty),
        HandleKind.ModuleReference =>
            "[.module " + _reader.GetString(_reader.GetModuleReference((ModuleReferenceHandle)parent).Name) + "]",
        HandleKind.MethodDefinition => MethodName(parent),
        _ => throw new BadImageFormatException($"Unexpected member reference parent {parent.Kind}."),
    };

    public ImmutableArray<string> GenericParameterNames(GenericParameterHandleCollection parameters) =>
        [.. parameters.Select(handle => _reader.GetString(_reader.GetGenericParameter(handle).Name))];

    public static string ParameterTypes(MethodSignature<string> signature) =>
        "(" + string.Join(", ", signature.ParameterTypes) + ")";

    public static string CallingConvention(SignatureHeader header)
    {
        var builder = new StringBuilder();
        if (header.IsInstance)
        {
            builder.Append("instance ");
        }
        if (header.HasExplicitThis)
        {
            builder.Append("explicit ");
        }
        builder.Append(header.CallingConvention switch
        {
            SignatureCallingConvention.Default => "",
            SignatureCallingConvention.CDecl => "unmanaged cdecl ",
            SignatureCallingConvention.StdCall => "unmanaged stdcall ",
            SignatureCallingConvention.ThisCall => "unmanaged thiscall ",
            SignatureCallingConvention.FastCall => "unmanaged fastcall ",
            SignatureCallingConvention.VarArgs => "vararg ",
            SignatureCallingConvention.Unmanaged => "unmanaged ",
            _ => "callconv(" + Invariant.Format((int)header.CallingConvention) + ") ",
        });
        return builder.ToString();
    }

    private static string Qualify(string @namespace, string name) =>
        @namespace.Length == 0 ? name : @namespace + "." + name;

    private DepthGuard Enter()
    {
        if (++_depth > MaximumDepth)
        {
            _depth--;
            throw new BadImageFormatException("Type or signature nesting is too deep or cyclic.");
        }
        return new DepthGuard(this);
    }

    private readonly struct DepthGuard(NameFormatter owner) : IDisposable
    {
        public void Dispose() => owner._depth--;
    }

    // ISignatureTypeProvider

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => PrimitiveName(typeCode);

    public static string PrimitiveName(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "bool",
        PrimitiveTypeCode.Byte => "uint8",
        PrimitiveTypeCode.Char => "char",
        PrimitiveTypeCode.Double => "float64",
        PrimitiveTypeCode.Int16 => "int16",
        PrimitiveTypeCode.Int32 => "int32",
        PrimitiveTypeCode.Int64 => "int64",
        PrimitiveTypeCode.IntPtr => "native int",
        PrimitiveTypeCode.Object => "object",
        PrimitiveTypeCode.SByte => "int8",
        PrimitiveTypeCode.Single => "float32",
        PrimitiveTypeCode.String => "string",
        PrimitiveTypeCode.TypedReference => "typedref",
        PrimitiveTypeCode.UInt16 => "uint16",
        PrimitiveTypeCode.UInt32 => "uint32",
        PrimitiveTypeCode.UInt64 => "uint64",
        PrimitiveTypeCode.UIntPtr => "native uint",
        PrimitiveTypeCode.Void => "void",
        _ => throw new BadImageFormatException($"Unknown primitive type code {(int)typeCode}."),
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
        TypeDefinitionName(handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
        TypeReferenceName(handle);

    public string GetTypeFromSpecification(MetadataReader reader, GenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        using var guard = Enter();
        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape)
    {
        var dimensions = new string[shape.Rank];
        for (int index = 0; index < shape.Rank; index++)
        {
            bool hasLower = index < shape.LowerBounds.Length;
            bool hasSize = index < shape.Sizes.Length;
            int lower = hasLower ? shape.LowerBounds[index] : 0;
            dimensions[index] = (hasLower, hasSize) switch
            {
                (_, true) => Invariant.Format(lower) + "..." + Invariant.Format(lower + shape.Sizes[index] - 1),
                (true, false) => Invariant.Format(lower) + "...",
                _ => "",
            };
        }
        return elementType + "[" + string.Join(",", dimensions) + "]";
    }

    public string GetByReferenceType(string elementType) => elementType + "&";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetPinnedType(string elementType) => elementType + " pinned";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        genericType + "<" + string.Join(", ", typeArguments) + ">";

    public string GetGenericTypeParameter(GenericContext genericContext, int index) => genericContext.TypeParameter(index);

    public string GetGenericMethodParameter(GenericContext genericContext, int index) => genericContext.MethodParameter(index);

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
        unmodifiedType + (isRequired ? " modreq(" : " modopt(") + modifier + ")";

    public string GetFunctionPointerType(MethodSignature<string> signature) =>
        "method " + CallingConvention(signature.Header) + signature.ReturnType + " *" + ParameterTypes(signature);
}
