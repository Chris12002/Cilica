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
        ResolvedAssembly runtimeLibrary, MemberResolutionBudget work, bool includeInt32Vector = false,
        ImmutableArray<ResolvedType> referenceRoots = default, IReadOnlyDictionary<ObjectTypeId, TypeInput>? snapshots = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(runtimeLibrary);
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes)
            throw new ArgumentException("Value catalog needs 1..256 explicit roots.", nameof(roots));
        foreach (var root in roots) ArgumentNullException.ThrowIfNull(root);
        try { return Build(resolver, [.. roots.Select(t => new ConstructedType(t, SignatureList<TypeSignature>.Empty))], runtimeLibrary, work, includeInt32Vector, referenceRoots.IsDefault ? default : [.. referenceRoots.Select(t => new ConstructedType(t, SignatureList<TypeSignature>.Empty))], snapshots, generics: false); }
        catch (Failure failure) { return new(null, failure.Diagnostics); }
        catch (ObjectStringType.Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    internal static ValueCatalogResult BuildClosedCatalog(AssemblyResolver resolver, ImmutableArray<ConstructedType> roots,
        ResolvedAssembly library, MemberResolutionBudget work, bool includeInt32Vector,
        ImmutableArray<ConstructedType> referenceRoots, IReadOnlyDictionary<ObjectTypeId, TypeInput> snapshots, Func<ConstructedType, ObjectDelegateType?>? delegates = null, bool integers64 = false, ImmutableArray<ObjectTypeId> vectorRoots = default, Action<ConstructedType, TypeInput>? observe = null)
    {
        try { return Build(resolver, roots, library, work, includeInt32Vector, referenceRoots, snapshots, generics: true, delegates, integers64, vectorRoots, observe); }
        catch (Failure failure) { return new(null, failure.Diagnostics); }
        catch (ObjectStringType.Failure failure) { return new(null, [failure.Diagnostic]); }
        catch (ArgumentException error) { return new(null, [new(DiagnosticIds.TranslationLimit, DiagnosticSeverity.Error, error.Message)]); }
    }

    private static ObjectTypeId ClosedId(ConstructedType type) => ObjectModelBuilder.Id(type);

    private static ValueCatalogResult Build(AssemblyResolver resolver, ImmutableArray<ConstructedType> roots,
        ResolvedAssembly library, MemberResolutionBudget work, bool includeInt32Vector,
        ImmutableArray<ConstructedType> referenceRoots, IReadOnlyDictionary<ObjectTypeId, TypeInput>? snapshots, bool generics, Func<ConstructedType, ObjectDelegateType?>? delegates = null, bool integers64 = false, ImmutableArray<ObjectTypeId> vectorRoots = default, Action<ConstructedType, TypeInput>? observe = null)
    {
        var strings = new ObjectStringType(resolver, library, work);
        var inputs = new Dictionary<ObjectTypeId, TypeInput>();
        var identities = new HashSet<ObjectTypeId>();
        var paths = new Dictionary<ObjectTypeId, string>();
        var references = new Dictionary<ObjectTypeId, ConstructedType>();
        var arrays = new HashSet<ObjectTypeId>();
        var pending = new Queue<ConstructedType>();
        var values = new List<ValueTypeLayout>();
        int fields = 0;
        static T Result<T>(ResolutionResult<T> result) where T : class => result.Value ?? throw new Failure(result.Diagnostics);
        static ObjectTypeId Id(ResolvedType type) => ObjectModelBuilder.Id(type);
        static string Label(ConstructedType type) => ClosedId(type).ContentHash + ":0x" + ClosedId(type).Token.ToString("x8", CultureInfo.InvariantCulture) + ":" + ClosedId(type).Specialization;
        static void Fail(string message, string? location = null, string code = DiagnosticIds.ExecutionUnsupported) =>
            throw new Failure([new(code, DiagnosticSeverity.Error, message, location)]);
        void IncludeId(ObjectTypeId id)
        {
            if (identities.Add(id) && identities.Count > ObjectModelLimits.MaximumTypes)
                Fail("Combined value/reference type limit exceeded.", code: DiagnosticIds.TranslationLimit);
        }
        TypeInput ReadClosed(ConstructedType type, TypeReadLimits limits)
        {
            var id = ClosedId(type);
            IncludeId(id);
            if (inputs.TryGetValue(id, out var cached)) return cached;
            int remainingFields = Math.Min(limits.MaximumFields, ObjectModelLimits.MaximumFields - fields);
            TypeInput input;
            if (snapshots is not null && snapshots.TryGetValue(id, out var preloaded))
            {
                // Discovery already charged this immutable snapshot to the same ledger.
                input = preloaded;
                if (input.Fields.Length > remainingFields || input.InterfaceCount > limits.MaximumInterfaces)
                    Fail("Cached type exceeds the remaining field/interface budget.", code: DiagnosticIds.TranslationLimit);
            }
            else
            {
                if (work.Remaining == 0) Fail("Value catalog metadata-member limit exceeded.", code: DiagnosticIds.TranslationLimit);
                var readLimits = new TypeReadLimits(remainingFields, Math.Min(limits.MaximumMembers, work.Remaining), limits.MaximumOwnerDepth, limits.MaximumInterfaces);
                input = Result(generics ? resolver.ReadClosedTypeInput(type, library, readLimits)
                    : resolver.ReadTypeInput(type.Definition.Assembly, type.Definition.Handle, readLimits));
                if (!work.TryConsume(input.MetadataMembersRead)) Fail("Value catalog metadata-member limit exceeded.", code: DiagnosticIds.TranslationLimit);
            }
            fields += input.Fields.Length;
            inputs.Add(id, input); observe?.Invoke(type, input); return input;
        }
        TypeInput Read(ResolvedType type, TypeReadLimits limits) => ReadClosed(new(type, SignatureList<TypeSignature>.Empty), limits);
        var enumScalars = new Dictionary<ResolvedType, bool>();
        bool EnumScalar(TypeSignature signature)
        {
            if (!integers64 || signature is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named) return false;
            if (enumScalars.TryGetValue(named.Definition, out bool known)) return known;
            try
            {
                bool scalar = Int32EnumStorage.Matches(signature,
                    resolver.ResolveType(library, "System", "Enum").Value, type => Read(type, new()));
                enumScalars.Add(named.Definition, scalar); return scalar;
            }
            catch (ArgumentException error) { Fail(error.Message); return false; }
        }
        void IncludeValue(ConstructedType type, string path)
        {
            ObjectTypeId id;
            try { id = ClosedId(type); }
            catch (ArgumentException error) { Fail(error.Message, "value inclusion: " + path, DiagnosticIds.TranslationLimit); return; }
            IncludeId(id);
            if (paths.TryAdd(id, path)) pending.Enqueue(type);
        }
        ObjectTypeId IncludeReference(ResolvedType type) => IncludeClosedReference(new(type, SignatureList<TypeSignature>.Empty));
        ObjectTypeId IncludeClosedReference(ConstructedType type)
        {
            var id = ClosedId(type); IncludeId(id); references.TryAdd(id, type); return id;
        }

        // Validate ownership for every explicit root before deduplicating identities.
        foreach (var root in roots)
        {
            ArgumentNullException.ThrowIfNull(root);
            _ = Result(resolver.ResolveType(root.Definition.Assembly, root.Definition.Handle));
            IncludeValue(root, Label(root));
        }
        var objectBase = Result(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object));
        IncludeReference(objectBase);
        if (!referenceRoots.IsDefault)
            foreach (var reference in referenceRoots)
            {
                _ = Result(resolver.ResolveType(reference.Definition.Assembly, reference.Definition.Handle));
                IncludeClosedReference(reference);
            }
        if (includeInt32Vector)
        {
            var vector = ObjectTypeId.Vector(Id(objectBase), ObjectArrayElement.Int32);
            IncludeId(vector); arrays.Add(vector);
        }
        if (!vectorRoots.IsDefault)
            foreach (var vector in vectorRoots) { IncludeId(vector); arrays.Add(vector); }
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

        ObjectCatalogResult referenceResult;
        do
        {
            while (pending.TryDequeue(out var type))
            {
                var id = ClosedId(type);
                TypeInput input;
                try { input = ReadClosed(type, new()); }
                catch (Failure failure) { throw new Failure([.. failure.Diagnostics.Select(d => d with { Location = d.Location + "; value inclusion: " + paths[id] })]); }
                if (input.IsByRefLike) Fail("ByRefLike values require stack-only lifetime and escape checking before executable layout.", input.Source.Name);
                if (input.IsReferenceAssembly || input.OwnerGenericArity != type.Arguments.Count || input.HasTypeInitializer || input.HasModuleInitializer
                    || input.PackingSize != 0 || input.ClassSize != 0 && !(input.ClassSize == 1 && (input.Fields.IsEmpty
                        || integers64 && input.Fields.All(f => DefaultStaticFields.IsDefault(input, f)))
                        && (input.Attributes & TypeAttributes.LayoutMask) == TypeAttributes.SequentialLayout) || !integers64 && input.InterfaceCount != 0
                    || !ValueLayoutPlanner.AttributesSupported(input.Attributes)
                    || input.BaseType is not TypeSignature.Named { Kind: SignatureTypeKind.Unknown or SignatureTypeKind.Class } declaredBase
                    || Id(declaredBase.Definition) != Id(valueBase))
                    Fail("Value layouts require sealed nongeneric direct children of the selected System.ValueType, default managed layout, and no interfaces or type/module initialization.", input.Source.Name + "; value inclusion: " + paths[id]);
                var declared = new List<ObjectField>();
                var contracts = new List<ObjectTypeId>();
                if (input.Interfaces.IsDefault || input.InterfaceCount != input.Interfaces.Length)
                    Fail("Value interface snapshot does not match its declaration count.", input.Source.Name);
                foreach (var signature in input.Interfaces)
                {
                    var contract = signature switch {
                        TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.Unknown } named => new ConstructedType(named.Definition, SignatureList<TypeSignature>.Empty),
                        TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } generic => new ConstructedType(generic.Definition.Definition, generic.Arguments),
                        _ => null };
                    if (contract is null) Fail("Value interface declarations require exact closed interface identities.", input.Source.Name);
                    var definition = ReadClosed(contract!, new());
                    if ((definition.Attributes & TypeAttributes.Interface) == 0) Fail("Value interface row names a non-interface type.", input.Source.Name);
                    contracts.Add(IncludeClosedReference(contract!));
                }
                foreach (var field in input.Fields)
                {
                    if (integers64 && DefaultStaticFields.IsDefault(input, field)) continue;
                    string location = paths[id] + " -> " + input.Source.Name + "::" + field.Name + " field 0x" + field.Token.ToString("x8", CultureInfo.InvariantCulture);
                    if (field.HasExplicitStorage || !ObjectModelValidator.FieldAttributesSupported(field.Attributes))
                        Fail("Static, literal, marshalled, RVA, explicit-offset and constant field storage require later lowering.", location);
                    ObjectStorage storage;
                    if (EnumScalar(field.Signature)) storage = new ObjectStorage.Int32();
                    else if (field.Signature is TypeSignature.Named { Kind: SignatureTypeKind.ValueType } nested)
                    {
                        IncludeValue(new(nested.Definition, SignatureList<TypeSignature>.Empty), location); storage = new ObjectStorage.Value(Id(nested.Definition));
                    }
                    else if (generics && field.Signature is TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic)
                    {
                        var nestedType = new ConstructedType(generic.Definition.Definition, generic.Arguments);
                        IncludeValue(nestedType, location); storage = new ObjectStorage.Value(ClosedId(nestedType));
                    }
                    else
                    {
                        var converted = generics && field.Signature is TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } genericReference
                            ? IrType.Reference(IncludeClosedReference(new(genericReference.Definition.Definition, genericReference.Arguments)))
                            : ObjectSignatureTypes.Convert(field.Signature, Id(objectBase), IncludeReference,
                            array => { IncludeId(array); arrays.Add(array); return array; },
                            text: () => IncludeReference(strings.Require()), integers64: integers64,
                            closedReference: integers64 ? generic => IncludeClosedReference(new(generic.Definition.Definition, generic.Arguments)) : null);
                        if (converted is null)
                            Fail("Value fields require Int32, supported object references/vectors, or named nongeneric inline structs. Byrefs, pointers, other primitives, generic values and value arrays require later lowering.", location, DiagnosticIds.ExecutionSignatureUnsupported);
                        storage = converted == IrType.Int32 ? new ObjectStorage.Int32() : converted == IrType.Int64
                            ? new ObjectStorage.Int64() : new ObjectStorage.Reference(converted!.Class!);
                    }
                    declared.Add(new(new(id, field.Token), field.Name, field.Attributes, storage));
                }
                values.Add(new(input.Source, input.Attributes, [.. declared]) { ExternallyVisible = input.ExternallyVisible, Interfaces = [.. contracts] });
            }
            ObjectModelBuilder.TypeSnapshot ReadClosedReference(ConstructedType type, TypeReadLimits limits)
            {
                try { return new(ReadClosed(type, limits), []); }
                catch (Failure failure) { return new(null, failure.Diagnostics); }
            }
            referenceResult = generics ? ObjectModelBuilder.BuildClosedCatalog(resolver, [.. references.Values], library, ReadClosedReference, [.. arrays], strings, delegates, integers64, integers64 ? type => { IncludeValue(type, "heap field: " + Label(type)); return ClosedId(type); } : null, EnumScalar)
                : ObjectModelBuilder.BuildCatalog(resolver, [.. references.Values.Select(t => t.Definition)], library, Read, [.. arrays], strings);
            if (referenceResult.Catalog is null) return new(null, referenceResult.Diagnostics);
        } while (pending.Count != 0);
        var catalog = new ValueTypeCatalog([.. roots.Select(ClosedId).Distinct()], Id(valueBase),
            [.. values.OrderBy(t => t.Source.ContentHash, StringComparer.Ordinal).ThenBy(t => t.Source.Token).ThenBy(t => t.Source.Specialization, StringComparer.Ordinal)], referenceResult.Catalog);
        try { _ = ValueLayoutPlanner.Build(catalog); }
        catch (ArgumentException exception)
        {
            Fail(exception.Message, roots.IsEmpty ? "empty inline catalog" : "value inclusion: " + paths[ClosedId(roots[0])]);
        }
        return new(catalog, []);
    }

    private sealed class Failure(ImmutableArray<Diagnostic> diagnostics) : Exception
    {
        internal ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
    }
}
