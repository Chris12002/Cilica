using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace Cilica.Compiler;

public static class ObjectIrValidator
{
    public static void Validate(ObjectIrProgram program) => Validate(program, locations: false);

    internal static void Validate(ObjectIrProgram program, bool locations, ScalarBoxingRules? boxes = null, bool delegates = false, ManagedExceptionRules? exceptions = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ObjectModelValidator.Validate(program.Flow.Types);
        _ = new ObjectNativeLayout(program.Flow.Types);
        var rules = new ObjectIrRules(program, locations, delegates: delegates, boxes: boxes);
        ReferenceIrValidator.Validate(program.Flow, rules, locations ? new ManagedLocationRules(program.Flow.Types) : null, boxes, exceptions);
        rules.DispatchMaps();
        rules.ConstructorChains();
        var entry = rules.Binding(program.Flow.Entry);
        var method = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        if (entry.Kind != ObjectMethodKind.Static || method.ReturnType != IrType.Int32 || method.Parameters.Any(p => p != IrType.Int32))
            throw new ArgumentException("Object program entry requires a static Int32 boundary; references stay internal.", nameof(program));
    }
}

internal sealed partial class ObjectIrRules
{
    private readonly Dictionary<IrMethodId, ObjectMethodBinding> _bindings = [];
    private readonly Dictionary<IrMethodId, IrMethod> _methods = [];
    private readonly HashSet<IrMethodId> _abstractMethods = [];
    private readonly Dictionary<ObjectFieldId, ObjectField> _fields;
    private readonly Dictionary<ObjectFieldId, ObjectStaticField> _statics;
    private readonly Dictionary<ObjectTypeId, ObjectTypeLayout> _layouts;
    private readonly HashSet<ObjectTypeId> _contracts;
    private readonly HashSet<ObjectTypeId> _arrays;
    private readonly Dictionary<ObjectTypeId, ValueTypeLayout>? _valueLayouts;
    private readonly Dictionary<ObjectTypeId, ValueTypeLayout>? _arrayValueLayouts;
    private readonly HashSet<ObjectTypeId> _scalarTypeTargets;
    private readonly bool _enumQueryReady;
    private readonly HashSet<ObjectTypeId> _enumEntryTypes;
    private readonly IrTypeRules _types;
    private readonly ObjectDispatchRules _dispatch;
    private readonly ObjectInterfaceOperationRules _interfaces;
    private readonly ObjectIrProgram _program;
    private readonly bool _allowDelegates;
    private readonly Dictionary<ObjectTypeId, ObjectDelegateType> _delegates;
    private readonly HashSet<string> _literals = new(StringComparer.Ordinal);
    private int _literalUnits;

