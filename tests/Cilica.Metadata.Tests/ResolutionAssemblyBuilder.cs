using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly MethodBodyStreamEncoder _bodies = new(new BlobBuilder());
    private readonly List<(EntityHandle Owner, int Count, GenericParameterAttributes Attributes)> _parameters = [];
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

    internal TypeDefinitionHandle Type(string ns, string name, TypeDefinitionHandle parent = default,
        EntityHandle baseType = default, int arity = 0, TypeAttributes? attributes = null,
        GenericParameterAttributes genericAttributes = GenericParameterAttributes.None)
    {
        TypeDefinitionHandle handle = _metadata.AddTypeDefinition(attributes ?? (parent.IsNil ? TypeAttributes.Public : TypeAttributes.NestedPublic),
            _metadata.GetOrAddString(ns), _metadata.GetOrAddString(name), baseType,
            MetadataTokens.FieldDefinitionHandle(_metadata.GetRowCount(TableIndex.Field) + 1),
            MetadataTokens.MethodDefinitionHandle(_metadata.GetRowCount(TableIndex.MethodDef) + 1));
        if (!parent.IsNil) _metadata.AddNestedType(handle, parent);
        Parameters(handle, arity, genericAttributes);
        return handle;
    }

    // Add each type before its contiguous member rows, as required by TypeDef lists.
    internal MethodDefinitionHandle Method(string name, byte[] signature, int arity = 0,
        MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.Static,
        Action<InstructionEncoder>? emit = null, byte[]? locals = null)
    {
        int offset = -1;
        if (emit is not null)
        {
            var il = new InstructionEncoder(new BlobBuilder(), new ControlFlowBuilder());
            emit(il);
            var localSignature = locals is null ? default : _metadata.AddStandaloneSignature(_metadata.GetOrAddBlob(locals));
            offset = _bodies.AddMethodBody(il, 8, localSignature, MethodBodyAttributes.InitLocals);
        }
        var handle = _metadata.AddMethodDefinition(attributes, emit is null ? MethodImplAttributes.Runtime : MethodImplAttributes.IL,
            _metadata.GetOrAddString(name), _metadata.GetOrAddBlob(signature), offset, MetadataTokens.ParameterHandle(1));
        Parameters(handle, arity);
        return handle;
    }

    private void Parameters(EntityHandle owner, int count, GenericParameterAttributes attributes = GenericParameterAttributes.None)
    {
        if (count > 0) _parameters.Add((owner, count, attributes));
    }

    internal FieldDefinitionHandle Field(string name, params byte[] signature) =>
        FieldWithAttributes(name, FieldAttributes.Public, signature);

    internal FieldDefinitionHandle FieldWithAttributes(string name, FieldAttributes attributes, params byte[] signature) =>
        _metadata.AddFieldDefinition(attributes, _metadata.GetOrAddString(name), _metadata.GetOrAddBlob(signature));

    internal void Implements(TypeDefinitionHandle owner, EntityHandle @interface) => _metadata.AddInterfaceImplementation(owner, @interface);

    internal void MethodImplementation(TypeDefinitionHandle owner, EntityHandle body, EntityHandle declaration) =>
        _metadata.AddMethodImplementation(owner, body, declaration);

    internal UserStringHandle String(string value) => _metadata.GetOrAddUserString(value);

    internal TypeSpecificationHandle Specification(params byte[] signature) =>
        _metadata.AddTypeSpecification(_metadata.GetOrAddBlob(signature));

    internal MemberReferenceHandle Member(EntityHandle parent, string name, params byte[] signature) =>
        _metadata.AddMemberReference(parent, _metadata.GetOrAddString(name), _metadata.GetOrAddBlob(signature));

    internal MethodSpecificationHandle Instantiate(EntityHandle method, params byte[] signature) =>
        _metadata.AddMethodSpecification(method, _metadata.GetOrAddBlob(signature));

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
        foreach (var group in _parameters.OrderBy(p => MetadataTokens.GetRowNumber(p.Owner) * 2
            + (p.Owner.Kind == HandleKind.MethodDefinition ? 1 : 0)))
            for (int i = 0; i < group.Count; i++)
                _metadata.AddGenericParameter(group.Owner, group.Attributes,
                    _metadata.GetOrAddString("T" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), i);
        _parameters.Clear();
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(_metadata),
            _bodies.Builder, deterministicIdProvider: _ =>
                new BlobContentId(new Guid("2b0b3f0a-bdea-4d77-9650-c5988d4ac20c"), 1));
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        return blob.ToArray();
    }
}
