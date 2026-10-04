using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Reads bounded nongeneric inline layouts as metadata; never loads or executes input assemblies.</summary>
public static class ValueModelBuilder
{
    public static ValueCatalogResult BuildCatalog(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly runtimeLibrary) => BuildCatalog(resolver, roots, runtimeLibrary,
            new MemberResolutionBudget(ObjectModelLimits.MaximumMetadataMembers));

    internal static ValueCatalogResult BuildCatalog(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly runtimeLibrary, MemberResolutionBudget work)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(runtimeLibrary);
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes)
            throw new ArgumentException("Value catalog needs 1..256 explicit roots.", nameof(roots));
        try { return Build(resolver, roots, runtimeLibrary, work); }
        catch (Failure failure) { return new(null, failure.Diagnostics); }
        catch (ObjectStringType.Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    private static ValueCatalogResult Build(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ResolvedAssembly library, MemberResolutionBudget work)
    {
        var strings = new ObjectStringType(resolver, library, work);
        var inputs = new Dictionary<ObjectTypeId, TypeInput>();
        var identities = new HashSet<ObjectTypeId>();
        var paths = new Dictionary<ObjectTypeId, string>();
        var references = new Dictionary<ObjectTypeId, ResolvedType>();
        var arrays = new HashSet<ObjectTypeId>();
        var pending = new Queue<ResolvedType>();
        var values = new List<ValueTypeLayout>();
        int fields = 0;
        static T Result<T>(ResolutionResult<T> result) where T : class => result.Value ?? throw new Failure(result.Diagnostics);
        static ObjectTypeId Id(ResolvedType type) => ObjectModelBuilder.Id(type);
        static string Label(ResolvedType type) => Id(type).ContentHash + ":0x" + Id(type).Token.ToString("x8", CultureInfo.InvariantCulture);
        static void Fail(string message, string? location = null, string code = DiagnosticIds.ExecutionUnsupported) =>
            throw new Failure([new(code, DiagnosticSeverity.Error, message, location)]);
        void IncludeId(ObjectTypeId id)
        {
            if (identities.Add(id) && identities.Count > ObjectModelLimits.MaximumTypes)
                Fail("Combined value/reference type limit exceeded.", code: DiagnosticIds.TranslationLimit);
        }
        TypeInput Read(ResolvedType type, TypeReadLimits limits)
        {
            IncludeId(Id(type));
            if (inputs.TryGetValue(Id(type), out var cached)) return cached;
            if (work.Remaining == 0) Fail("Value catalog metadata-member limit exceeded.", code: DiagnosticIds.TranslationLimit);
            var input = Result(resolver.ReadTypeInput(type.Assembly, type.Handle, new(
                Math.Min(limits.MaximumFields, ObjectModelLimits.MaximumFields - fields),
                Math.Min(limits.MaximumMembers, work.Remaining), limits.MaximumOwnerDepth, limits.MaximumInterfaces)));
            if (!work.TryConsume(input.MetadataMembersRead)) Fail("Value catalog metadata-member limit exceeded.", code: DiagnosticIds.TranslationLimit);
            fields += input.Fields.Length;
            inputs.Add(Id(type), input); return input;
        }
        void IncludeValue(ResolvedType type, string path)
        {
            IncludeId(Id(type));
            if (paths.TryAdd(Id(type), path)) pending.Enqueue(type);
        }
        ObjectTypeId IncludeReference(ResolvedType type)
        {
            IncludeId(Id(type)); references.TryAdd(Id(type), type); return Id(type);
        }

        // Validate ownership for every explicit root before deduplicating identities.
        foreach (var root in roots)
        {
            ArgumentNullException.ThrowIfNull(root);
            _ = Result(resolver.ResolveType(root.Assembly, root.Handle));
            IncludeValue(root, Label(root));
        }
        var objectBase = Result(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object));
        IncludeReference(objectBase);
        var valueBase = Result(resolver.ResolveType(library, "System", "ValueType"));
        var baseInput = Read(valueBase, new());
        if (baseInput.IsReferenceAssembly || baseInput.OwnerGenericArity != 0 || baseInput.HasTypeInitializer
            || baseInput.HasModuleInitializer || !baseInput.Fields.IsEmpty || baseInput.InterfaceCount != 0
            || baseInput.PackingSize != 0 || baseInput.ClassSize != 0
            || (baseInput.Attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.LayoutMask)) != TypeAttributes.Abstract
            || !ObjectModelValidator.ClassAttributes(baseInput.Attributes & ~TypeAttributes.Abstract)
            || baseInput.BaseType is not TypeSignature.Named { Kind: SignatureTypeKind.Unknown or SignatureTypeKind.Class } parent
            || Id(parent.Definition) != Id(objectBase))
            Fail("Selected System.ValueType must be a fieldless, nongeneric abstract direct child of the selected Object without initialization or native layout.", baseInput.Source.Name);

        while (pending.TryDequeue(out var type))
        {
            var id = Id(type);
            TypeInput input;
            try { input = Read(type, new()); }
            catch (Failure failure) { throw new Failure([.. failure.Diagnostics.Select(d => d with { Location = d.Location + "; value inclusion: " + paths[id] })]); }
            if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || input.HasTypeInitializer || input.HasModuleInitializer
                || input.PackingSize != 0 || input.ClassSize != 0 || input.InterfaceCount != 0
                || !ValueLayoutPlanner.AttributesSupported(input.Attributes)
                || input.BaseType is not TypeSignature.Named { Kind: SignatureTypeKind.Unknown or SignatureTypeKind.Class } declaredBase
                || Id(declaredBase.Definition) != Id(valueBase))
                Fail("Value layouts require sealed nongeneric direct children of the selected System.ValueType, default managed layout, and no interfaces or type/module initialization.", input.Source.Name + "; value inclusion: " + paths[id]);
            var declared = new List<ObjectField>();
            foreach (var field in input.Fields)
            {
                string location = paths[id] + " -> " + input.Source.Name + "::" + field.Name + " field 0x" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
                if (field.HasExplicitStorage || !ObjectModelValidator.FieldAttributesSupported(field.Attributes))
                    Fail("Static, literal, marshalled, RVA, explicit-offset and constant field storage require later lowering.", location);
                ObjectStorage storage;
                if (field.Signature is TypeSignature.Named { Kind: SignatureTypeKind.ValueType } nested)
                {
                    IncludeValue(nested.Definition, location); storage = new ObjectStorage.Value(Id(nested.Definition));
                }
                else
                {
                    var converted = ObjectSignatureTypes.Convert(field.Signature, Id(objectBase), IncludeReference,
                        array => { IncludeId(array); arrays.Add(array); return array; },
                        text: () => IncludeReference(strings.Require()));
                    if (converted is null)
                        Fail("Value fields require Int32, supported object references/vectors, or named nongeneric inline structs. Byrefs, pointers, other primitives, generic values and value arrays require later lowering.", location, DiagnosticIds.ExecutionSignatureUnsupported);
                    storage = converted == IrType.Int32 ? new ObjectStorage.Int32() : new ObjectStorage.Reference(converted!.Class!);
                }
                declared.Add(new(new(id, field.Token), field.Name, field.Attributes, storage));
            }
            values.Add(new(input.Source, input.Attributes, [.. declared]) { ExternallyVisible = input.ExternallyVisible });
        }
        var referenceResult = ObjectModelBuilder.BuildCatalog(resolver, [.. references.Values], library, Read, [.. arrays], strings);
        if (referenceResult.Catalog is null) return new(null, referenceResult.Diagnostics);
        var catalog = new ValueTypeCatalog([.. roots.Select(Id).Distinct()], Id(valueBase),
            [.. values.OrderBy(t => t.Source.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Source.Token)], referenceResult.Catalog);
        try { _ = ValueLayoutPlanner.Build(catalog); }
        catch (ArgumentException exception)
        {
            Fail(exception.Message, "value inclusion: " + paths[Id(roots[0])]);
        }
        return new(catalog, []);
    }

    private sealed class Failure(ImmutableArray<Diagnostic> diagnostics) : Exception
    {
        internal ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
    }
}