    internal ObjectIrRules(ObjectIrProgram program, bool locations = false, ValueTypeCatalog? values = null, bool delegates = false, ScalarBoxingRules? boxes = null)
    {
        _program = program;
        _statics = program.Flow.Types.Statics.ToDictionary(f => f.Id);
        var flow = program.Flow;
        _allowDelegates = delegates;
        Require(delegates || flow.Types.Delegates.IsEmpty, "delegate catalogs require the explicit delegate IR profile.");
        _delegates = flow.Types.Delegates.ToDictionary(d => ObjectTypeId.From(d.Source));
        _types = new(flow.Types, values, locations);
        _layouts = flow.Types.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        _contracts = flow.Types.Interfaces.Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
        _arrays = flow.Types.Arrays.ToHashSet();
        _valueLayouts = values?.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        _arrayValueLayouts = _valueLayouts ?? boxes?.Catalog.Types.ToDictionary(t => ObjectTypeId.From(t.Source));
        _scalarTypeTargets = values is not null
            ? values.Types.Where(t => t.ScalarBox is not null).Select(t => ObjectTypeId.From(t.Source)).ToHashSet()
            : boxes is not null ? boxes.Plans.Where(p => p.ScalarBox is not null).Select(p => p.Type).ToHashSet() : [];
        _valueKeyCatalog = values ?? boxes?.Catalog;
        _valueKeyScalar = values is null && boxes is not null;
        var enumCatalog = values ?? boxes?.Catalog;
        _enumEntryTypes = enumCatalog is null ? [] : enumCatalog.Types.Where(t => t.ScalarEnum && !t.EnumConstants.IsDefault).Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
        _enumQueryReady = enumCatalog is not null && enumCatalog.Types.All(t => !t.ScalarEnum || !t.EnumConstants.IsDefault);
        if (!flow.Methods.IsDefault && flow.Methods.Any(m => m is not null && !m.Blocks.IsDefault
            && m.Blocks.Any(b => b is not null && !b.Statements.IsDefault && b.Values.Any(v => v.Expression is IrExpression.EnumQuery))))
        {
            Require(flow.Types.Integer64 && _enumQueryReady && flow.Types.String is not null, "enum queries require complete bounded enum metadata and String storage.");
            EnumQueryMetadata.Validate(enumCatalog!);
        }
        _fields = flow.Types.Types.SelectMany(t => t.Fields).ToDictionary(f => f.Id);
        Require(!flow.Methods.IsDefault && flow.Methods.Length is > 0 and <= ProgramLimits.MaximumMethods
            && !program.AbstractMethods.IsDefault && flow.Methods.Length + program.AbstractMethods.Length <= ProgramLimits.MaximumMethods
            && !program.Bindings.IsDefault && program.Bindings.Length == flow.Methods.Length + program.AbstractMethods.Length, "bounded methods and matching bindings are required.");
        foreach (var method in flow.Methods)
        {
            Require(method?.Source is not null && method.Source.ContentHash is { Length: 64 } && CanonicalHash(method.Source.ContentHash.ToLowerInvariant()), "invalid method source.");
            Require(_methods.TryAdd(IrMethodId.From(method.Source), method), "duplicate method identity.");
        }
        Require(program.AbstractMethods.IsEmpty || flow.Types.Integer64, "abstract class signatures require the integer-object profile.");
        int abstractParameters = 0;
        foreach (var declaration in program.AbstractMethods)
        {
            Require(declaration?.Source is { } source && CanonicalHash(source.ContentHash)
                && (uint)source.Token >> 24 == 0x06 && (source.Token & 0xffffff) != 0
                && source.Name is not null && source.AssemblyIdentity is not null
                && ObjectModelValidator.Specialization(source.Specialization)
                && !declaration.Parameters.IsDefault && declaration.Parameters.Length is > 0 and <= IrLimits.MaximumParameters
                && declaration.Parameters.All(p => p is not null && (p.Kind is IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Reference || values is not null && p.Kind == IrTypeKind.Value && _types.Valid(p, false)))
                && declaration.ReturnType is { Kind: IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Reference or IrTypeKind.Void },
                "invalid abstract class signature/provenance.");
            var id = IrMethodId.From(declaration.Source);
            Require(_methods.TryAdd(id, declaration.Signature) && _abstractMethods.Add(id), "abstract declaration duplicates a method identity.");
            abstractParameters += declaration.Parameters.Length;
        }
        Require(program.AbstractMethods.IsEmpty || flow.Methods.Sum(m => m.Parameters.Length) + abstractParameters <= IrLimits.MaximumParameters,
            "abstract signatures and executable bodies share the parameter limit.");
        var initializerOwners = new HashSet<ObjectTypeId>();
        var initializerNameUnits = 0;
        var primitiveCodes = new Dictionary<PrimitiveTypeCode, ObjectTypeId>();
        var primitiveOwners = new Dictionary<ObjectTypeId, PrimitiveReceiver>();
        foreach (var binding in program.Bindings)
        {
            Require(binding?.Method is not null && binding.Owner is not null && binding.Owner.ArrayElement is null && ObjectModelValidator.Specialization(binding.Owner.Specialization) && CanonicalHash(binding.Owner.ContentHash)
                && (uint)binding.Owner.Token >> 24 == 0x02 && (binding.Owner.Token & 0xffffff) > 1
                && binding.Owner.ContentHash == binding.Method.ContentHash
                && _methods.ContainsKey(binding.Method) && _bindings.TryAdd(binding.Method, binding), "invalid or duplicate method binding identity.");
            Require((binding.Owner != flow.Types.String || flow.Types.Integer64
                    && binding.Kind is ObjectMethodKind.Static or ObjectMethodKind.Instance
                    && binding.TypeInitializerName is null && binding.VirtualSlot is null
                    && (binding.Attributes & (MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final
                        | MethodAttributes.Abstract | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0)
                && !_delegates.ContainsKey(binding.Owner), "selected String bodies require ordinary nonvirtual methods; delegates require explicit operations.");
            Require(ObjectLexicalAccess.Valid(binding.Owner, binding.LexicalOwners)
                && (binding.LexicalOwners.IsEmpty || ObjectLexicalAccess.Public(binding.LexicalOwners) == binding.OwnerExternallyVisible
                    && ObjectLexicalAccess.AssemblyVisible(binding.LexicalOwners) == binding.OwnerAssemblyVisible),
                "lexical owner visibility contradicts the method declaration.");
            var method = _methods[binding.Method];
            Require(!method.Parameters.IsDefault && method.Parameters.Length <= IrLimits.MaximumParameters
                && method.Parameters.All(p => _types.Valid(p, true)) && (_types.Valid(method.ReturnType, true) || method.ReturnType == IrType.Void)
                && binding.Kind is ObjectMethodKind.Static or ObjectMethodKind.Instance
                or ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor or ObjectMethodKind.ValueConstructor or ObjectMethodKind.ValueInstance or ObjectMethodKind.PrimitiveInstance, "invalid bound signature/kind.");
            Require((binding.Attributes & ~(MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.Abstract
                | (flow.Types.Integer64 && binding.Kind == ObjectMethodKind.Instance
                    && (binding.Attributes & (MethodAttributes.MemberAccessMask | MethodAttributes.Virtual)) == (MethodAttributes.Assembly | MethodAttributes.Virtual)
                    ? MethodAttributes.CheckAccessOnOverride : 0))) == 0
                && (binding.Attributes & MethodAttributes.MemberAccessMask) is > MethodAttributes.PrivateScope and <= MethodAttributes.Public,
                "native and unsupported method flags require later lowering.");
            bool abstractMethod = _abstractMethods.Contains(binding.Method);
            Require(abstractMethod == ((binding.Attributes & MethodAttributes.Abstract) != 0), "abstract flags and bodyless declarations disagree.");
            if (abstractMethod)
                Require(binding.Kind == ObjectMethodKind.Instance && binding.TypeInitializerName is null && !binding.InvariantProviderOnly
                    && (binding.Attributes & (MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.RTSpecialName)) == MethodAttributes.Virtual
                    && ((binding.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                        || flow.Types.Integer64 && (binding.Attributes & MethodAttributes.MemberAccessMask) is MethodAttributes.Assembly or MethodAttributes.Family)
                    && _layouts.TryGetValue(binding.Owner, out var abstractOwner) && (abstractOwner.Attributes & TypeAttributes.Abstract) != 0,
                    "abstract declarations require admitted public/internal/protected virtual instance slots on abstract class owners.");
            bool isStatic = binding.Kind == ObjectMethodKind.Static;
            if (binding.TypeInitializerName is { } initializerName)
                Require(flow.Types.Integer64 && initializerName.Length is > 0 and <= ObjectStringLimits.MaximumLiteralCodeUnits
                    && (initializerNameUnits += initializerName.Length) <= ObjectStringLimits.MaximumTotalLiteralCodeUnits
                    && initializerOwners.Add(binding.Owner) && initializerOwners.Count <= ObjectModelLimits.MaximumTypes
                    && binding.Method != flow.Entry && isStatic && method.Parameters.IsEmpty && method.ReturnType == IrType.Void
                    && binding.Attributes == (MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName),
                    "type initializers require distinct closed owners, bounded names and exact private static void signatures/flags.");
            bool valueConstructor = binding.Kind == ObjectMethodKind.ValueConstructor;
            bool valueInstance = binding.Kind == ObjectMethodKind.ValueInstance;
            bool primitiveInstance = binding.Kind == ObjectMethodKind.PrimitiveInstance;
            Require(!binding.ReadOnlyReceiver || valueInstance || primitiveInstance, "readonly receiver evidence requires an ordinary value or primitive instance method.");
            Require(!binding.ThrowingIntegerParser || binding.IntegerParser is not null, "throwing integer parser evidence requires a selected parser identity.");
            if (binding.IntegerParser is { } parser)
            {
                bool implicitStyle = binding.ThrowingIntegerParser && (parser.Code == PrimitiveTypeCode.Int64 || method.Parameters.Length == 2);
                Require(flow.Types.Integer64 && isStatic && !binding.InvariantProviderOnly && binding.Primitive is null
                    && binding.OwnerExternallyVisible && binding.Owner.Specialization == "" && binding.Owner.ContentHash == flow.Types.ObjectBase.ContentHash
                    && (parser.Code == PrimitiveTypeCode.Int32 && parser.Source is { Name: "System.Int32" }
                        || parser.Code == PrimitiveTypeCode.Int64 && parser.Source is { Name: "System.Int64" })
                    && ObjectTypeId.From(parser.Source) == binding.Owner && binding.TypeInitializerName is null && binding.VirtualSlot is null
                    && binding.Attributes == (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                    && method.Parameters.Length == (implicitStyle ? 2 : binding.ThrowingIntegerParser ? 3 : 4) && method.Parameters[0] == StringType
                    && (implicitStyle || method.Parameters[1] == IrType.Int32)
                    && method.Parameters[implicitStyle ? 1 : 2].Class is { } parserProvider && _program.Flow.Types.Interfaces.Any(i => ObjectTypeId.From(i.Source) == parserProvider
                        && i.Source.Name == "System.IFormatProvider" && parserProvider.ContentHash == flow.Types.ObjectBase.ContentHash)
                    && (binding.ThrowingIntegerParser || method.Parameters[3] == IrType.Location(parser.Code == PrimitiveTypeCode.Int32 ? IrType.Int32 : IrType.Int64))
                    && method.ReturnType == (binding.ThrowingIntegerParser && parser.Code == PrimitiveTypeCode.Int64 ? IrType.Int64 : IrType.Int32),
                    "integer parsing requires the selected static Int32/Int64 declaration and exact String/style/provider signature with the matching result/output mode.");
            }
            if (binding.InvariantProviderOnly)
                Require((primitiveInstance && binding.Primitive?.Code != PrimitiveTypeCode.Char
                    || binding.Kind == ObjectMethodKind.ValueInstance && _valueLayouts is not null
                        && _valueLayouts.TryGetValue(binding.Owner, out var decimalLayout)
                        && decimalLayout.Source.Name == "System.Decimal" && binding.Owner.ContentHash == flow.Types.ObjectBase.ContentHash
                        && binding.Attributes == (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final))
                    && method.Parameters.Length == 2 && method.ReturnType == StringType
                    && method.Parameters[1].Class is { } provider && _program.Flow.Types.Interfaces.Any(i => ObjectTypeId.From(i.Source) == provider
                        && i.Source.Name == "System.IFormatProvider" && provider.ContentHash == flow.Types.ObjectBase.ContentHash),
                    "invariant provider contract requires the selected primitive or Decimal method and provider signature.");
            Require(primitiveInstance == (binding.Primitive is not null), "primitive receiver evidence/kind mismatch.");
            Require(!method.Parameters.Any(p => p.Kind == IrTypeKind.ManagedLocation)
                || (isStatic || valueConstructor) && binding.VirtualSlot is null && (binding.Attributes & MethodAttributes.Virtual) == 0
                || (valueInstance || primitiveInstance) && binding.VirtualSlot is null
                || flow.Types.Integer64 && binding.Kind is ObjectMethodKind.Instance or ObjectMethodKind.Constructor,
                "borrowed parameters require static methods, value constructors or admitted integer-object instance methods.");
            Require(!binding.OwnerExternallyVisible || !_layouts.TryGetValue(binding.Owner, out var layout)
                || (layout.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic,
                "owner visibility contradicts its layout attributes.");
            Require(!binding.OwnerAssemblyVisible || !_layouts.TryGetValue(binding.Owner, out var assemblyLayout)
                || (assemblyLayout.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NotPublic
                    or TypeAttributes.NestedPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem,
                "assembly owner visibility contradicts its layout attributes.");
            Require(((binding.Attributes & MethodAttributes.Static) != 0) == isStatic, "static attribute/kind mismatch.");
            if (valueConstructor || valueInstance)
                Require(values is not null && flow.Types.Integer64 && values.Types.Any(t => ObjectTypeId.From(t.Source) == binding.Owner
                        && (!binding.OwnerExternallyVisible || t.ExternallyVisible))
                    && method.Parameters.Length > 0 && method.Parameters[0] == IrType.Location(IrType.Value(binding.Owner), binding.ReadOnlyReceiver)
                    && method.ReturnType.Kind != IrTypeKind.ManagedLocation && binding.VirtualSlot is null
                    && (valueConstructor ? method.ReturnType == IrType.Void
                        && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final))
                            == (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                        : (binding.Attributes & MethodAttributes.RTSpecialName) == 0
                            && ((binding.Attributes & MethodAttributes.Virtual) != 0 || (binding.Attributes & (MethodAttributes.NewSlot | MethodAttributes.Final)) == 0)),
                    "value constructor/instance method requires an exact declared inline receiver capability and admitted declaration flags; constructors require void and constructor flags.");
            else if (primitiveInstance)
            {
                Require(flow.Types.Integer64 && binding.OwnerExternallyVisible && binding.Owner.Specialization == ""
                    && binding.Owner.ContentHash == flow.Types.ObjectBase.ContentHash
                    && binding.Primitive!.Source is { } primitiveSource && ObjectTypeId.From(primitiveSource) == binding.Owner
                    && primitiveSource.Name == "System." + binding.Primitive.Code
                    && binding.Primitive.Code is PrimitiveTypeCode.Char or PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64
                    && method.Parameters.Length > 0 && method.Parameters[0] == (binding.Primitive.ReceiverType with { ReadOnly = binding.ReadOnlyReceiver })
                    && method.ReturnType.Kind != IrTypeKind.ManagedLocation && binding.VirtualSlot is null
                    && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0
                    && ((binding.Attributes & MethodAttributes.Virtual) != 0 || (binding.Attributes & (MethodAttributes.NewSlot | MethodAttributes.Final)) == 0),
                    "primitive instance method requires exact selected scalar identity, a width-preserving borrow and ordinary direct-call flags.");
                if (binding.Primitive!.Code == PrimitiveTypeCode.Char)
                    Require(method.Source.Name == "System.Char::ToString" && method.Parameters.Length == 1
                        && method.ReturnType == StringType && !binding.InvariantProviderOnly
                        && binding.Attributes == (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual),
                        "character primitive requires the exact parameterless ToString override.");
                Require(!primitiveOwners.TryGetValue(binding.Owner, out var previous) || previous == binding.Primitive,
                    "primitive identity has contradictory receiver facts.");
                Require(!primitiveCodes.TryGetValue(binding.Primitive!.Code, out var previousOwner) || previousOwner == binding.Owner,
                    "primitive code has contradictory selected identities.");
                primitiveOwners[binding.Owner] = binding.Primitive!;
                primitiveCodes[binding.Primitive!.Code] = binding.Owner;
            }
            else if (!isStatic)
                Require((_layouts.ContainsKey(binding.Owner) || flow.Types.Integer64 && binding.Owner == flow.Types.String) && method.Parameters.Length > 0
                    && method.Parameters[0] == IrType.Reference(binding.Owner), "instance receiver must have the exact declaring class type.");
            if (Constructor(binding))
            {
                Require(method.ReturnType == IrType.Void && (binding.Attributes & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))
                    == (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName), "constructor requires void and constructor flags.");
                Require(binding.Kind == ObjectMethodKind.ObjectBaseConstructor ? binding.Owner == flow.Types.ObjectBase
                    && method.Parameters.Length == 1 : binding.Owner != flow.Types.ObjectBase, "invalid selected base constructor binding.");
            }
            else Require(binding.Owner != flow.Types.ObjectBase || flow.Types.Integer64 && binding.Kind == ObjectMethodKind.Instance
                && binding.Attributes == (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig)
                && (method.ReturnType == IrType.Int32 && (binding.VirtualName == "Equals" && method.Parameters.Length == 2
                    && method.Parameters[1] == IrType.Reference(flow.Types.ObjectBase)
                    || binding.VirtualName == "GetHashCode" && method.Parameters.Length == 1)
                    || binding.VirtualName == "ToString" && method.Parameters.Length == 1 && method.ReturnType == StringType),
                "ordinary selected Object methods require the admitted managed equality/hash/default-text signatures.");
        }
        ValidateGenericArrays();
        ValidateGenericTypeTests();
        ObjectLexicalAccess.Consistent(program.Bindings.Select(b => (b.Owner, b.LexicalOwners))
            .Concat(flow.Types.Types.Select(t => (ObjectTypeId.From(t.Source), t.LexicalOwners)))
            .Concat(flow.Types.Statics.Select(f => (f.Id.Owner, f.LexicalOwners)))
            .Concat(flow.Types.Delegates.Select(d => (ObjectTypeId.From(d.Source), d.LexicalOwners)))
            .Concat((_arrayValueLayouts?.Values.AsEnumerable() ?? []).Select(v => (ObjectTypeId.From(v.Source), v.LexicalOwners))));
        _interfaces = new(program, _bindings, _methods, values ?? boxes?.Catalog);
        _dispatch = new(program, _bindings, _methods, _types, _interfaces);
    }

    internal void DispatchMaps()
    {
        _dispatch.Validate(); _interfaces.ValidateUsage(_program);
        var used = _program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements).Select(s => s switch {
            IrStatement.Define { Value.Expression: IrExpression.LoadStatic read } => read.Field,
            IrStatement.Define { Value.Expression: IrExpression.AddressStatic address } => address.Field,
            IrStatement.StoreStatic write => write.Field, _ => null }).Where(f => f is not null).ToHashSet();
        Require(used.SetEquals(_statics.Keys), "static catalog contains unused or missing field declarations.");
    }
    internal IEnumerable<IrMethodId> DispatchTargets(IrMethodId declaration) => _interfaces.Has(declaration)
        ? _interfaces.Targets(declaration) : _dispatch.Targets(declaration);
    internal bool Bodyless(IrMethodId declaration) => _interfaces.Has(declaration) || _abstractMethods.Contains(declaration);
    internal IrType? ReturnType(IrMethodId? id) => id is null ? null : _interfaces.Has(id)
        ? _interfaces.Declaration(id).ReturnType : _methods.TryGetValue(id, out var method) ? method.ReturnType : null;

    internal ObjectMethodBinding Binding(IrMethodId id) => _bindings[id];
    internal bool InitializesValueField(IrMethod method, ObjectFieldId? field, IrExpression receiver) =>
        field is not null && Binding(IrMethodId.From(method.Source)) is { Kind: ObjectMethodKind.ValueConstructor } binding
        && binding.Owner == field.Owner && receiver is IrExpression.Parameter { Index: 0 };
    internal bool TypeTest(IrMethod method, ObjectTypeId? target) => target is { ArrayElement: not null }
        ? _program.Flow.Types.Integer64 && ArrayAllocation(method, target)
        : target is not null && (target == _program.Flow.Types.String || _contracts.Contains(target)
            && (_program.Flow.Types.Interfaces.Single(i => ObjectTypeId.From(i.Source) == target).ExternallyVisible
                || _program.Flow.Types.Integer64 && method.Source.ContentHash == target.ContentHash)
            || _delegates.TryGetValue(target, out var callable) && DelegateVisible(method, target, callable) || _layouts.TryGetValue(target, out var layout)
            && (Binding(IrMethodId.From(method.Source)).Owner == target
                || (layout.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public
                || _program.Flow.Types.Integer64 && ObjectLexicalAccess.Visible(Binding(IrMethodId.From(method.Source)), target, layout.LexicalOwners)));
    internal IEnumerable<ObjectTypeId> OperationTypes(IrMethod method) => method.Blocks.SelectMany(b => b.Statements).SelectMany(s => s switch
    {
        IrStatement.InitializeType when _program.Flow.Types.String is { } initializationString => new[] { initializationString },
        IrStatement.Define { Value.Expression: IrExpression.LoadStatic f } when _statics.TryGetValue(f.Field, out var slot) && slot.Type is { Kind: IrTypeKind.Reference, Class: { } type } => new[] { type },
        IrStatement.Define { Value.Expression: IrExpression.AddressStatic f } when _statics.TryGetValue(f.Field, out var addressSlot) && addressSlot.Type is { Kind: IrTypeKind.Reference, Class: { } addressType } => new[] { addressType },
        IrStatement.StoreStatic f when _statics.TryGetValue(f.Field, out var slot) && slot.Type is { Kind: IrTypeKind.Reference, Class: { } type } => new[] { type },
        IrStatement.Define { Value.Expression: IrExpression.LoadField f } => new[] { f.Field.Owner },
        IrStatement.Define { Value.Expression: IrExpression.AddressField f } => new[] { f.Field.Owner },
        IrStatement.StoreField f => new[] { f.Field.Owner },
        IrStatement.Define { Value.Expression: IrExpression.NewException creation }
            when (creation.Message is not null || creation.ParameterName is not null || creation.TypeName is not null || creation.ObjectName is not null)
                && _program.Flow.Types.String is { } text => new[] { text },
        IrStatement.Define { Value.Expression: IrExpression.NewDelegate d } => new[] { d.Type },
        IrStatement.Define { Value.Expression: IrExpression.CallDelegate d } => new[] { d.Type },
        IrStatement.InvokeDelegate d => new[] { d.Type },
        IrStatement.Define { Value.Expression: IrExpression.RuntimeTypeLiteral t } => _scalarTypeTargets.Contains(t.Target)
            ? new[] { t.Implementation } : new[] { t.Target, t.Implementation },
        IrStatement.Define { Value.Expression: IrExpression.EnumEntry } => new[] { _program.Flow.Types.String! },
        IrStatement.Define { Value.Expression: IrExpression.EnumQuery t } => new[] { t.Implementation, _program.Flow.Types.String! },
        IrStatement.Define { Value.Expression: IrExpression.ObjectClone c } => new[] { c.Type },
        IrStatement.Define { Value.Expression: IrExpression.ObjectGetType t } => new[] { t.Implementation },
        IrStatement.Define { Value.Expression: IrExpression.RuntimeTypeHash t } => new[] { t.Implementation },
        IrStatement.Define { Value.Expression: IrExpression.InvariantCulture c } => new[] { c.Type },
        IrStatement.Define { Value.Expression: IrExpression.EmptyArray a } => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.NewArray a } => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.AddressElement a } => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.AddressArraySlot a } => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.LoadElement a } => new[] { a.ArrayType },
        IrStatement.StoreElement a => new[] { a.ArrayType },
        IrStatement.Define { Value.Expression: IrExpression.StringFromCodeUnits } when _program.Flow.Types.String is { } created =>
            new[] { created, ObjectTypeId.Vector(_program.Flow.Types.ObjectBase, ObjectArrayElement.Int32) },
        IrStatement.Define { Value.Expression: IrExpression.StringLiteral or IrExpression.StringLength or IrExpression.StringCodeUnit
            or IrExpression.StringEquals or IrExpression.StringConcat or IrExpression.IntegerTextFind or IrExpression.IntegerTextPublish } when _program.Flow.Types.String is { } text => new[] { text },
        IrStatement.Define { Value.Expression: IrExpression.Call c } when _interfaces.Has(c.Target) => _interfaces.SignatureTypes(c.Target),
        IrStatement.Invoke c when _interfaces.Has(c.Target) => _interfaces.SignatureTypes(c.Target),
        IrStatement.Define { Value.Expression: IrExpression.Call c } when _abstractMethods.Contains(c.Target) => AbstractSignatureTypes(c.Target),
        IrStatement.Invoke c when _abstractMethods.Contains(c.Target) => AbstractSignatureTypes(c.Target),
        IrStatement.Define { Value.Expression: IrExpression.Call c } => ProtectedStaticTypes(method, c.Target),
        IrStatement.Invoke c => ProtectedStaticTypes(method, c.Target),
        _ => Array.Empty<ObjectTypeId>(),
    });

