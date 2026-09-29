using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Cilica.Metadata.Resolution;

namespace Cilica.Metadata.Tests;

/// <summary>Small metadata-only assembly graphs, independent of installed reference packs.</summary>
internal sealed class ResolutionAssemblyBuilder
{
    private readonly MetadataBuilder _metadata = new();
    internal const TypeAttributes Forwarder = (TypeAttributes)0x00200000;

    internal ResolutionAssemblyBuilder(AssemblyIdentity identity, byte[]? publicKey = null, AssemblyFlags flags = 0)
    {
        _metadata.AddModule(0, _metadata.GetOrAddString(identity.Name + ".dll"),
            _metadata.GetOrAddGuid(new Guid("2b0b3f0a-bdea-4d77-9650-c5988d4ac20c")), default, default);
        _metadata.AddAssembly(_metadata.GetOrAddString(identity.Name), identity.Version,
            _metadata.GetOrAddString(identity.Culture == "neutral" ? "" : identity.Culture),
            publicKey is null ? default : _metadata.GetOrAddBlob(publicKey),
            publicKey is null ? flags : flags | AssemblyFlags.PublicKey, AssemblyHashAlgorithm.None);
        _metadata.AddTypeDefinition(0, default, _metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
    }

    internal AssemblyReferenceHandle Reference(AssemblyIdentity identity, byte[]? fullKey = null)
        => _metadata.AddAssemblyReference(_metadata.GetOrAddString(identity.Name), identity.Version,
            _metadata.GetOrAddString(identity.Culture == "neutral" ? "" : identity.Culture),
            fullKey is not null ? _metadata.GetOrAddBlob(fullKey)
                : identity.PublicKeyToken is null ? default : _metadata.GetOrAddBlob(Convert.FromHexString(identity.PublicKeyToken)),
            fullKey is null ? 0 : AssemblyFlags.PublicKey, default);

    internal TypeDefinitionHandle Type(string ns, string name, TypeDefinitionHandle parent = default)
    {
        TypeDefinitionHandle handle = _metadata.AddTypeDefinition(parent.IsNil ? TypeAttributes.Public : TypeAttributes.NestedPublic,
            _metadata.GetOrAddString(ns), _metadata.GetOrAddString(name), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        if (!parent.IsNil) _metadata.AddNestedType(handle, parent);
        return handle;
    }

    internal ExportedTypeHandle Forward(string ns, string name, AssemblyReferenceHandle target) =>
        _metadata.AddExportedType(TypeAttributes.Public | Forwarder,
            _metadata.GetOrAddString(ns), _metadata.GetOrAddString(name), target, 0);

    internal ExportedTypeHandle NestedExport(string name, ExportedTypeHandle parent) =>
        _metadata.AddExportedType(TypeAttributes.NestedPublic, default, _metadata.GetOrAddString(name), parent, 0);

    internal TypeReferenceHandle TypeReference(string ns, string name, EntityHandle scope) =>
        _metadata.AddTypeReference(scope, _metadata.GetOrAddString(ns), _metadata.GetOrAddString(name));

    internal void MarkReferenceAssembly(string scope = "System.Runtime")
    {
        var reference = Reference(new AssemblyIdentity(scope, new Version(10, 0, 0, 0)));
        var type = TypeReference("System.Runtime.CompilerServices", "ReferenceAssemblyAttribute", reference);
        var ctor = _metadata.AddMemberReference(type, _metadata.GetOrAddString(".ctor"),
            _metadata.GetOrAddBlob(new byte[] { 0x20, 0, 1 }));
        _metadata.AddCustomAttribute(MetadataTokens.EntityHandle(0x20000001), ctor,
            _metadata.GetOrAddBlob(new byte[] { 1, 0, 0, 0 }));
    }

    internal byte[] Build()
    {
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(_metadata),
            new BlobBuilder(), deterministicIdProvider: _ =>
                new BlobContentId(new Guid("2b0b3f0a-bdea-4d77-9650-c5988d4ac20c"), 1));
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        return blob.ToArray();
    }
}
