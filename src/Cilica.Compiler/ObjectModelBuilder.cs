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

/// <summary>Builds a bounded class/field closure through explicit metadata resolution; never executes input.</summary>
public static class ObjectModelBuilder
{
    public static ObjectModelResult Build(AssemblyResolver resolver, ResolvedType root, ResolvedAssembly runtimeLibrary)
    {
        ArgumentNullException.ThrowIfNull(root);
        var result = BuildCatalog(resolver, [root], runtimeLibrary);
        return new(result.Catalog is null ? null : new(Id(root), result.Catalog.ObjectBase, result.Catalog.Types), result.Diagnostics);
    }

    public static ObjectCatalogResult BuildCatalog(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly runtimeLibrary) => BuildCatalog(resolver, roots, runtimeLibrary, null);

    internal static ObjectCatalogResult BuildCatalog(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly runtimeLibrary, Func<ResolvedType, TypeReadLimits, TypeInput>? readType)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes)
            throw new ArgumentException("Catalog needs 1..256 explicit roots.", nameof(roots));

        var selected = resolver.ResolveRuntimeType(runtimeLibrary, PrimitiveTypeCode.Object);
        if (selected.Value is not ResolvedType objectBase) return new(null, selected.Diagnostics);
        var parents = new Dictionary<ObjectTypeId, (ObjectTypeId? Parent, int Field)>();
        var pending = new Queue<ResolvedType>();
        var layouts = new List<ObjectTypeLayout>();
        int fields = 0, members = 0;
        string Path(ObjectTypeId id)
        {
            var steps = new List<string>();
            while (true)
            {
                var inclusion = parents[id];
                steps.Add(id.ContentHash + ":0x" + id.Token.ToString("x8", CultureInfo.InvariantCulture)
                    + (inclusion.Field == 0 ? "" : " via field 0x" + inclusion.Field.ToString("x8", CultureInfo.InvariantCulture)));
                if (inclusion.Parent is not ObjectTypeId parent) break;
                id = parent;
            }
            steps.Reverse(); return string.Join(" -> ", steps);
        }
        ObjectCatalogResult Failed(ObjectTypeId id, string code, string message, string? location = null) =>
            new(null, [new(code, DiagnosticSeverity.Error, message, (location is null ? "" : location + "; ") + "layout inclusion: " + Path(id))]);
        bool Include(ResolvedType type, ObjectTypeId? parent = null, int field = 0)
        {
            var id = Id(type);
            if (parents.ContainsKey(id)) return true;
            if (parents.Count >= ObjectModelLimits.MaximumTypes) return false;
            parents.Add(id, (parent, field)); pending.Enqueue(type); return true;
        }
        // Root first: ownership must be checked even if another session has the same content identity as the base.
        foreach (var root in roots)
        {
            ArgumentNullException.ThrowIfNull(root);
            // Validate every root's reader ownership before identity deduplication.
            var owned = resolver.ResolveType(root.Assembly, root.Handle);
            if (owned.Value is null) return new(null, owned.Diagnostics);
            if (!Include(root)) return new(null, [new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Object catalog type limit exceeded.")]);
        }
        if (!Include(objectBase)) return new(null, [new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, "Object catalog type limit exceeded.")]);
        while (pending.TryDequeue(out ResolvedType? type))
        {
            var id = Id(type);
            if (members >= ObjectModelLimits.MaximumMetadataMembers)
                return Failed(id, DiagnosticIds.TranslationLimit, "Object layout metadata-member limit exceeded.");
            var budget = new TypeReadLimits(ObjectModelLimits.MaximumFields - fields, ObjectModelLimits.MaximumMetadataMembers - members, ObjectModelLimits.MaximumOwnerDepth);
            TypeInput input;
            if (readType is not null) input = readType(type, budget);
            else
            {
                var read = resolver.ReadTypeInput(type.Assembly, type.Handle, budget);
                if (read.Value is not TypeInput snapshot)
                    return new(null, [.. read.Diagnostics.Select(d => d with { Location = d.Location + "; layout inclusion: " + Path(id) })]);
                input = snapshot;
            }
            members += input.MetadataMembersRead;
            if (input.Fields.Length > ObjectModelLimits.MaximumFields - fields || members > ObjectModelLimits.MaximumMetadataMembers)
                return Failed(id, DiagnosticIds.TranslationLimit, "Aggregate catalog field/metadata-member limit exceeded.");
            if (id.Token == 0x02000001)
                return Failed(id, DiagnosticIds.ExecutionUnsupported, "The module definition is not an instance class.", input.Source.Name);
            if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || input.InterfaceCount != 0
                || input.PackingSize != 0 || input.ClassSize != 0 || input.HasTypeInitializer || input.HasModuleInitializer
                || !ObjectModelValidator.ClassAttributes(input.Attributes))
                return Failed(id, DiagnosticIds.ExecutionUnsupported, ObjectModel.Profile +
                    " requires concrete nongeneric automatic-layout implementation classes without interfaces, native layout or type/module initialization.", input.Source.Name);
            ObjectTypeId? baseId = input.BaseType is TypeSignature.Named { Kind: SignatureTypeKind.Unknown or SignatureTypeKind.Class } named
                ? Id(named.Definition) : null;
            if (id == Id(objectBase) ? input.BaseType is not null || !input.Fields.IsEmpty
                || (input.Attributes & System.Reflection.TypeAttributes.Sealed) != 0
                : baseId != Id(objectBase))
                return Failed(id, DiagnosticIds.ExecutionUnsupported, "Classes must derive directly from the selected, fieldless System.Object definition.", input.Source.Name);
            var declared = new List<ObjectField>();
            foreach (var field in input.Fields)
            {
                string location = input.Source.Name + "::" + field.Name + " token 0x" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
                if (field.HasExplicitStorage || !ObjectModelValidator.FieldAttributesSupported(field.Attributes))
                    return Failed(id, DiagnosticIds.ExecutionUnsupported, "Static, literal, marshalled, RVA, constant and explicit-offset field storage require later lowering.", location);
                ObjectStorage storage;
                switch (field.Signature)
                {
                    case TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }: storage = new ObjectStorage.Int32(); break;
                    case TypeSignature.Primitive { Code: PrimitiveTypeCode.Object }: storage = new ObjectStorage.Reference(Id(objectBase)); break;
                    case TypeSignature.Named { Kind: SignatureTypeKind.Class } reference:
                        if (!Include(reference.Definition, id, field.Token))
                            return Failed(id, DiagnosticIds.TranslationLimit, "Object layout type limit exceeded.", location);
                        storage = new ObjectStorage.Reference(Id(reference.Definition)); break;
                    default: return Failed(id, DiagnosticIds.ExecutionSignatureUnsupported,
                        "Object fields require Int32 or class-reference storage; value types, arrays, strings, modifiers and pointers need later profiles.", location);
                }
                declared.Add(new(new(id, field.Token), field.Name, field.Attributes, storage));
            }
            fields += input.Fields.Length;
            layouts.Add(new(input.Source, baseId, input.Attributes, [.. declared]));
        }
        var model = new ObjectTypeCatalog([.. roots.Select(Id).Distinct()], Id(objectBase),
            [.. layouts.OrderBy(t => t.Source.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Source.Token)]);
        ObjectModelValidator.Validate(model);
        return new(model, []);
    }

    internal static ObjectTypeId Id(ResolvedType type) => new(type.Assembly.ContentHash.ToLowerInvariant(), MetadataTokens.GetToken(type.Handle));
}