    private IEnumerable<ObjectTypeId> ProtectedStaticTypes(IrMethod method, IrMethodId target)
    {
        var caller = Binding(IrMethodId.From(method.Source));
        return _bindings.TryGetValue(target, out var callee)
            && !ObjectMethodAccess.Allowed(caller, callee, _program.Flow.Types.Integer64)
            && ProtectedStaticAccess(caller, callee) ? new[] { caller.Owner } : Array.Empty<ObjectTypeId>();
    }

    private IEnumerable<ObjectTypeId> AbstractSignatureTypes(IrMethodId id) => _methods[id].Parameters.Append(_methods[id].ReturnType)
        .Where(t => t.Kind == IrTypeKind.Reference).Select(t => t.Class!);

    private bool DelegateVisible(IrMethod caller, ObjectTypeId type, ObjectDelegateType declaration) =>
        declaration.ExternallyVisible || _program.Flow.Types.Integer64
            && ObjectLexicalAccess.Visible(Binding(IrMethodId.From(caller.Source)), type, declaration.LexicalOwners);

    internal IrType? DelegateReturn(ObjectTypeId? type) => type is not null && _delegates.TryGetValue(type, out var declaration)
        ? declaration.ReturnType : null;

    internal bool DelegateCapture(IrMethod caller, IrExpression.NewDelegate capture, IrType result, IReadOnlyList<IrType> values, int ilOffset)
    {
        if (!_allowDelegates || capture.Type is null || capture.Target is null || !_delegates.TryGetValue(capture.Type, out var declaration)
            || result != IrType.Reference(capture.Type) || capture.Receiver < 0 || capture.Receiver >= values.Count
            || !_bindings.TryGetValue(capture.Target, out var target) || target.Kind is not (ObjectMethodKind.Static or ObjectMethodKind.Instance)
            || (capture.VirtualLookupOffset is int lookup
                ? !_program.Flow.Types.Integer64 || lookup < 0 || lookup != ilOffset - 6 || target.Kind != ObjectMethodKind.Instance
                    || (target.Attributes & (MethodAttributes.Virtual | MethodAttributes.Final)) != (MethodAttributes.Virtual | MethodAttributes.Final)
                    || target.VirtualSlot is null
                : (target.Attributes & MethodAttributes.Virtual) != 0 || target.VirtualSlot is not null)
            || !Access(Binding(IrMethodId.From(caller.Source)), target)
            || !DelegateVisible(caller, capture.Type, declaration)) return false;
        var body = _methods[capture.Target];
        int skip = target.Kind == ObjectMethodKind.Instance ? 1 : 0;
        return body.ReturnType == DelegateReturn(capture.Type) && body.Parameters.Length == declaration.ParameterCount + skip
            && body.Parameters.Skip(skip).SequenceEqual(declaration.Parameters)
            && (skip == 0 ? values[capture.Receiver] == IrType.NullReference
                : _types.Assignable(values[capture.Receiver], IrType.Reference(target.Owner)));
    }

