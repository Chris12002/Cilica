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

    internal sealed class PlanFailure(Diagnostic diagnostic, IrMethodId? declaration = null) : Exception(diagnostic.Message)
    {
        internal Diagnostic Diagnostic { get; } = diagnostic;
        internal IrMethodId? Declaration { get; } = declaration;
    }
    // A monotonic translation closure: Run receives the complete declaration set on each pass.
    internal sealed class Session(AssemblyResolver resolver, ResolvedAssembly runtimeLibrary, MemberResolutionBudget budget,
        Func<ResolvedType, TypeReadLimits, TypeInput>? readType = null)
    {
        private readonly ObjectStringType _strings = new(resolver, runtimeLibrary, budget);
        private readonly Dictionary<ObjectTypeId, ResolvedType> _roots = [], _knownTypes = [];
        private readonly HashSet<ObjectTypeId> _arrays = [];
        private readonly Dictionary<IrMethodId, MethodDeclarationInput> _inputs = [];
        private readonly Dictionary<IrMethodId, ObjectDispatchMethod> _methods = [];
        private readonly Dictionary<IrMethodId, ObjectInterfaceDeclaration> _declarations = [];
        private readonly Dictionary<IrMethodId, ResolvedMethod> _resolved = [];
        private readonly Dictionary<ObjectTypeId, TypeInput> _typeInputs = [];
        private readonly Dictionary<(IrMethodId Declaration, ObjectTypeId Receiver), InterfaceMethodTarget> _choices = [];
        private ObjectTypeId _object = null!;
        private int _parameters;

        internal ObjectInterfaceDispatchPlan Run(ImmutableArray<ResolvedType> roots, ImmutableArray<ResolvedMethod> declarations,
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
            var catalog = Value(ObjectModelBuilder.BuildCatalog(resolver, [.. _roots.Values], runtimeLibrary, (type, limits) =>
            {
                _knownTypes.TryAdd(ObjectModelBuilder.Id(type), type);
                if (readType is not null) return readType(type, limits);
                if (_typeInputs.TryGetValue(ObjectModelBuilder.Id(type), out var cached)) return cached;
                var read = resolver.ReadTypeInput(type.Assembly, type.Handle, limits with { MaximumMembers = Remaining() });
                var input = Value(read); Consume(input.MetadataMembersRead); _typeInputs.Add(ObjectModelBuilder.Id(type), input); return input;
            }, [.. _arrays], _strings));
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
                        try { exact = Value(resolver.ResolveInterfaceMethodTarget(resolved.DeclaringType.Definition.Assembly,
                            resolved.Handle, _knownTypes[receiver], budget)); }
                        catch (PlanFailure failure) { throw new PlanFailure(failure.Diagnostic, id); }
                        _choices.Add((id, receiver), exact);
                    }
                    ObjectVirtualSlot? slot = null;
                    if (exact.Slot is { } introduction)
                    {
                        slot = new(Id(introduction), ObjectModelBuilder.Id(introduction.DeclaringType.Definition));
                        Method(introduction, slot, catalog);
                    }
                    Method(exact.Mapping, slot, catalog); Method(exact.Implementation, slot, catalog);
                    targets.Add(new(receiver, Id(exact.Mapping), Id(exact.Implementation), exact.IsExplicit));
                }
                maps.Add(new(id, [.. targets]));
            }
            var plan = new ObjectInterfaceDispatchPlan(catalog,
                [.. _declarations.OrderBy(p => p.Key.ContentHash, StringComparer.Ordinal).ThenBy(p => p.Key.Token).Select(p => p.Value)],
                [.. _methods.OrderBy(p => p.Key.ContentHash, StringComparer.Ordinal).ThenBy(p => p.Key.Token).Select(p => p.Value)],
                [.. maps.OrderBy(m => m.Declaration.ContentHash, StringComparer.Ordinal).ThenBy(m => m.Declaration.Token)]);
            try { ObjectInterfacePlanValidator.Validate(plan); }
            catch (ArgumentException exception) { Fail(exception.Message); }
            return plan;
        }

        internal ObjectInterfaceDeclaration Declare(ResolvedMethod method)
        {
            Initialize(); var input = Read(method); var id = IrMethodId.From(input.Source);
            if (_declarations.TryGetValue(id, out var cached)) return cached;
            var owner = Include(method.DeclaringType.Definition);
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

        private ObjectTypeId Include(ResolvedType type)
        {
            ArgumentNullException.ThrowIfNull(type);
            Value(resolver.ResolveType(type.Assembly, type.Handle)); // Check ownership before identity deduplication.
            var id = ObjectModelBuilder.Id(type);
            if (!_roots.ContainsKey(id) && _roots.Count + _arrays.Count >= ObjectModelLimits.MaximumTypes) Fail("Interface catalog root limit exceeded.", limit: true);
            _roots.TryAdd(id, type); return id;
        }
        private MethodDeclarationInput Read(ResolvedMethod method)
        {
            ArgumentNullException.ThrowIfNull(method);
            var owner = method.DeclaringType.Definition;
            Value(resolver.ResolveType(owner.Assembly, owner.Handle));
            if (method.DeclaringType.Arguments.Count != 0 || method.MethodArguments.Count != 0) Fail("Constructed generic dispatch requires later lowering.");
            var id = Id(method);
            if (_inputs.TryGetValue(id, out var cached)) return cached;
            if (_inputs.Count >= ProgramLimits.MaximumMethods) Fail("Interface declarations/class facts share the method limit.", limit: true);
            var input = Value(resolver.ReadMethodDeclarationInput(owner.Assembly, method.Handle, new(MaximumMembers: Remaining())));
            Consume(input.MetadataMembersRead);
            if (input.OwnerToken != MetadataTokens.GetToken(owner.Handle)) Fail("Resolved method owner contradicts its declaration.", input.Source.Name);
            if (input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0 || input.Signature.Header != 0x20
                || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer)
                Fail("Interface dispatch preparation requires nongeneric instance implementation metadata without initialization.", input.Source.Name);
            _inputs.Add(id, input); _resolved.Add(id, method); return input;
        }
        private void Method(ResolvedMethod method, ObjectVirtualSlot? slot, ObjectTypeCatalog catalog)
        {
            var input = Read(method); var id = Id(method); var owner = ObjectModelBuilder.Id(method.DeclaringType.Definition);
            if (!input.HasBody || (input.Implementation & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.ManagedMask
                | MethodImplAttributes.InternalCall | MethodImplAttributes.Synchronized)) != MethodImplAttributes.IL)
                Fail("Selected class method requires an ordinary managed IL body; its instructions still need translation.", input.Source.Name);
            if (_methods.TryGetValue(id, out var known))
            {
                if (known.Binding.VirtualSlot != slot) Fail("Exact interface choices disagree on a canonical class slot.", input.Source.Name);
                return;
            }
            var parameters = Parameters(input, owner); var returned = Type(input.Signature.ReturnType, true);
            if (parameters.Append(returned).Any(t => t.Class is { } type && !catalog.Types.Any(c => ObjectTypeId.From(c.Source) == type)
                && !catalog.Interfaces.Any(c => ObjectTypeId.From(c.Source) == type) && !catalog.Arrays.Contains(type) && type != catalog.String))
                Fail("Mapped method signature introduced a type outside the prepared catalog.", input.Source.Name);
            Count(parameters.Length);
            _methods.Add(id, new(new(id, owner, ObjectMethodKind.Instance, input.Attributes, input.OwnerExternallyVisible)
                { VirtualSlot = slot, VirtualName = slot is null ? null : input.DeclaredName }, input.DeclaredName, parameters, returned));
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
        private IrType Type(TypeSignature signature, bool allowVoid = false) =>
            ObjectSignatureTypes.Convert(signature, _object, Include, IncludeArray, allowVoid, () => Include(_strings.Require()))
            ?? throw new PlanFailure(new(DiagnosticIds.ExecutionSignatureUnsupported, DiagnosticSeverity.Error, ObjectSignatureTypes.Unsupported));
        private void Count(int count) { _parameters += count; if (_parameters > ProgramLimits.MaximumCallArguments) Fail("Aggregate dispatch parameter limit exceeded.", limit: true); }
        private int Remaining() { if (budget.Remaining == 0) Fail("Interface preparation metadata budget exhausted.", limit: true); return budget.Remaining; }
        private void Consume(int work) { if (!budget.TryConsume(work)) Fail("Interface preparation metadata budget exhausted.", limit: true); }
        private static IrMethodId Id(ResolvedMethod method) => new(method.DeclaringType.Definition.Assembly.ContentHash, MetadataTokens.GetToken(method.Handle));
        private static T Value<T>(ResolutionResult<T> result) where T : class => result.Value ?? throw new PlanFailure(result.Diagnostics[0]);
        private static ObjectTypeCatalog Value(ObjectCatalogResult result) => result.Catalog ?? throw new PlanFailure(result.Diagnostics[0]);
        private static void Fail(string message, string? location = null, bool limit = false) =>
            throw new PlanFailure(new(limit ? DiagnosticIds.TranslationLimit : DiagnosticIds.ExecutionUnsupported, DiagnosticSeverity.Error, message, location));
    }
}
