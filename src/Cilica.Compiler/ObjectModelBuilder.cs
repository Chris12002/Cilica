using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Builds a bounded class/field/interface/vector closure through explicit metadata resolution; never executes input.</summary>
public static class ObjectModelBuilder
{
    internal sealed record TypeSnapshot(TypeInput? Value, ImmutableArray<Diagnostic> Diagnostics);

    public static ObjectModelResult Build(AssemblyResolver resolver, ResolvedType root, ResolvedAssembly runtimeLibrary)
    {
        ArgumentNullException.ThrowIfNull(root);
        var result = BuildCatalog(resolver, [root], runtimeLibrary);
        return new(result.Catalog is null ? null : new ObjectModel(Id(root), result.Catalog.ObjectBase, result.Catalog.Types)
            { Statics = result.Catalog.Statics, Interfaces = result.Catalog.Interfaces, Arrays = result.Catalog.Arrays, ValueArrays = result.Catalog.ValueArrays, String = result.Catalog.String, Delegates = result.Catalog.Delegates }, result.Diagnostics);
    }

    public static ObjectCatalogResult BuildCatalog(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly runtimeLibrary) => BuildCatalog(resolver, roots, runtimeLibrary, null);

    internal static ObjectCatalogResult BuildCatalog(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly runtimeLibrary, Func<ResolvedType, TypeReadLimits, TypeInput>? readType, ImmutableArray<ObjectTypeId> arrayRoots = default,
        ObjectStringType? strings = null)
    {
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes)
            throw new ArgumentException("Catalog needs 1..256 explicit roots.", nameof(roots));
        foreach (var root in roots) ArgumentNullException.ThrowIfNull(root);
        try { return BuildCatalogCore(resolver, [.. roots.Select(t => new ConstructedType(t, SignatureList<TypeSignature>.Empty))], runtimeLibrary,
            readType is null ? null : (type, limits) => new TypeSnapshot(readType(type.Definition, limits), []), arrayRoots, strings, generics: false); }
        catch (ObjectStringType.Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    internal static ObjectCatalogResult BuildClosedCatalog(AssemblyResolver resolver, ImmutableArray<ConstructedType> roots,
        ResolvedAssembly library, Func<ConstructedType, TypeReadLimits, TypeSnapshot> readType,
        ImmutableArray<ObjectTypeId> arrays, ObjectStringType strings, Func<ConstructedType, ObjectDelegateType?>? delegates = null, bool integers64 = false, Func<ConstructedType, ObjectTypeId>? inline = null, Func<TypeSignature, bool>? enumScalar = null) =>
        BuildCatalogCore(resolver, roots, library, readType, arrays, strings, generics: true, delegates, integers64, inline, enumScalar);

    private static ObjectCatalogResult BuildCatalogCore(AssemblyResolver resolver, ImmutableArray<ConstructedType> roots,
        ResolvedAssembly runtimeLibrary, Func<ConstructedType, TypeReadLimits, TypeSnapshot>? readType, ImmutableArray<ObjectTypeId> arrayRoots,
        ObjectStringType? strings, bool generics, Func<ConstructedType, ObjectDelegateType?>? delegates = null, bool integers64 = false, Func<ConstructedType, ObjectTypeId>? inline = null, Func<TypeSignature, bool>? enumScalar = null)
    {
        strings ??= new(resolver, runtimeLibrary, new(ObjectModelLimits.MaximumMetadataMembers));
        ObjectTypeId? stringType = null;
        ArgumentNullException.ThrowIfNull(resolver);
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes)
            throw new ArgumentException("Catalog needs 1..256 explicit roots.", nameof(roots));

        var selected = resolver.ResolveRuntimeType(runtimeLibrary, PrimitiveTypeCode.Object);
        if (selected.Value is not ResolvedType objectBase) return new(null, selected.Diagnostics);
        var parents = new Dictionary<ObjectTypeId, (ObjectTypeId? Parent, int Field, bool BaseEdge, bool InterfaceEdge)>();
        var arrays = arrayRoots.IsDefault ? new HashSet<ObjectTypeId>() : new HashSet<ObjectTypeId>(arrayRoots);
        var pending = new Queue<ConstructedType>();
        ObjectTypeId ClosedId(ConstructedType type) => Id(type);
        ConstructedType Plain(ResolvedType type) => new(type, SignatureList<TypeSignature>.Empty);
        var layouts = new List<ObjectTypeLayout>();
        var interfaces = new List<ObjectInterface>();
        var interfaceTypes = new Dictionary<ObjectTypeId, ConstructedType>();
        var delegateTypes = new List<ObjectDelegateType>();
        int fields = 0, members = 0;
        string Path(ObjectTypeId id)
        {
            var steps = new List<string>();
            while (true)
            {
                var inclusion = parents[id];
                steps.Add(id.ContentHash + ":0x" + id.Token.ToString("x8", CultureInfo.InvariantCulture) + ":" + id.Specialization
                    + (inclusion.BaseEdge ? " via base class" : inclusion.InterfaceEdge ? " via interface" : inclusion.Field == 0 ? "" : " via field 0x" + inclusion.Field.ToString("x8", CultureInfo.InvariantCulture)));
                if (inclusion.Parent is not ObjectTypeId parent) break;
                id = parent;
            }
            steps.Reverse(); return string.Join(" -> ", steps);
        }
        ObjectCatalogResult Failed(ObjectTypeId id, string code, string message, string? location = null) =>
            new(null, [new(code, DiagnosticSeverity.Error, message, (location is null ? "" : location + "; ") + "layout inclusion: " + Path(id))]);
        bool Include(ConstructedType type, ObjectTypeId? parent = null, int field = 0, bool baseEdge = false, bool interfaceEdge = false)
        {
            var id = ClosedId(type);
            if (parents.ContainsKey(id)) return true;
            if (parents.Count + arrays.Count >= ObjectModelLimits.MaximumTypes) return false;
            parents.Add(id, (parent, field, baseEdge, interfaceEdge)); pending.Enqueue(type); return true;
        }
        // Root first: ownership must be checked even if another session has the same content identity as the base.
        foreach (var root in roots)
        {
            ArgumentNullException.ThrowIfNull(root);
            // Validate every root's reader ownership before identity deduplication.
            var owned = resolver.ResolveType(root.Definition.Assembly, root.Definition.Handle);
            if (owned.Value is null) return new(null, owned.Diagnostics);
            if (!Include(root)) return new(null, [new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Object catalog type limit exceeded.")]);
        }
        if (!Include(Plain(objectBase))) return new(null, [new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Object catalog type limit exceeded.")]);
        while (pending.TryDequeue(out ConstructedType? type))
        {
            var id = ClosedId(type);
            if (members >= ObjectModelLimits.MaximumMetadataMembers)
                return Failed(id, DiagnosticIds.TranslationLimit, "Object layout metadata-member limit exceeded.");
            if (type.Arguments.Count == 0 && strings.Matches(type.Definition))
            {
                stringType = Id(strings.Require(ObjectModelLimits.MaximumMetadataMembers - members));
                members += strings.ValidationWork; // Shell replaces the ordinary field/interface snapshot.
                if (members > ObjectModelLimits.MaximumMetadataMembers)
                    return Failed(id, DiagnosticIds.TranslationLimit, "Aggregate catalog metadata-member limit exceeded.");
                continue;
            }
            if (delegates?.Invoke(type) is { } callable)
            {
                delegateTypes.Add(callable); continue;
            }
            var budget = new TypeReadLimits(ObjectModelLimits.MaximumFields - fields, ObjectModelLimits.MaximumMetadataMembers - members, ObjectModelLimits.MaximumOwnerDepth);
            TypeSnapshot read;
            if (readType is not null) read = readType(type, budget);
            else
            {
                var result = resolver.ReadTypeInput(type.Definition.Assembly, type.Definition.Handle, budget);
                read = new(result.Value, result.Diagnostics);
            }
            if (read.Value is not TypeInput input)
                return new(null, [.. read.Diagnostics.Select(d => d with { Location = d.Location + "; layout inclusion: " + Path(id) })]);
            members += input.MetadataMembersRead;
            if (input.Fields.Length > ObjectModelLimits.MaximumFields - fields || members > ObjectModelLimits.MaximumMetadataMembers)
                return Failed(id, DiagnosticIds.TranslationLimit, "Aggregate catalog field/metadata-member limit exceeded.");
            if (id.Token == 0x02000001)
                return Failed(id, DiagnosticIds.ExecutionUnsupported, "The module definition is not an instance class.", input.Source.Name);
            if (input.IsByRefLike)
                return Failed(id, DiagnosticIds.ExecutionUnsupported, "ByRefLike types cannot enter heap/reference layouts.", input.Source.Name);
            bool isInterface = (input.Attributes & System.Reflection.TypeAttributes.Interface) != 0;
            if (isInterface && !input.ExternallyVisible)
                return Failed(id, DiagnosticIds.ExecutionUnsupported,
                    "Interface contracts require public visibility and public enclosing types.", input.Source.Name);
            if (input.IsReferenceAssembly || input.OwnerGenericArity != type.Arguments.Count || isInterface && type.Arguments.Count != 0 && !integers64
                || input.PackingSize != 0 || input.ClassSize != 0 || input.HasTypeInitializer && (!integers64 || (input.Attributes & System.Reflection.TypeAttributes.BeforeFieldInit) == 0) || input.HasModuleInitializer
                || !(isInterface ? ObjectModelValidator.InterfaceAttributes(input.Attributes) : ObjectModelValidator.ClassAttributes(input.Attributes, integers64)))
                return Failed(id, DiagnosticIds.ExecutionUnsupported, ObjectModel.Profile +
                    (generics ? " requires closed automatic-layout classes or admitted public fieldless interfaces without native layout or initialization."
                        : " requires concrete nongeneric automatic-layout classes or public fieldless interfaces without native layout or type/module initialization."), input.Source.Name);
            if (input.Interfaces.IsDefault || input.Interfaces.Length != input.InterfaceCount || input.Interfaces.Length > ObjectModelLimits.MaximumTypes)
                return Failed(id, DiagnosticIds.ExecutionUnsupported, "Interface snapshots must retain every bounded declaration edge.", input.Source.Name);
            var contracts = new List<ObjectTypeId>();
            foreach (var signature in input.Interfaces)
            {
                ConstructedType? contract = signature switch {
                    TypeSignature.Named { Kind: SignatureTypeKind.Unknown or SignatureTypeKind.Class } named => Plain(named.Definition),
                    TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } generic when integers64 => new(generic.Definition.Definition, generic.Arguments),
                    _ => null };
                if (contract is null)
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "Interface edges require admitted exact nominal or closed interface definitions.", input.Source.Name);
                if (!Include(contract, id, interfaceEdge: true))
                    return Failed(id, DiagnosticIds.TranslationLimit, "Object catalog type limit exceeded while including an interface.", input.Source.Name);
                contracts.Add(ClosedId(contract));
            }
            if (isInterface)
            {
                if (input.BaseType is not null || !input.Fields.IsEmpty)
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "Interface contracts must be fieldless and have no class base.", input.Source.Name);
                if (input.ClosedParameterAttributes.Length != type.Arguments.Count)
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "Closed interface parameter attributes must match the declared arguments.", input.Source.Name);
                var arguments = ImmutableArray.CreateBuilder<ObjectInterfaceArgument>();
                bool exceeded = false;
                ObjectTypeId Reference(ConstructedType reference)
                {
                    if (!Include(reference, id)) exceeded = true;
                    return ClosedId(reference);
                }
                for (int index = 0; index < type.Arguments.Count; ++index)
                {
                    var argument = type.Arguments[index];
                    var storage = ObjectSignatureTypes.Convert(argument, Id(objectBase), reference => Reference(Plain(reference)), vector => {
                        if (!arrays.Contains(vector) && parents.Count + arrays.Count >= ObjectModelLimits.MaximumTypes) exceeded = true;
                        else arrays.Add(vector); return vector;
                    }, text: () => Reference(Plain(strings.Require(ObjectModelLimits.MaximumMetadataMembers - members))),
                        closedReference: generic => Reference(new(generic.Definition.Definition, generic.Arguments)), integers64: integers64, inline: inline);
                    if (argument is TypeSignature.Vector && storage is null)
                        return Failed(id, DiagnosticIds.ExecutionUnsupported, "Interface array arguments require an admitted vector element type.", input.Source.Name);
                    var valueArgument = argument switch {
                        TypeSignature.Named { Kind: SignatureTypeKind.ValueType } named => Plain(named.Definition),
                        TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                        _ => null };
                    arguments.Add(new(ClosedGenericKey.Create(new([argument]), SignatureList<TypeSignature>.Empty),
                        storage?.Kind == IrTypeKind.Reference ? storage.Class : null,
                        input.ClosedParameterAttributes[index] & System.Reflection.GenericParameterAttributes.VarianceMask) {
                            Value = valueArgument is not null && inline is not null && enumScalar?.Invoke(argument) != true ? inline(valueArgument) : null });
                }
                if (exceeded) return Failed(id, DiagnosticIds.TranslationLimit, "Interface argument type closure exceeds the catalog limit.", input.Source.Name);
                interfaceTypes.Add(id, type);
                interfaces.Add(new(input.Source, input.Attributes, [.. contracts]) { ExternallyVisible = input.ExternallyVisible, Arguments = arguments.ToImmutable() });
                continue;
            }
            ConstructedType? baseType = input.BaseType switch
            {
                TypeSignature.Named { Kind: SignatureTypeKind.Unknown or SignatureTypeKind.Class } named => Plain(named.Definition),
                TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } generic when generics => new(generic.Definition.Definition, generic.Arguments),
                _ => null,
            };
            ObjectTypeId? baseId = baseType is null ? null : ClosedId(baseType);
            if (id == Id(objectBase))
            {
                if (input.BaseType is not null || !input.Fields.IsEmpty || contracts.Count != 0 || (input.Attributes & System.Reflection.TypeAttributes.Sealed) != 0)
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "The selected fieldless System.Object definition must be unsealed and have no parent.", input.Source.Name);
            }
            else
            {
                if (baseId is null)
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "Classes require a supported closed class base leading to the selected System.Object.", input.Source.Name);
                if (!Include(baseType!, id, baseEdge: true))
                    return Failed(id, DiagnosticIds.TranslationLimit, "Object layout type limit exceeded while including a base class.", input.Source.Name);
            }
            var declared = new List<ObjectField>();
            foreach (var field in input.Fields)
            {
                if (integers64 && DefaultStaticFields.IsStorage(input, field)) continue;
                string location = input.Source.Name + "::" + field.Name + " token 0x" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
                if (field.HasExplicitStorage || !ObjectModelValidator.FieldAttributesSupported(field.Attributes))
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "Static, literal, marshalled, RVA, constant and explicit-offset field storage require later lowering.", location);
                if (integers64 && enumScalar?.Invoke(field.Signature) == true)
                {
                    declared.Add(new(new(id, field.Token), field.Name, field.Attributes, new ObjectStorage.Int32()));
                    continue;
                }
                if (integers64 && inline is not null)
                {
                    var nested = field.Signature switch {
                        TypeSignature.Named { Kind: SignatureTypeKind.ValueType } named => Plain(named.Definition),
                        TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                        _ => null };
                    if (nested is not null)
                    {
                        declared.Add(new(new(id, field.Token), field.Name, field.Attributes, new ObjectStorage.Value(inline(nested))));
                        continue;
                    }
                }
                bool exceeded = false;
                var storageType = generics && field.Signature is TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } genericReference
                    ? GenericReference(genericReference) : ObjectSignatureTypes.Convert(field.Signature, Id(objectBase), reference =>
                {
                    if (!Include(Plain(reference), id, field.Token)) exceeded = true;
                    return Id(reference);
                }, array =>
                {
                    if (!arrays.Contains(array) && parents.Count + arrays.Count >= ObjectModelLimits.MaximumTypes) exceeded = true;
                    else arrays.Add(array);
                    return array;
                }, text: () =>
                {
                    var text = strings.Require(ObjectModelLimits.MaximumMetadataMembers - members);
                    if (!Include(Plain(text), id, field.Token)) exceeded = true;
                    return Id(text);
                }, integers64: integers64, closedReference: integers64 ? generic => GenericReference(generic).Class! : null, inline: inline);
                IrType GenericReference(TypeSignature.Generic generic)
                {
                    var closed = new ConstructedType(generic.Definition.Definition, generic.Arguments);
                    if (!Include(closed, id, field.Token)) exceeded = true;
                    return IrType.Reference(ClosedId(closed));
                }
                if (exceeded) return Failed(id, DiagnosticIds.TranslationLimit, "Object layout type limit exceeded.", location);
                if (storageType is null) return Failed(id, DiagnosticIds.ExecutionSignatureUnsupported, ObjectSignatureTypes.Unsupported, location);
                ObjectStorage storage = storageType == IrType.Int32 ? new ObjectStorage.Int32() : storageType == IrType.Int64
                    ? new ObjectStorage.Int64() : new ObjectStorage.Reference(storageType.Class!);
                declared.Add(new(new(id, field.Token), field.Name, field.Attributes, storage));
            }
            fields += input.Fields.Length;
            layouts.Add(new(input.Source, baseId, input.Attributes, [.. declared]) { LexicalOwners = input.LexicalOwners, Interfaces = [.. contracts] });
        }
        if (stringType is not null && interfaces.Count != 0)
            members += strings.CheckInterfaces([.. interfaces], ObjectModelLimits.MaximumMetadataMembers - members);
        if (arrays.Count != 0 && interfaces.Count != 0)
            members += ObjectArrayInterfaces.Check(resolver, runtimeLibrary, interfaces, interfaceTypes, ObjectModelLimits.MaximumMetadataMembers - members, strings.MetadataBudget, integers64);
        var model = new ObjectTypeCatalog([.. roots.Select(ClosedId).Distinct(), .. arrayRoots.IsDefault ? [] : arrayRoots.Distinct()], Id(objectBase),
            [.. layouts.OrderBy(t => t.Source.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Source.Token).ThenBy(t => t.Source.Specialization, StringComparer.Ordinal)])
        { Integer64 = integers64, Delegates = [.. delegateTypes], String = stringType, Arrays = [.. arrays.OrderBy(a => a.ArrayElement!.Kind).ThenBy(a => a.ArrayElement!.Type?.ContentHash, StringComparer.Ordinal).ThenBy(a => a.ArrayElement!.Type?.Token)],
            Interfaces = [.. interfaces.OrderBy(t => t.Source.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Source.Token)] };
        // Metadata can form cycles or extend sealed classes. Surface those as
        // input diagnostics rather than leaking the detached API's exception.
        try { ObjectModelValidator.Validate(model, inlineFields: inline is not null); }
        catch (ArgumentException exception) { return Failed(ClosedId(roots[0]), DiagnosticIds.ExecutionUnsupported, exception.Message); }
        return new(model, []);
    }

    internal static ObjectTypeId Id(ConstructedType type) => Id(type.Definition) with
        { Specialization = ClosedGenericKey.Create(type.Arguments, SignatureList<TypeSignature>.Empty) };

    internal static ObjectTypeId Id(ResolvedType type) => new(type.Assembly.ContentHash.ToLowerInvariant(), MetadataTokens.GetToken(type.Handle));
}