    internal bool DelegateCall(IrMethod caller, ObjectTypeId? type, int receiver,
        System.Collections.Immutable.ImmutableArray<int> arguments, IrType result, IReadOnlyList<IrType> values) =>
        _allowDelegates && type is not null && _delegates.TryGetValue(type, out var declaration)
        && DelegateVisible(caller, type, declaration)
        && result == DelegateReturn(type) && receiver >= 0 && receiver < values.Count
        && _types.Assignable(values[receiver], IrType.Reference(type)) && !arguments.IsDefault
        && arguments.Length == declaration.ParameterCount
        && arguments.Select((a, i) => a >= 0 && a < values.Count && _types.Assignable(values[a], declaration.Parameters[i])).All(valid => valid);

    internal IrType? StringType => _program.Flow.Types.String is { } text ? IrType.Reference(text) : null;

    internal ObjectTypeId? RuntimeTypeBase(ObjectTypeId? implementation)
    {
        if (!_program.Flow.Types.Integer64 || implementation is null || implementation.Specialization != ""
            || implementation.ContentHash != _program.Flow.Types.ObjectBase.ContentHash
            || !_layouts.TryGetValue(implementation, out var concrete) || concrete.Source.Name != "Cilica.Runtime.RuntimeTypeObject"
            || concrete.Attributes != (TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit) || !concrete.Fields.IsEmpty || !concrete.Interfaces.IsEmpty
            || concrete.BaseType is not { } type || !_layouts.TryGetValue(type, out var selected)
            || type.Specialization != "" || type.ContentHash != implementation.ContentHash || selected.Source.Name != "System.Type"
            || selected.Attributes != (TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit) || !selected.Fields.IsEmpty || !selected.Interfaces.IsEmpty
            || selected.BaseType is not { } member || !_layouts.TryGetValue(member, out var info)
            || member.ContentHash != implementation.ContentHash || member.Specialization != "" || info.Source.Name != "System.Reflection.MemberInfo"
            || info.Attributes != (TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit) || !info.Fields.IsEmpty || !info.Interfaces.IsEmpty
            || info.BaseType != _program.Flow.Types.ObjectBase
            || _layouts.Any(p => p.Key != implementation && p.Value.BaseType == type)) return null;
        return type;
    }

