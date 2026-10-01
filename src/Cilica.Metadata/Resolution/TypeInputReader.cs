using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>Snapshots bounded structural metadata without loading or running the type.</summary>
    public ResolutionResult<TypeInput> ReadTypeInput(ResolvedAssembly assembly, TypeDefinitionHandle handle,
        TypeReadLimits? limits = null)
    {
        CheckOwner(assembly);
        limits ??= new();
        if (limits.MaximumFields < 0 || limits.MaximumMembers <= 0 || limits.MaximumOwnerDepth is <= 0 or > TypeReadLimits.MaximumSupportedOwnerDepth)
            throw new ArgumentOutOfRangeException(nameof(limits), "Type input limits require nonnegative fields, positive member budgets and owner depth 1..64.");
        return Bind<TypeInput>(assembly, handle, () =>
        {
            var reader = assembly.Image.Reader;
            var type = reader.GetTypeDefinition(handle);
            int work = 0, arity = 0;
            void Member()
            {
                if (++work > limits.MaximumMembers)
                    throw Failure(DiagnosticIds.TranslationLimit, "Type input exceeds the metadata-member limit.");
            }
            var owners = new HashSet<TypeDefinitionHandle>();
            for (var current = handle; !current.IsNil;)
            {
                if (!owners.Add(current)) throw Failure(DiagnosticIds.InvalidSignature, "Declaring-type chain is cyclic.");
                if (owners.Count > limits.MaximumOwnerDepth)
                    throw Failure(DiagnosticIds.TranslationLimit, "Declaring-type depth limit exceeded.");
                Member();
                var owner = reader.GetTypeDefinition(current);
                arity = checked(arity + owner.GetGenericParameters().Count);
                current = owner.GetDeclaringType();
            }
            if (type.GetFields().Count > limits.MaximumFields)
                throw Failure(DiagnosticIds.TranslationLimit, "Type input exceeds the field limit.");
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
                    fields.Add(new(token, reader.GetString(field.Name), field.Attributes,
                        decoder.Field(field.Signature), field.GetOffset() != -1 || field.GetRelativeVirtualAddress() != 0
                            || !field.GetMarshallingDescriptor().IsNil || !field.GetDefaultValue().IsNil));
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
            bool typeInitializer = Initializer(type), moduleInitializer = Initializer(reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1)));
            return new(source, type.Attributes, arity, assembly.IsReferenceAssembly, baseType,
                type.GetInterfaceImplementations().Count, layout.PackingSize, layout.Size,
                typeInitializer, moduleInitializer, fields.ToImmutable(), work);
        });
    }
}
