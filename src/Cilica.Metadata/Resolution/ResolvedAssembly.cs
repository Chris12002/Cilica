using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

/// <summary>
/// Immutable image information owned by an AssemblyResolver. Metadata handles are scoped
/// to this image. Internal readers must not outlive the owning resolver.
/// </summary>
public sealed class ResolvedAssembly
{
    internal ResolvedAssembly(AssemblyImage image, string contentHash)
    {
        Image = image;
        Identity = AssemblyIdentity.ReadDefinition(image.Reader);
        ContentHash = contentHash;
        References = [.. image.Reader.AssemblyReferences.Select(h => AssemblyIdentity.ReadReference(image.Reader, h))];
        var names = new NameFormatter(image.Reader);
        IsReferenceAssembly = image.Reader.GetAssemblyDefinition().GetCustomAttributes().Any(h =>
        {
            string name = names.AttributeTypeName(image.Reader.GetCustomAttribute(h).Constructor);
            // This is a conservative marker, not assembly authentication. A custom
            // scope using this marker must not make stub bodies appear executable.
            return name == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute"
                || name.EndsWith("]System.Runtime.CompilerServices.ReferenceAssemblyAttribute", StringComparison.Ordinal);
        });
    }

    public AssemblyIdentity Identity { get; }
    public string InputName => Image.InputName;
    public string ContentHash { get; }
    public bool IsReferenceAssembly { get; }
    public ImmutableArray<AssemblyIdentity> References { get; }
    internal AssemblyImage Image { get; }
    internal TypeIndex? Types { get; set; }
}

/// <summary>A type definition with its owning image; a raw token alone is not an identity.</summary>
public sealed record ResolvedType
{
    internal ResolvedType(ResolvedAssembly assembly, TypeDefinitionHandle handle)
    {
        Assembly = assembly;
        Handle = handle;
    }

    public ResolvedAssembly Assembly { get; }
    public TypeDefinitionHandle Handle { get; }

    // Identical snapshots can be opened through different paths. Reader ownership
    // stays session-specific, but semantic type identity must not depend on that path.
    public bool Equals(ResolvedType? other) => other is not null && Handle == other.Handle
        && Assembly.Identity.Equals(other.Assembly.Identity)
        && string.Equals(Assembly.ContentHash, other.Assembly.ContentHash, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Assembly.Identity, Assembly.ContentHash, Handle);
}

internal sealed class TypeIndex
{
    internal readonly Dictionary<(TypeDefinitionHandle Parent, string Namespace, string Name), List<TypeDefinitionHandle>> Definitions = [];
    internal readonly Dictionary<(string Namespace, string Name), List<ExportedTypeHandle>> Exports = [];

    internal TypeIndex(MetadataReader reader)
    {
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            TypeDefinitionHandle parent = type.GetDeclaringType();
            var key = (parent, parent.IsNil ? reader.GetString(type.Namespace) : "", reader.GetString(type.Name));
            if (!Definitions.TryGetValue(key, out var definitions)) Definitions.Add(key, definitions = []);
            definitions.Add(handle);
        }
        foreach (ExportedTypeHandle handle in reader.ExportedTypes)
        {
            ExportedType type = reader.GetExportedType(handle);
            // Nested exported types resolve through the outer forwarder and then the
            // implementation's nested definitions; their names are not top-level names.
            if (type.Implementation.Kind == HandleKind.ExportedType) continue;
            var key = (reader.GetString(type.Namespace), reader.GetString(type.Name));
            if (!Exports.TryGetValue(key, out var exports)) Exports.Add(key, exports = []);
            exports.Add(handle);
        }
    }
}