    internal bool RuntimeTypeLiteral(ObjectTypeId? target, ObjectTypeId? implementation, IrType result) =>
        RuntimeTypeBase(implementation) is { } type && result == IrType.Reference(type) && target is not null
        && (_layouts.ContainsKey(target) || _contracts.Contains(target) || _arrays.Contains(target)
            || _delegates.ContainsKey(target) || _program.Flow.Types.String == target
            || _program.Flow.Types.Integer64 && _scalarTypeTargets.Contains(target));

    internal bool EnumEntry(IrExpression.EnumEntry entry, IReadOnlyList<IrType> values, IrType result) =>
        _program.Flow.Types.Integer64 && entry.EnumType is not null && _enumEntryTypes.Contains(entry.EnumType)
        && entry.Index >= 0 && entry.Index < values.Count && values[entry.Index] == IrType.Int32
        && StringType is not null && result == (entry.Name ? StringType : IrType.Int32);

    internal bool EnumQuery(IrExpression.EnumQuery query, IReadOnlyList<IrType> values, IrType result) =>
        _program.Flow.Types.Integer64 && _enumQueryReady && RuntimeTypeBase(query.Implementation) is { } type
        && query.Type >= 0 && query.Type < values.Count && query.Value >= 0 && query.Value < values.Count
        && _types.Assignable(values[query.Type], IrType.Reference(type))
        && values[query.Value].Kind is IrTypeKind.Reference or IrTypeKind.NullReference
        && result == (query.Message ? StringType : IrType.Int32);

    private bool _defaultTextNamesValidated;
    internal bool DefaultObjectText(IrMethod method, int operand, IReadOnlyList<IrType> values, IrType result)
    {
        if (!_program.Flow.Types.Integer64 || result != StringType || operand < 0 || operand >= values.Count
            || values[operand] != IrType.Reference(_program.Flow.Types.ObjectBase)
            || method.Source.Name != "System.Object::ToString" || method.Parameters.Length != 1
            || method.Parameters[0] != values[operand] || !_bindings.TryGetValue(IrMethodId.From(method.Source), out var binding)
            || binding.Owner != _program.Flow.Types.ObjectBase || binding.Kind != ObjectMethodKind.Instance
            || binding.VirtualSlot is null) return false;
        if (!_defaultTextNamesValidated)
        {
            foreach (var layout in _program.Flow.Types.Types)
            {
                string name = layout.Source.RuntimeName ?? layout.Source.Name.Replace('/', '+');
                if (name.Length > ObjectStringLimits.MaximumLiteralCodeUnits
                    || name.Length > ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits) return false;
                if (!StringLiteral([.. name.Select(c => (ushort)c)])) return false;
            }
            _defaultTextNamesValidated = true;
        }
        return true;
    }

    internal bool RuntimeTypeOperand(int operand, IReadOnlyList<IrType> values, ObjectTypeId? implementation, IrType result, bool hash) =>
        RuntimeTypeBase(implementation) is { } type && operand >= 0 && operand < values.Count
        && (hash ? _types.Assignable(values[operand], IrType.Reference(type)) : values[operand].Kind is IrTypeKind.Reference or IrTypeKind.NullReference)
        && result == (hash ? IrType.Int32 : IrType.Reference(type));

    private ObjectTypeId? _invariantCulture;
    internal bool InvariantCulture(ObjectTypeId? type, IrType result)
    {
        if (!_program.Flow.Types.Integer64 || type is null || type.ArrayElement is not null || type.Specialization != ""
            || type.ContentHash != _program.Flow.Types.ObjectBase.ContentHash || result != IrType.Reference(type)
            || !_layouts.TryGetValue(type, out var layout) || layout.Source.Name != "System.Globalization.CultureInfo"
            || layout.BaseType != _program.Flow.Types.ObjectBase || !layout.Fields.IsEmpty
            || layout.Attributes != (TypeAttributes.Public | TypeAttributes.BeforeFieldInit)
            || _bindings.Values.Any(b => b.Owner == type) || _invariantCulture is not null && _invariantCulture != type) return false;
        _invariantCulture = type;
        return true;
    }

    internal bool StringOperand(int operand, IReadOnlyList<IrType> values) => StringType is { } type
        && operand >= 0 && operand < values.Count && _types.Assignable(values[operand], type);

    internal bool StringLiteral(System.Collections.Immutable.ImmutableArray<ushort> units)
    {
        if (StringType is null || units.IsDefault || units.Length > ObjectStringLimits.MaximumLiteralCodeUnits
            || units.Length > ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits) return false;
        _literalUnits += units.Length;
        // The unit budget is checked before copying/hashing. Ordinal UTF-16 retains
        // unpaired surrogates, unlike an encoder that might replace invalid scalars.
        string content = string.Create(units.Length, units, static (target, source) =>
        {
            for (int i = 0; i < source.Length; i++) target[i] = (char)source[i];
        });
        _literals.Add(content);
        return _literals.Count <= ObjectStringLimits.MaximumDistinctLiterals;
    }

