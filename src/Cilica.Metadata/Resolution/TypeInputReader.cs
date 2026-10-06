using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>Reads one owned TypeDef row and charges the caller's metadata work ledger.</summary>
    public ResolutionResult<TypeDefinitionFlags> ReadTypeFlags(ResolvedType type, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(budget);
        return Bind(type.Assembly, type.Handle, () =>
        {
            InspectMember(budget);
            return new TypeDefinitionFlags(type.Assembly.Image.Reader.GetTypeDefinition(type.Handle).Attributes);
        });
    }

    private static bool PrimitiveLiteral(MetadataReader reader, FieldDefinitionHandle handle,
        FieldDefinition field, TypeSignature signature)
    {
        const FieldAttributes required = FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault;
        if ((field.Attributes & ~FieldAttributes.FieldAccessMask) != required
            || (field.Attributes & FieldAttributes.FieldAccessMask) is FieldAttributes.PrivateScope or > FieldAttributes.Public
            || field.GetDefaultValue().IsNil || signature is not TypeSignature.Primitive primitive)
            return false;
        var constant = reader.GetConstant(field.GetDefaultValue());
        if (constant.Parent != handle) return false;
        var (type, size) = primitive.Code switch {
            PrimitiveTypeCode.Boolean => (ConstantTypeCode.Boolean, 1),
            PrimitiveTypeCode.Char => (ConstantTypeCode.Char, 2),
            PrimitiveTypeCode.SByte => (ConstantTypeCode.SByte, 1),
            PrimitiveTypeCode.Byte => (ConstantTypeCode.Byte, 1),
            PrimitiveTypeCode.Int16 => (ConstantTypeCode.Int16, 2),
            PrimitiveTypeCode.UInt16 => (ConstantTypeCode.UInt16, 2),
            PrimitiveTypeCode.Int32 => (ConstantTypeCode.Int32, 4),
            PrimitiveTypeCode.UInt32 => (ConstantTypeCode.UInt32, 4),
            PrimitiveTypeCode.Int64 => (ConstantTypeCode.Int64, 8),
            PrimitiveTypeCode.UInt64 => (ConstantTypeCode.UInt64, 8),
            PrimitiveTypeCode.Single => (ConstantTypeCode.Single, 4),
            PrimitiveTypeCode.Double => (ConstantTypeCode.Double, 8),
            PrimitiveTypeCode.String when constant.TypeCode == ConstantTypeCode.String => (ConstantTypeCode.String, -1),
            PrimitiveTypeCode.Object or PrimitiveTypeCode.String => (ConstantTypeCode.NullReference, 4),
            _ => ((ConstantTypeCode)0, 0)
        };
        if (size == 0 || constant.TypeCode != type) return false;
        var blob = reader.GetBlobReader(constant.Value);
        if (size == -1) return blob.Length % 2 == 0;
        if (blob.Length != size) return false;
        return type switch {
            ConstantTypeCode.NullReference => blob.ReadUInt32() == 0,
            ConstantTypeCode.Boolean => blob.ReadByte() <= 1,
            _ => true
        };
    }

    /// <summary>Snapshots bounded structural metadata without loading or running the type.</summary>
    public ResolutionResult<TypeInput> ReadTypeInput(ResolvedAssembly assembly, TypeDefinitionHandle handle,
        TypeReadLimits? limits = null)
    {
        CheckOwner(assembly);
        limits ??= new();
        if (limits.MaximumFields < 0 || limits.MaximumMembers <= 0 || limits.MaximumOwnerDepth is <= 0 or > TypeReadLimits.MaximumSupportedOwnerDepth
            || limits.MaximumInterfaces is < 0 or > 256)
            throw new ArgumentOutOfRangeException(nameof(limits), "Type input limits require nonnegative fields, positive member budgets, owner depth 1..64 and direct interfaces 0..256.");
        return Bind<TypeInput>(assembly, handle, () =>
        {
            var reader = assembly.Image.Reader;
            var type = reader.GetTypeDefinition(handle);
            int work = 0, arity = 0;
            bool externallyVisible = true;
            void Member()
            {
                if (++work > limits.MaximumMembers)
                    throw Failure(DiagnosticIds.TranslationLimit, "Type input exceeds the metadata-member limit.");
            }
            var lexical = ImmutableArray.CreateBuilder<LexicalTypeOwner>();
            var owners = new HashSet<TypeDefinitionHandle>();
            for (var current = handle; !current.IsNil;)
            {
                if (!owners.Add(current)) throw Failure(DiagnosticIds.InvalidSignature, "Declaring-type chain is cyclic.");
                if (owners.Count > limits.MaximumOwnerDepth)
                    throw Failure(DiagnosticIds.TranslationLimit, "Declaring-type depth limit exceeded.");
                Member();
                var owner = reader.GetTypeDefinition(current);
                lexical.Add(new(MetadataTokens.GetToken(current), owner.Attributes & TypeAttributes.VisibilityMask, owner.GetGenericParameters().Count));
                // Nested declarations repeat inherited parameters; do not count them twice.
                // The maximum also retains the gate for under-declared nested owners.
                arity = Math.Max(arity, owner.GetGenericParameters().Count);
                current = owner.GetDeclaringType();
                externallyVisible &= (owner.Attributes & TypeAttributes.VisibilityMask) ==
                    (current.IsNil ? TypeAttributes.Public : TypeAttributes.NestedPublic);
            }
            if (type.GetFields().Count > limits.MaximumFields)
                throw Failure(DiagnosticIds.TranslationLimit, "Type input exceeds the field limit.");
            if (type.GetInterfaceImplementations().Count > limits.MaximumInterfaces)
                throw Failure(DiagnosticIds.TranslationLimit, "Type input exceeds the direct-interface limit.");
            var decoder = new SignatureDecoder(this, assembly);
            var fields = ImmutableArray.CreateBuilder<FieldInput>();
            foreach (var fieldHandle in type.GetFields())
            {
                Member();
                int token = MetadataTokens.GetToken(fieldHandle);
                string context = "Field token 0x" + token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + ": ";
                try
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if (field.GetDeclaringType() != handle)
                        throw Failure(DiagnosticIds.InvalidSignature, "Field is listed by a different declaring type.");
                    var signature = decoder.Field(field.Signature);
                    bool explicitStorage = field.GetOffset() != -1 || field.GetRelativeVirtualAddress() != 0
                        || !field.GetMarshallingDescriptor().IsNil;
                    var constant = field.GetDefaultValue();
                    if (!constant.IsNil) Member();
                    fields.Add(new(token, reader.GetString(field.Name), field.Attributes,
                        signature, explicitStorage || !constant.IsNil) {
                        IsMetadataConstant = !explicitStorage && PrimitiveLiteral(reader, fieldHandle, field, signature)
                    });
                }
                catch (BindingFailureException failure)
                {
                    throw new BindingFailureException(failure.Diagnostics.Select(d => d with { Message = context + d.Message }));
                }
                catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
                {
                    throw Failure(exception is UnsupportedBindingException ? DiagnosticIds.ResolutionUnsupported : DiagnosticIds.InvalidSignature,
                        context + exception.Message);
                }
            }
            bool Initializer(TypeDefinition definition)
            {
                Member();
                foreach (var methodHandle in definition.GetMethods())
                {
                    Member();
                    if (reader.StringComparer.Equals(reader.GetMethodDefinition(methodHandle).Name, ".cctor")) return true;
                }
                return false;
            }
            var layout = type.GetLayout();
            var source = new TypeSource(assembly.Identity.DisplayName, assembly.ContentHash, MetadataTokens.GetToken(handle),
                new NameFormatter(reader).TypeDefinitionName(handle));
            // The field signatures share a node budget; resolving the base must
            // not consume a slot from the exact 4096-field boundary.
            TypeSignature? baseType = type.BaseType.IsNil ? null : new SignatureDecoder(this, assembly).Type(type.BaseType);
            var interfaces = ImmutableArray.CreateBuilder<TypeSignature>();
            var interfaceDecoder = new SignatureDecoder(this, assembly);
            foreach (var implementation in type.GetInterfaceImplementations())
            {
                Member();
                interfaces.Add(interfaceDecoder.Type(reader.GetInterfaceImplementation(implementation).Interface));
            }
            bool typeInitializer = Initializer(type), moduleInitializer = Initializer(reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1)));
            bool byRefLike = TypeMarkers.IsByRefLike(new(assembly, handle), Member);
            return new TypeInput(source, type.Attributes, arity, assembly.IsReferenceAssembly, baseType,
                type.GetInterfaceImplementations().Count, layout.PackingSize, layout.Size,
                typeInitializer, moduleInitializer, fields.ToImmutable(), work)
                { LexicalOwners = lexical.ToImmutable(), Interfaces = interfaces.ToImmutable(), ExternallyVisible = externallyVisible, IsByRefLike = byRefLike };
        });
    }
}
