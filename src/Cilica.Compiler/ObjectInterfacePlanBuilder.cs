using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Prepares bounded interface maps using exact metadata resolution; inspected IL is neither read nor executed.</summary>
public static class ObjectInterfacePlanBuilder
{
    public static ObjectInterfacePlanResult Build(AssemblyResolver resolver, ImmutableArray<ResolvedType> roots,
        ImmutableArray<ResolvedMethod> declarations, ResolvedAssembly runtimeLibrary, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes || declarations.IsDefaultOrEmpty
            || declarations.Length > ProgramLimits.MaximumMethods)
            throw new ArgumentException("Interface plans need 1..256 explicit roots and declarations.");
        budget ??= new();
        try { return new(new Session(resolver, runtimeLibrary, budget).Run(roots, declarations), []); }
        catch (PlanFailure failure) { return new(null, [failure.Diagnostic]); }
        catch (ObjectStringType.Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    public static ObjectInterfacePlanResult BuildClosed(AssemblyResolver resolver, ImmutableArray<ConstructedType> roots,
        ImmutableArray<ResolvedMethod> declarations, ResolvedAssembly runtimeLibrary, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        if (roots.IsDefaultOrEmpty || roots.Length > ObjectModelLimits.MaximumTypes || declarations.IsDefaultOrEmpty
            || declarations.Length > ProgramLimits.MaximumMethods)
            throw new ArgumentException("Closed interface plans need 1..256 explicit roots and declarations.");
        budget ??= new();
        try { return new(new Session(resolver, runtimeLibrary, budget, closed: true).RunClosed(roots, declarations), []); }
        catch (PlanFailure failure) { return new(null, [failure.Diagnostic]); }
        catch (ObjectStringType.Failure failure) { return new(null, [failure.Diagnostic]); }
    }

    internal sealed class PlanFailure(Diagnostic diagnostic, IrMethodId? declaration = null) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
        internal IrMethodId? Declaration { get; } = declaration;
    }
    // A monotonic translation closure: Run receives the complete declaration set on each pass.
    internal sealed class Session(AssemblyResolver resolver, ResolvedAssembly runtimeLibrary, MemberResolutionBudget budget,
        Func<ResolvedType, TypeReadLimits, TypeInput>? readType = null, bool closed = false, Func<TypeSignature, bool, IrType>? signature = null)
    {
        private readonly ObjectStringType _strings = new(resolver, runtimeLibrary, budget);
        private readonly Dictionary<ObjectTypeId, ConstructedType> _roots = [], _knownTypes = [];
        private readonly HashSet<ObjectTypeId> _arrays = [];
        private readonly Dictionary<IrMethodId, MethodDeclarationInput> _inputs = [];
        private readonly Dictionary<IrMethodId, ObjectDispatchMethod> _methods = [];
        private readonly Dictionary<IrMethodId, ObjectInterfaceDeclaration> _declarations = [];
        private readonly Dictionary<IrMethodId, ResolvedMethod> _resolved = [];
        private readonly Dictionary<ObjectTypeId, TypeInput> _typeInputs = [];
        private readonly Dictionary<(IrMethodId Declaration, ObjectTypeId Receiver), ClosedInterfaceMethodTarget> _choices = [];
        private readonly Dictionary<(IrMethodId, ObjectTypeId), IrMethodId> _arrayChoices = [];
        private ObjectTypeId _object = null!;
        private int _parameters;
        private ValueTypeCatalog? _values;

        internal ObjectInterfaceDispatchPlan Run(ImmutableArray<ResolvedType> roots, ImmutableArray<ResolvedMethod> declarations,
            ImmutableArray<ObjectTypeId> arrays = default)
            => RunClosed([.. roots.Select(t => new ConstructedType(t, SignatureList<TypeSignature>.Empty))], declarations, arrays);

        internal ObjectInterfaceDispatchPlan RunClosed(ImmutableArray<ConstructedType> roots, ImmutableArray<ResolvedMethod> declarations,
            ImmutableArray<ObjectTypeId> arrays = default)
        {
            Initialize();
            if (!arrays.IsDefault) foreach (var array in arrays) IncludeArray(array);
            foreach (var root in roots) Include(root);
            var requested = new HashSet<IrMethodId>();
            foreach (var resolved in declarations)
            {
                var declaration = Declare(resolved);
                if (!requested.Add(IrMethodId.From(declaration.Source))) Fail("Duplicate interface declaration requested.", declaration.Source.Name);
            }
            TypeInput Snapshot(ConstructedType type, TypeReadLimits limits)
            {
                var id = ObjectModelBuilder.Id(type); _knownTypes.TryAdd(id, type);
                if (readType is not null) return readType(type.Definition, limits);
                if (_typeInputs.TryGetValue(id, out var cached)) return cached;
                var bounded = limits with { MaximumMembers = Math.Min(limits.MaximumMembers, Remaining()) };
                var read = closed ? resolver.ReadClosedTypeInput(type, runtimeLibrary, bounded)
                    : resolver.ReadTypeInput(type.Definition.Assembly, type.Definition.Handle, bounded);
                var input = Value(read); Consume(input.MetadataMembersRead); _typeInputs.Add(id, input); return input;
            }
            var catalog = Value(closed
                ? ObjectModelBuilder.BuildClosedCatalog(resolver, [.. _roots.Values], runtimeLibrary,
                    (type, limits) => new(Snapshot(type, limits), []), [.. _arrays], _strings, integers64: true)
                : ObjectModelBuilder.BuildCatalog(resolver, [.. _roots.Values.Select(t => t.Definition)], runtimeLibrary,
                    (type, limits) => Snapshot(new(type, SignatureList<TypeSignature>.Empty), limits), [.. _arrays], _strings));
            return Prepare(catalog, declarations);
        }

        internal IEnumerable<ConstructedType> ClosedRoots => _roots.Values;
        internal IEnumerable<ObjectTypeId> Arrays => _arrays;
        internal ObjectInterfaceDispatchPlan RunPrepared(ObjectTypeCatalog catalog,
            IReadOnlyDictionary<ObjectTypeId, ConstructedType> known, ImmutableArray<ResolvedMethod> declarations, ValueTypeCatalog? values = null)
        {
            _values = values;
            foreach (var pair in known) _knownTypes.TryAdd(pair.Key, pair.Value);
            return Prepare(catalog, declarations);
        }

        private ObjectInterfaceDispatchPlan Prepare(ObjectTypeCatalog catalog, ImmutableArray<ResolvedMethod> declarations)
        {
            var types = new IrTypeRules(catalog); var maps = new List<ObjectInterfaceDispatch>(); int targetCount = 0;
            foreach (var resolved in declarations)
            {
                var id = Id(resolved); var declaration = _declarations[id]; var targets = new List<ObjectInterfaceTarget>();
                foreach (var layout in catalog.Types)
                {
                    var receiver = ObjectTypeId.From(layout.Source);
                    if (!types.Assignable(IrType.Reference(receiver), IrType.Reference(declaration.Owner))) continue;
                    if (++targetCount > ObjectDispatchLimits.MaximumTargets) Fail("Interface dispatch target limit exceeded.", limit: true);
                    if (!_choices.TryGetValue((id, receiver), out var exact))
                    {
                        try
                        {
                            if (closed) exact = Value(resolver.ResolveClosedInterfaceMethodTarget(resolved, _knownTypes[receiver], runtimeLibrary, budget));
                            else
                            {
                                var legacy = Value(resolver.ResolveInterfaceMethodTarget(resolved.DeclaringType.Definition.Assembly,
                                    resolved.Handle, _knownTypes[receiver].Definition, budget));
                                exact = new(legacy.Declaration, _knownTypes[receiver], legacy.Mapping, legacy.Slot, legacy.Implementation, legacy.IsExplicit);
                            }
                        }
                        catch (PlanFailure failure) { throw new PlanFailure(failure.Diagnostic, id); }
                        _choices.Add((id, receiver), exact);
                    }
                    ObjectVirtualSlot? slot = null;
                    if (exact.Slot is { } introduction)
                    {
                        slot = new(Id(introduction), ObjectModelBuilder.Id(introduction.DeclaringType));
                        Method(introduction, slot, catalog);
                    }
                    Method(exact.Mapping, slot, catalog); Method(exact.Implementation, slot, catalog);
                    targets.Add(new(receiver, Id(exact.Mapping), Id(exact.Implementation), exact.IsExplicit));
                }
                foreach (var receiver in catalog.Arrays)
                {
                    if (!types.Assignable(IrType.Reference(receiver), IrType.Reference(declaration.Owner))) continue;
                    if (++targetCount > ObjectDispatchLimits.MaximumTargets) Fail("Interface dispatch target limit exceeded.", limit: true);
                    IrMethodId helper;
                    try { helper = ArrayMethod(declaration, receiver, catalog); }
                    catch (PlanFailure failure) { throw new PlanFailure(failure.Diagnostic, id); }
                    targets.Add(new(receiver, helper, helper, false));
                }
                maps.Add(new(id, [.. targets]));
            }
            var plan = new ObjectInterfaceDispatchPlan(catalog,
                [.. _declarations.OrderBy(p => p.Key.ContentHash, StringComparer.Ordinal).ThenBy(p => p.Key.Token).ThenBy(p => p.Key.Specialization, StringComparer.Ordinal).Select(p => p.Value)],
                [.. _methods.OrderBy(p => p.Key.ContentHash, StringComparer.Ordinal).ThenBy(p => p.Key.Token).ThenBy(p => p.Key.Specialization, StringComparer.Ordinal).Select(p => p.Value)],
                [.. maps.OrderBy(m => m.Declaration.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Declaration.Token).ThenBy(m => m.Declaration.Specialization, StringComparer.Ordinal)]) { Values = _values };
            try { ObjectInterfacePlanValidator.Validate(plan); }
            catch (ArgumentException exception) { Fail(exception.Message); }
            return plan;
        }

        internal ObjectInterfaceDeclaration Declare(ResolvedMethod method)
        {
            Initialize(); var input = Read(method); var id = IrMethodId.From(input.Source);
            if (_declarations.TryGetValue(id, out var cached)) return cached;
            var owner = Include(method.DeclaringType);
            const MethodAttributes required = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract;
            const MethodAttributes allowed = required | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
            if ((input.OwnerAttributes & (TypeAttributes.Interface | TypeAttributes.Abstract)) != (TypeAttributes.Interface | TypeAttributes.Abstract)
                || (input.Attributes & required) != required || (input.Attributes & ~allowed) != 0 || input.HasBody || !input.OwnerExternallyVisible)
                Fail("Interface declarations require public abstract instance contracts without a body.", input.Source.Name);
            var parameters = Parameters(input, owner); var returned = Type(input.Signature.ReturnType, true);
            var declaration = new ObjectInterfaceDeclaration(input.Source, owner, input.DeclaredName,
                input.Attributes, input.OwnerExternallyVisible, parameters, returned);
            Count(parameters.Length); _declarations.Add(id, declaration); return declaration;
        }
        internal ResolvedMethod Resolved(IrMethodId id) => _resolved[id];
        internal ObjectDispatchMethod? Fact(IrMethodId id) => _methods.GetValueOrDefault(id);
        private void Initialize()
        {
            if (_object is null) _object = Include(Value(resolver.ResolveRuntimeType(runtimeLibrary, PrimitiveTypeCode.Object)));
        }

        private ObjectTypeId Include(ResolvedType type) => Include(new ConstructedType(type, SignatureList<TypeSignature>.Empty));
        private ObjectTypeId Include(ConstructedType type)
        {
            ArgumentNullException.ThrowIfNull(type);
            Value(resolver.ResolveType(type.Definition.Assembly, type.Definition.Handle)); // Check ownership before identity deduplication.
            ObjectTypeId id;
            try { id = ObjectModelBuilder.Id(type); }
            catch (ArgumentException error) { throw new PlanFailure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error, error.Message)); }
            if (!_roots.ContainsKey(id) && _roots.Count + _arrays.Count >= ObjectModelLimits.MaximumTypes) Fail("Interface catalog root limit exceeded.", limit: true);
            _roots.TryAdd(id, type); return id;
        }
        private MethodDeclarationInput Read(ResolvedMethod method, bool arrayHelper = false)
        {
            ArgumentNullException.ThrowIfNull(method);
            var owner = method.DeclaringType.Definition;
            Value(resolver.ResolveType(owner.Assembly, owner.Handle));
            if (!closed && method.DeclaringType.Arguments.Count != 0 || method.MethodArguments.Count != 0) Fail("Constructed generic dispatch requires later lowering.");
            var id = Id(method);
            if (_inputs.TryGetValue(id, out var cached)) return cached;
            if (_inputs.Count >= ProgramLimits.MaximumMethods) Fail("Interface declarations/class facts share the method limit.", limit: true);
            var input = Value(resolver.ReadMethodDeclarationInput(owner.Assembly, method.Handle, new(MaximumMembers: Remaining())));
            Consume(input.MetadataMembersRead);
            if (input.OwnerToken != MetadataTokens.GetToken(owner.Handle)) Fail("Resolved method owner contradicts its declaration.", input.Source.Name);
            if (input.OwnerGenericArity != method.DeclaringType.Arguments.Count || input.Signature.GenericArity != 0 || input.Signature.Header != (arrayHelper ? 0 : 0x20)
                || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer)
                Fail("Interface dispatch preparation requires exact admitted instance owners without generic method parameters or initialization.", input.Source.Name);
            if (closed)
            {
                var authenticated = Value(resolver.ResolveClosedCall(method, method.Handle, budget));
                if (authenticated != method) Fail("Closed dispatch signature contradicts its metadata declaration.", input.Source.Name);
                input = input with { Source = input.Source with { Specialization = id.Specialization }, Signature = method.Signature };
            }
            _inputs.Add(id, input); _resolved.Add(id, method); return input;
        }
        private void Method(ResolvedMethod method, ObjectVirtualSlot? slot, ObjectTypeCatalog catalog)
        {
            var input = Read(method); var id = Id(method); var owner = ObjectModelBuilder.Id(method.DeclaringType);
            if (!input.HasBody || (input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask
                | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL)
                Fail("Selected class method requires an ordinary managed IL body; its instructions still need translation.", input.Source.Name);
            if (_methods.TryGetValue(id, out var known))
            {
                if (known.Binding.VirtualSlot != slot) Fail("Exact interface choices disagree on a canonical class slot.", input.Source.Name);
                return;
            }
            var parameters = Parameters(input, owner); var returned = Type(input.Signature.ReturnType, true);
            var types = new IrTypeRules(catalog, _values, locations: catalog.Integer64);
            if (parameters.Any(t => !types.Valid(t, true)) || returned != IrType.Void && !types.Valid(returned, true))
                Fail("Mapped method signature introduced a type outside the prepared catalog.", input.Source.Name);
            Count(parameters.Length);
            _methods.Add(id, new(new(id, owner, ObjectMethodKind.Instance, input.Attributes, input.OwnerExternallyVisible)
                { LexicalOwners = catalog.Integer64 ? input.LexicalOwners : [], VirtualSlot = slot, VirtualName = slot is null ? null : input.DeclaredName,
                    OwnerAssemblyVisible = closed && input.OwnerAssemblyVisible }, input.DeclaredName, parameters, returned));
        }
        private IrMethodId ArrayMethod(ObjectInterfaceDeclaration declaration, ObjectTypeId receiver, ObjectTypeCatalog catalog)
        {
            var key = (IrMethodId.From(declaration.Source), receiver);
            if (_arrayChoices.TryGetValue(key, out var cached)) return cached;
            var contract = catalog.Interfaces.Single(i => ObjectTypeId.From(i.Source) == declaration.Owner);
            var recipe = ObjectArrayInterfaceRecipes.Select(declaration, contract.ArrayContract, catalog.ObjectBase);
            if (!closed || !catalog.Integer64 || recipe is null)
                Fail("Array interface member requires an explicit runtime helper contract; only Count, indexed reads/writes, fixed-size Add, collection flags and SyncRoot are admitted.", declaration.Source.Name);
            // Writes use the interface's element view so covariant array stores
            // retain the actual receiver's runtime type check after bounds checks.
            var view = receiver;
            if (recipe is "Add" or "Set")
            {
                var item = declaration.Parameters[^1];
                if (item != IrType.Int32 && item.Kind is not (IrTypeKind.Reference or IrTypeKind.Value))
                    Fail("Array interface mutation requires Int32, reference or inline value elements.", declaration.Source.Name);
                view = ObjectTypeId.Vector(catalog.ObjectBase, item == IrType.Int32 ? ObjectArrayElement.Int32 : item.Kind == IrTypeKind.Value ? ObjectArrayElement.Value(item.Class!) : ObjectArrayElement.Reference(item.Class!));
            }
            TypeSignature element;
            if (view.ArrayElement!.Kind == ObjectArrayElementKind.Int32) element = new TypeSignature.Primitive(PrimitiveTypeCode.Int32);
            else if (view.ArrayElement.Type == catalog.ObjectBase) element = new TypeSignature.Primitive(PrimitiveTypeCode.Object);
            else if (view.ArrayElement.Type == catalog.String) element = new TypeSignature.Primitive(PrimitiveTypeCode.String);
            else
            {
                if (!_knownTypes.TryGetValue(view.ArrayElement.Type!, out var type))
                    throw new PlanFailure(new(DiagnosticIds.ExecutionUnsupported, DiagnosticSeverity.Error, "Missing closed vector element metadata."));
                var named = new TypeSignature.Named(type.Definition, view.ArrayElement.Kind == ObjectArrayElementKind.Value ? SignatureTypeKind.ValueType : SignatureTypeKind.Class);
                element = type.Arguments.Count == 0 ? named : new TypeSignature.Generic(named, type.Arguments);
            }
            var owner = Value(resolver.ResolveType(runtimeLibrary, "Cilica.Runtime", "ArrayInterface`1"));
            var method = Value(resolver.ResolveClosedDeclaration(new(owner, new([element])), recipe!, ObjectArrayInterfaceRecipes.Arity(recipe!), budget));
            _ = Value(resolver.AuthorizeLibraryIntrinsic(runtimeLibrary, method.Handle,
                recipe switch { "Count" => LibraryIntrinsicCapability.ArrayInterfaceCount, "Get" => LibraryIntrinsicCapability.ArrayInterfaceGet,
                    "Add" => LibraryIntrinsicCapability.ArrayInterfaceAdd, "Set" => LibraryIntrinsicCapability.ArrayInterfaceSet,
                    "False" => LibraryIntrinsicCapability.ArrayInterfaceFalse, "True" => LibraryIntrinsicCapability.ArrayInterfaceTrue, _ => LibraryIntrinsicCapability.ArrayInterfaceIdentity }));
            var input = Read(method, arrayHelper: true); var id = Id(method);
            var parameters = input.Signature.Parameters.Select(p => Type(p)).ToImmutableArray();
            var returned = Type(input.Signature.ReturnType, true);
            if (!input.HasBody || input.Implementation != MethodImplAttributes.IL
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.OwnerGenericArity != 1 || parameters.Length != (ObjectArrayInterfaceRecipes.Arity(recipe!))
                || parameters[0] != IrType.Reference(view) || recipe is ("Get" or "Set") && parameters[1] != IrType.Int32
                || recipe is ("Add" or "Set") && parameters[^1] != ObjectArrayInterfaceRecipes.Result("Get", view)
                || returned != ObjectArrayInterfaceRecipes.Result(recipe!, view))
                Fail("Invalid selected array interface helper declaration.", input.Source.Name);
            if (!_methods.ContainsKey(id))
            {
                Count(parameters.Length);
                _methods.Add(id, new(new(id, ObjectModelBuilder.Id(method.DeclaringType), ObjectMethodKind.Static,
                    input.Attributes, input.OwnerExternallyVisible) { LexicalOwners = input.LexicalOwners, OwnerAssemblyVisible = input.OwnerAssemblyVisible },
                    input.DeclaredName, parameters, returned) { ArrayHelper = true });
            }
            _arrayChoices.Add(key, id); return id;
        }
        private ImmutableArray<IrType> Parameters(MethodDeclarationInput input, ObjectTypeId owner)
        {
            if (input.Signature.Parameters.Count >= IrLimits.MaximumParameters) Fail("Dispatch declaration parameter limit exceeded.", input.Source.Name, true);
            return [IrType.Reference(owner), .. input.Signature.Parameters.Select(p => Type(p))];
        }
        private ObjectTypeId IncludeArray(ObjectTypeId array)
        {
            if (!_arrays.Contains(array) && _roots.Count + _arrays.Count >= ObjectModelLimits.MaximumTypes)
                Fail("Interface catalog root limit exceeded.", limit: true);
            _arrays.Add(array); return array;
        }
        private IrType Type(TypeSignature type, bool allowVoid = false) => signature is not null ? signature(type, allowVoid) :
            ObjectSignatureTypes.Convert(type, _object, Include, IncludeArray, allowVoid, () => Include(_strings.Require()), integers64: closed,
                closedReference: closed ? generic => Include(new ConstructedType(generic.Definition.Definition, generic.Arguments)) : null)
            ?? throw new PlanFailure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error, ObjectSignatureTypes.Unsupported));
        private void Count(int count) { _parameters += count; if (_parameters > ProgramLimits.MaximumCallArguments) Fail("Aggregate dispatch parameter limit exceeded.", limit: true); }
        private int Remaining() { if (budget.Remaining == 0) Fail("Interface preparation metadata budget exhausted.", limit: true); return budget.Remaining; }
        private void Consume(int work) { if (!budget.TryConsume(work)) Fail("Interface preparation metadata budget exhausted.", limit: true); }
        private static IrMethodId Id(ResolvedMethod method)
        {
            try { return new(method.DeclaringType.Definition.Assembly.ContentHash, MetadataTokens.GetToken(method.Handle),
                ClosedGenericKey.Create(method.DeclaringType.Arguments, method.MethodArguments)); }
            catch (ArgumentException error) { throw new PlanFailure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error, error.Message)); }
        }
        private static T Value<T>(ResolutionResult<T> result) where T : class => result.Value ?? throw new PlanFailure(result.Diagnostics[0]);
        private static ObjectTypeCatalog Value(ObjectCatalogResult result) => result.Catalog ?? throw new PlanFailure(result.Diagnostics[0]);
        private static void Fail(string message, string? location = null, bool limit = false) =>
            throw new PlanFailure(new(limit ? DiagnosticIds.TranslationLimit : DiagnosticIds.ExecutionUnsupported, DiagnosticSeverity.Error, message, location));
    }
}