    internal IrType? ArrayElement(ObjectTypeId? array) => array is not null && _types.Valid(IrType.Reference(array), true) && _arrays.Contains(array)
        ? array.ArrayElement!.Kind switch {
            ObjectArrayElementKind.Boolean or ObjectArrayElementKind.Int32 or ObjectArrayElementKind.UInt32 or ObjectArrayElementKind.Byte or ObjectArrayElementKind.Char or ObjectArrayElementKind.Enum32 => IrType.Int32,
            ObjectArrayElementKind.Int64 => IrType.Int64,
            ObjectArrayElementKind.Reference => IrType.Reference(array.ArrayElement.Type!),
            ObjectArrayElementKind.Value when _valueLayouts?.ContainsKey(array.ArrayElement.Type!) == true => IrType.Value(array.ArrayElement.Type!),
            _ => null } : null;

    internal bool ArrayAllocation(IrMethod caller, ObjectTypeId? array, int offset = -1)
    {
        if (array is null || !_arrays.Contains(array)) return false;
        // The declaration's bare generic variable authorizes this exact newarr or
        // Empty instruction only. It cannot authorize a cast or explicit type use.
        if (_genericArrays.TryGetValue((IrMethodId.From(caller.Source), offset), out var authorized) && authorized == array) return true;
        return (array.ArrayElement!.Kind != ObjectArrayElementKind.Reference || TypeTest(caller, array.ArrayElement.Type!))
            && (array.ArrayElement.Kind != ObjectArrayElementKind.Enum32
                || _program.Flow.Types.EnumArrays.Any(e => ObjectTypeId.From(e.Source) == array.ArrayElement.Type
                    && ObjectLexicalAccess.Visible(Binding(IrMethodId.From(caller.Source)), array.ArrayElement.Type!, e.LexicalOwners)))
            && (array.ArrayElement.Kind != ObjectArrayElementKind.Value || _arrayValueLayouts is null
                || _arrayValueLayouts.TryGetValue(array.ArrayElement.Type!, out var layout)
                    && (layout.ExternallyVisible || Binding(IrMethodId.From(caller.Source)).Owner == array.ArrayElement.Type
                        || _program.Flow.Types.Integer64 && ObjectLexicalAccess.Visible(Binding(IrMethodId.From(caller.Source)),
                            array.ArrayElement.Type!, layout.LexicalOwners)));
    }

    internal bool CopyCompatible(IrExpression.ArrayCopyCompatible copy, IReadOnlyList<IrType> values) =>
        copy.ArrayType is not null && _arrays.Contains(copy.ArrayType)
        && copy.Source >= 0 && copy.Source < values.Count && copy.Destination >= 0 && copy.Destination < values.Count
        && _types.Assignable(values[copy.Source], IrType.Reference(copy.ArrayType))
        && _types.Assignable(values[copy.Destination], IrType.Reference(copy.ArrayType));

    internal bool ArrayLength(int array, IReadOnlyList<IrType> values) => array >= 0 && array < values.Count
        && (values[array] == IrType.NullReference || values[array].Class is { } type && _arrays.Contains(type));

    internal bool Element(ObjectTypeId? type, int array, int index, IReadOnlyList<IrType> values) => ArrayElement(type) is not null
        && array >= 0 && array < values.Count && index >= 0 && index < values.Count
        && _types.Assignable(values[array], IrType.Reference(type!)) && values[index] == IrType.Int32;

    internal bool StoreElement(IrStatement.StoreElement store, IReadOnlyList<IrType> values) =>
        Element(store.ArrayType, store.Array, store.Index, values) && store.Value >= 0 && store.Value < values.Count
        && _types.Assignable(values[store.Value], ArrayElement(store.ArrayType)) && store.Exceptional == IrExceptionTarget.MethodExit;

    internal bool Call(IrMethod caller, IrMethodId? target, System.Collections.Immutable.ImmutableArray<int> arguments,
        bool nullCheck, IReadOnlyList<IrType> values, bool allocation = false, bool virtualDispatch = false, IReadOnlyList<IrExpression>? expressions = null, ValueKeyCallContract? valueKeyContract = null)
    {
        if (target is null || arguments.IsDefault) return false;
        if (valueKeyContract is not null && (allocation || nullCheck || virtualDispatch || !ValueKeyCall(caller, target, valueKeyContract))) return false;
        if (_interfaces.Has(target))
        {
            var declaration = _interfaces.Declaration(target);
            return (declaration.OwnerExternallyVisible || _program.Flow.Types.Integer64 && caller.Source.ContentHash == declaration.Owner.ContentHash)
                && !allocation && nullCheck && virtualDispatch && arguments.Length == declaration.Parameters.Length
                && arguments.Select((argument, index) => argument >= 0 && argument < values.Count
                    && _types.Assignable(values[argument], declaration.Parameters[index])).All(valid => valid);
        }
        if (!_bindings.TryGetValue(target, out var callee) || _abstractMethods.Contains(target) && (allocation || !nullCheck || !virtualDispatch)) return false;
        var signature = _methods[target].Parameters;
        int skip = allocation ? 1 : 0;
        if (signature.IsDefault || arguments.Length != signature.Length - skip || valueKeyContract is null && !Access(Binding(IrMethodId.From(caller.Source)), callee)) return false;
        if (allocation ? !Constructor(callee) || (_layouts[callee.Owner].Attributes & TypeAttributes.Abstract) != 0 : Constructor(callee)) return false;
        if (nullCheck && callee.Kind != ObjectMethodKind.Instance) return false;
        if (virtualDispatch ? allocation || !nullCheck || callee.VirtualSlot is null || !_dispatch.Has(target)
            : nullCheck && callee.VirtualSlot is not null
                && (!_program.Flow.Types.Integer64 || (callee.Attributes & MethodAttributes.Final) == 0)) return false;
        if (callee.IntegerParser is { } parser)
        {
            bool implicitStyle = callee.ThrowingIntegerParser && (parser.Code == PrimitiveTypeCode.Int64 || signature.Length == 2);
            int providerIndex = implicitStyle ? 1 : 2;
            if (arguments.Length != (implicitStyle ? 2 : callee.ThrowingIntegerParser ? 3 : 4) || expressions is null
                || !implicitStyle && (arguments[1] < 0 || arguments[1] >= expressions.Count || expressions[arguments[1]] is not IrExpression.Constant parserStyle
                    || !IntegerParserRules.AllowsStyle(parser.Code, parserStyle.Value))
                || arguments[providerIndex] < 0 || arguments[providerIndex] >= expressions.Count
                || expressions[arguments[providerIndex]] is not IrExpression.InvariantCulture) return false;
        }
        if (callee.InvariantProviderOnly && (arguments.Length != 2 || expressions is null || arguments[1] < 0
            || arguments[1] >= expressions.Count || expressions[arguments[1]] is not IrExpression.InvariantCulture)) return false;
        for (int index = 0; index < arguments.Length; index++)
            if (arguments[index] < 0 || arguments[index] >= values.Count || !_types.Assignable(values[arguments[index]], signature[index + skip])) return false;
        if (callee.Owner == _program.Flow.Types.ObjectBase && callee.VirtualName == "ToString"
            && (arguments.IsEmpty || !OrdinaryTextReceiver(values[arguments[0]]))) return false;
        if (virtualDispatch && (arguments.IsEmpty || !_dispatch.Accepts(target, values[arguments[0]]))) return false;
        // Object slots also accept strings, arrays, delegates and boxes. Their
        // descriptors have no ordinary-class virtual table, so an Object alias
        // cannot prove that this map covers the dynamic receiver.
        if (virtualDispatch && callee.VirtualSlot?.Owner == _program.Flow.Types.ObjectBase)
            Require(!arguments.IsEmpty && (values[arguments[0]].Kind == IrTypeKind.NullReference
                || values[arguments[0]].Class is { } receiver && receiver != _program.Flow.Types.ObjectBase && _layouts.ContainsKey(receiver)),
                "Object-slot virtual calls require a statically known ordinary class receiver; special runtime objects and Object aliases need later dispatch lowering.");
        return true;
    }

    internal bool ConstructorInitializationCall(IrMethod caller, IrStatement.Invoke call, IReadOnlyList<IrType> values, IReadOnlyList<IrExpression> expressions)
    {
        if (call.Target is null || !_bindings.TryGetValue(call.Target, out var target)) return false;
        var owner = Binding(IrMethodId.From(caller.Source));
        if (owner.Kind != ObjectMethodKind.Constructor || !Constructor(target)
            || !(target.Owner == _layouts[owner.Owner].BaseType
                || _program.Flow.Types.Integer64 && target.Owner == owner.Owner) || call.NullCheckReceiver || call.VirtualDispatch || call.Arguments.IsDefault
            || call.Arguments.Length != _methods[target.Method].Parameters.Length || call.Arguments.IsEmpty
            || call.Arguments[0] < 0 || call.Arguments[0] >= values.Count
            || expressions[call.Arguments[0]] is not IrExpression.Parameter { Index: 0 }
            || values[call.Arguments[0]] != IrType.Reference(owner.Owner)
            || !Access(owner, target) && !ObjectMethodAccess.ProtectedBaseConstructor(owner, target, _layouts[owner.Owner].BaseType, _program.Flow.Types.Integer64)) return false;
        var signature = _methods[target.Method].Parameters;
        return call.Arguments.Select((argument, index) => argument >= 0 && argument < values.Count
            && _types.Assignable(values[argument], signature[index])).All(valid => valid);
    }

    private bool OrdinaryTextReceiver(IrType type)
    {
        if (type.Kind == IrTypeKind.NullReference) return true;
        if (type.Kind != IrTypeKind.Reference || type.Class is not { } receiver || receiver == _program.Flow.Types.ObjectBase) return false;
        for (int depth=0; depth<ObjectModelLimits.MaximumOwnerDepth; ++depth)
        {
            if (receiver == _program.Flow.Types.ObjectBase) return true;
            if (!_layouts.TryGetValue(receiver, out var layout)) return false;
            if (receiver.ContentHash == _program.Flow.Types.ObjectBase.ContentHash
                && layout.Source.Name is "System.Type" or "System.Reflection.MemberInfo" or "Cilica.Runtime.RuntimeTypeObject") return false;
            if (layout.BaseType is null) return false;
            receiver=layout.BaseType;
        }
        return false;
    }

    internal bool Clone(IrMethod method, ObjectTypeId? type, int operand, IReadOnlyList<IrType> values, IrType result)
    {
        if (!_program.Flow.Types.Integer64 || type is null || Binding(IrMethodId.From(method.Source)).Owner != type
            || operand < 0 || operand >= values.Count || !_types.Assignable(values[operand], IrType.Reference(type))
            || result != IrType.Reference(_program.Flow.Types.ObjectBase) || !_layouts.TryGetValue(type, out var layout)
            || (layout.Attributes & (TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Interface)) != TypeAttributes.Sealed) return false;
        var seen = new HashSet<ObjectTypeId>();
        while (type != _program.Flow.Types.ObjectBase)
        {
            if (type is null || type.ContentHash == _program.Flow.Types.ObjectBase.ContentHash || !seen.Add(type)
                || seen.Count > ObjectModelLimits.MaximumOwnerDepth || !_layouts.TryGetValue(type, out layout)) return false;
            type = layout.BaseType;
        }
        return true;
    }

    internal IrType? AllocationType(IrMethodId? constructor) => constructor is not null && _bindings.TryGetValue(constructor, out var target)
        && Constructor(target) ? IrType.Reference(target.Owner) : null;

    internal bool TypeInitialization(IrMethodId? target) => target is not null && _bindings.TryGetValue(target, out var binding)
        && binding.TypeInitializerName is not null;

    internal bool StaticField(IrMethod method, ObjectFieldId? id, bool write, out IrType? storage)
    {
        storage = null;
        if (id is null || !_statics.TryGetValue(id, out var field)) return false;
        var caller = Binding(IrMethodId.From(method.Source));
        if (write && (field.Attributes & FieldAttributes.InitOnly) != 0 && (caller.Owner != id.Owner || caller.TypeInitializerName is null)) return false;
        if (!ObjectFieldAccess.Allowed(caller, id.Owner, field.Attributes,
            field.OwnerExternallyVisible, field.LexicalOwners, _program.Flow.Types.Integer64)) return false;
        storage = field.Type; return true;
    }
    internal bool StaticAddress(IrMethod method, ObjectFieldId id, IrType result) =>
        _program.Flow.Types.Integer64 && StaticField(method, id, false, out var storage)
        && ManagedLocationRules.Address(storage!, result, (_statics[id].Attributes & FieldAttributes.InitOnly) != 0);
    internal bool StaticStore(IrMethod method, IrStatement.StoreStatic store, IReadOnlyList<IrType> values) =>
        store.Exceptional == IrExceptionTarget.MethodExit && StaticField(method, store.Field, true, out var type)
        && store.Value >= 0 && store.Value < values.Count && _types.Assignable(values[store.Value], type);
    internal bool Field(IrMethod method, ObjectFieldId? id, int receiver, IReadOnlyList<IrType> values, out IrType? storage)
    {
        storage = null;
        if (id?.Owner is not { ArrayElement: null } || !_fields.TryGetValue(id, out var field) || receiver < 0 || receiver >= values.Count
            || !_types.Assignable(values[receiver], IrType.Reference(id.Owner))) return false;
        var caller = Binding(IrMethodId.From(method.Source));
        var layout = _layouts[id.Owner];
        if (!ObjectFieldAccess.Allowed(caller, id.Owner, field.Attributes,
            (layout.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public,
            layout.LexicalOwners, _program.Flow.Types.Integer64)) return false;
        storage = field.Storage switch { ObjectStorage.Int32 => IrType.Int32, ObjectStorage.Int64 => IrType.Int64,
            ObjectStorage.Reference reference => IrType.Reference(reference.Type), ObjectStorage.Value value => IrType.Value(value.Type), _ => null! };
        return true;
    }

    internal bool Store(IrMethod method, IrStatement.StoreField store, IReadOnlyList<IrType> values, IReadOnlyList<IrExpression> expressions)
    {
        if (!Field(method, store.Field, store.Receiver, values, out var storage) || store.Value < 0 || store.Value >= values.Count
            || !_types.Assignable(values[store.Value], storage) || store.Exceptional != IrExceptionTarget.MethodExit) return false;
        if ((_fields[store.Field].Attributes & FieldAttributes.InitOnly) == 0) return true;
        var owner = Binding(IrMethodId.From(method.Source));
        return owner.Kind == ObjectMethodKind.Constructor && owner.Owner == store.Field.Owner
            && expressions[store.Receiver] is IrExpression.Parameter { Index: 0 };
    }

    internal void Method(IrMethod method)
    {
        var binding = Binding(IrMethodId.From(method.Source));
        string origin = " " + method.Source.Name + " token 0x" + method.Source.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        if (binding.Kind == ObjectMethodKind.Constructor)
        {
            void ConstructorRequire(bool condition, string message)
            {
                if (!condition) Require(false, message + origin);
            }
            ConstructorRequire(method.Blocks.Length == 1 || _program.Flow.Types.Integer64,
                "constructors require straight-line initialization outside the integer-object profile.");
            // Only the entry prefix may perform initialization. All admitted body
            // paths start after that prefix; no handler may recover its failures.
            ConstructorRequire(!method.ExceptionRegions.Any(r => r.TryStart == 0),
                "constructor initialization prefix cannot be protected by a local handler.");
            foreach (var block in method.Blocks)
            {
                bool entersEntry = block.Terminator switch {
                    IrJump j => j.Edge.Target == 0,
                    IrBranch b => b.Taken.Target == 0 || b.NotTaken.Target == 0,
                    IrLeave l => l.Edge.Target == 0, _ => false };
                ConstructorRequire(!entersEntry, "constructor body cannot re-enter its initialization prefix.");
                if (block.Id != 0)
                    ConstructorRequire(!block.Statements.OfType<IrStatement.Invoke>().Any(c =>
                        _bindings.TryGetValue(c.Target, out var target) && Constructor(target)),
                        "constructor must initialize its receiver only in the entry prefix.");
            }
            var values = method.Blocks[0].Values;
            var types = values.Select(v => v.Type).ToArray();
            var expressions = values.Select(v => v.Expression).ToArray();
            bool initialized = false;
            bool This(int id) => expressions[id] is IrExpression.Parameter { Index: 0 };
            foreach (var statement in method.Blocks[0].Statements)
            {
                if (statement is IrStatement.Invoke call && _bindings.TryGetValue(call.Target, out var callee) && Constructor(callee))
                {
                    ConstructorRequire(!initialized && ConstructorInitializationCall(method, call, types, expressions)
                        && call.Arguments.Skip(1).All(a => !This(a)),
                        "constructor must call its immediate base or same-owner constructor once on its direct receiver.");
                    if (callee.Owner == binding.Owner) _constructorChains.Add(binding.Method, callee.Method);
                    initialized = true;
                }
                else if (!initialized)
                {
                    // CLI permits declared-field initialization before the base call.
                    // Otherwise uninitialized this cannot be observed, aliased or escaped.
                    bool valid = statement switch
                    {
                        IrStatement.Define d => d.Value.Expression switch
                        {
                            IrExpression.Parameter or IrExpression.Constant or IrExpression.Null or IrExpression.Local => true,
                            IrExpression.Constant64 or IrExpression.DefaultValue or IrExpression.AddressLocal when _program.Flow.Types.Integer64 => true,
                            IrExpression.AddressParameter p when _program.Flow.Types.Integer64 => p.Index != 0,
                            IrExpression.Binary64 b when _program.Flow.Types.Integer64 => !This(b.Left) && !This(b.Right),
                            IrExpression.Not64 n when _program.Flow.Types.Integer64 => !This(n.Operand),
                            IrExpression.ConvertInteger c when _program.Flow.Types.Integer64 => !This(c.Operand),
                            IrExpression.IntegerCast c when _program.Flow.Types.Integer64 => !This(c.Operand),
                            IrExpression.CheckedNativeIndex c when _program.Flow.Types.Integer64 => !This(c.Operand),
                            IrExpression.ValueField f when _program.Flow.Types.Integer64 => !This(f.Operand),
                            IrExpression.WithValueField f when _program.Flow.Types.Integer64 => !This(f.Operand) && !This(f.Value),
                            IrExpression.LocationField f when _program.Flow.Types.Integer64 => !This(f.Location),
                            IrExpression.ReadOnlyLocation r when _program.Flow.Types.Integer64 => !This(r.Location),
                            IrExpression.LoadLocation l when _program.Flow.Types.Integer64 => !This(l.Location),
                            IrExpression.AddressField f when _program.Flow.Types.Integer64 => !This(f.Receiver),
                            IrExpression.AddressArraySlot a when _program.Flow.Types.Integer64 => !This(a.Array) && !This(a.Index),
                            IrExpression.Binary b => !This(b.Left) && !This(b.Right),
                            IrExpression.Unary u => !This(u.Operand),
                            IrExpression.Compare c => !This(c.Left) && !This(c.Right),
                            IrExpression.Call c => c.Arguments.All(a => !This(a)),
                            IrExpression.NewObject n => n.Arguments.All(a => !This(a)),
                            IrExpression.StringLiteral or IrExpression.InvariantCulture or IrExpression.RuntimeTypeLiteral => true,
                            IrExpression.ObjectDefaultText t => !This(t.Operand),
                            IrExpression.ObjectGetType t => !This(t.Operand),
                            IrExpression.RuntimeTypeHash t => !This(t.Operand),
                            IrExpression.ObjectIdentityHash t => !This(t.Operand),
                            IrExpression.ObjectClone t => !This(t.Operand),
                            IrExpression.IntegerTextFind s => !This(s.Index),
                            IrExpression.IntegerTextPublish s => !This(s.Index) && !This(s.Text),
                            IrExpression.StringLength s => !This(s.Operand),
                            IrExpression.StringCodeUnit s => !This(s.Operand) && !This(s.Index),
                            IrExpression.StringEquals s => !This(s.Left) && !This(s.Right),
                            IrExpression.StringFromCodeUnits s => !This(s.Array),
                            IrExpression.StringConcat s => !This(s.Left) && !This(s.Right),
                            IrExpression.EmptyArray or IrExpression.LoadStatic or IrExpression.AddressStatic => true,
                            IrExpression.NewArray a => !This(a.Length),
                            IrExpression.ArrayLength a => !This(a.Array),
                            IrExpression.LoadElement a => !This(a.Array) && !This(a.Index),
                            IrExpression.LoadField f => !This(f.Receiver),
                            IrExpression.CastClass c => !This(c.Operand),
                            IrExpression.IsInstance t => !This(t.Operand),
                            _ => false,
                        },
                        IrStatement.StoreLocal s => !This(s.Value),
                        IrStatement.StoreLocation s when _program.Flow.Types.Integer64 => !This(s.Location) && !This(s.Value),
                        IrStatement.InitializeLocation s when _program.Flow.Types.Integer64 => !This(s.Location),
                        IrStatement.InitializeType when _program.Flow.Types.Integer64 => true,
                        IrStatement.Invoke c => c.Arguments.All(a => !This(a)),
                        IrStatement.StoreStatic s => !This(s.Value),
                        IrStatement.StoreField s => !This(s.Value) && (!This(s.Receiver) || s.Field.Owner == binding.Owner),
                        IrStatement.StoreElement s => !This(s.Array) && !This(s.Index) && !This(s.Value),
                        _ => false,
                    };
                    ConstructorRequire(valid, "uninitialized constructor receiver cannot be read, aliased or passed before base initialization: "
                        + (statement is IrStatement.Define definition ? definition.Value.Expression.GetType().Name : statement.GetType().Name) + ".");
                }
            }
            ConstructorRequire(initialized, "constructor must initialize its direct receiver through its immediate base or same-owner constructor.");
        }
        if (binding.Kind == ObjectMethodKind.ObjectBaseConstructor)
            Require(method.Blocks.Length == 1 && method.Blocks[0].Statements.IsEmpty && method.Locals.IsEmpty
                && method.Blocks[0].Terminator is IrVoidReturn, "selected Object constructor must have an empty nop/ret body." + origin);
    }

    private static bool Constructor(ObjectMethodBinding b) => b.Kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor;
    private bool Access(ObjectMethodBinding caller, ObjectMethodBinding target) =>
        ObjectMethodAccess.Allowed(caller, target, _program.Flow.Types.Integer64)
        || ProtectedStaticAccess(caller, target);
    private bool ProtectedStaticAccess(ObjectMethodBinding caller, ObjectMethodBinding target) =>
        ObjectMethodAccess.ProtectedStatic(caller, target, _layouts.ContainsKey(caller.Owner) && _layouts.ContainsKey(target.Owner)
            && _types.Assignable(IrType.Reference(caller.Owner), IrType.Reference(target.Owner)), _program.Flow.Types.Integer64);
    private static bool CanonicalHash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit) && hash == hash.ToLowerInvariant();
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new ArgumentException("Invalid object operation IR: " + message);
    }
}
