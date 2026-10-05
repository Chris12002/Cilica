using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Cilica.Compiler;

/// <summary>Rejects caller-forged layout models before they can feed later lowering.</summary>
public static class ObjectModelValidator
{
    // CLI metadata flags, not use of the obsolete formatter serialization APIs.
    private const TypeAttributes SerializableMetadata = (TypeAttributes)0x00002000;
    private const FieldAttributes NonSerializedMetadata = (FieldAttributes)0x0080;
    public static void Validate(ObjectModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(new ObjectTypeCatalog([model.Entry], model.ObjectBase, model.Types) { Integer64 = model.Integer64, Interfaces = model.Interfaces, Arrays = model.Arrays, String = model.String, Delegates = model.Delegates });
    }

    public static void Validate(ObjectTypeCatalog model) => Validate(model, inlineFields: false);

    internal static void Validate(ObjectTypeCatalog model, bool inlineFields)
    {
        ArgumentNullException.ThrowIfNull(model);
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Invalid object layout model: " + message, nameof(model));
        }
        Require(Identity(model.ObjectBase, 0x02) && !model.Roots.IsDefault && model.Roots.Length is > 0 and <= ObjectModelLimits.MaximumTypes
            && model.Roots.All(r => ReferenceIdentity(r, model.ObjectBase)) && model.Roots.Distinct().Count() == model.Roots.Length,
            "roots/base need unique canonical nominal or array identities.");
        Require(model.String is null || Identity(model.String, 0x02) && model.String != model.ObjectBase,
            "selected String requires a canonical nominal identity distinct from Object.");
        int stringCount = model.String is null ? 0 : 1;
        Require(!model.Types.IsDefault && model.Types.Length is > 0 and <= ObjectModelLimits.MaximumTypes, "invalid type count.");
        Require(!model.Interfaces.IsDefault && model.Interfaces.Length <= ObjectModelLimits.MaximumTypes
            && model.Types.Length + model.Interfaces.Length <= ObjectModelLimits.MaximumTypes, "combined class/interface type limit exceeded.");
        Require(!model.Arrays.IsDefault && model.Arrays.Length <= ObjectModelLimits.MaximumTypes
            && model.Types.Length + model.Interfaces.Length + model.Arrays.Length + stringCount <= ObjectModelLimits.MaximumTypes,
            "combined class/interface/array/string type limit exceeded.");
        Require(!model.Delegates.IsDefault && model.Delegates.Length <= ObjectModelLimits.MaximumTypes
            && model.Types.Length + model.Interfaces.Length + model.Arrays.Length + stringCount + model.Delegates.Length <= ObjectModelLimits.MaximumTypes,
            "combined delegate/type limit exceeded.");
        var delegates = new HashSet<ObjectTypeId>();
        int delegateParameters = 0;
        foreach (var declaration in model.Delegates)
        {
            Require(declaration?.Source is not null && declaration.Source.Name is not null
                && declaration.Source.AssemblyIdentity is not null && Hash(declaration.Source.ContentHash), "invalid delegate source.");
            Require(Identity(ObjectTypeId.From(declaration.Source), 0x02) && delegates.Add(ObjectTypeId.From(declaration.Source))
                && declaration.ParameterCount is >= 0 and < IrLimits.MaximumParameters, "invalid delegate identity or signature.");
            delegateParameters += declaration.ParameterCount;
            Require(delegateParameters <= IrLimits.MaximumParameters, "aggregate delegate signature limit exceeded.");
        }
        Require(model.Arrays.All(a => ArrayIdentity(a, model.ObjectBase)) && model.Arrays.Distinct().Count() == model.Arrays.Length,
            "array identities require a unique selected-Object anchor and Int32 or nominal reference elements.");
        var arrays = model.Arrays.ToHashSet();
        var types = new Dictionary<ObjectTypeId, ObjectTypeLayout>();
        var interfaces = new Dictionary<ObjectTypeId, ObjectInterface>();
        var interfaceParameters = new Dictionary<(string Hash, int Token), GenericParameterAttributes[]>();
        var arrayContracts = new Dictionary<(string Hash, int Token), ObjectArrayInterfaceKind>();
        var argumentReferences = new Dictionary<string, ObjectTypeId?>();
        int argumentCount = 0;
        var fieldIdentities = new HashSet<(string Hash, string Specialization, int Token, int Component)>();
        int fieldCount = 0;
        foreach (var type in model.Types)
        {
            Require(type is not null && type.Source is not null, "type source is missing.");
            Require(type.Source.AssemblyIdentity is not null && type.Source.Name is not null
                && Hash(type.Source.ContentHash), "invalid type source.");
            var id = ObjectTypeId.From(type.Source);
            Require(Identity(id, 0x02) && types.TryAdd(id, type), "invalid or duplicate TypeDef identity.");
            Require(ClassAttributes(type.Attributes), "class attributes are outside the layout profile.");
            Require(Edges(type.Interfaces), "class interface edges need distinct canonical identities.");
            Require(!type.Fields.IsDefault && type.Fields.Length <= ObjectModelLimits.MaximumFields, "invalid field array.");
            fieldCount += type.Fields.Length;
            Require(fieldCount <= ObjectModelLimits.MaximumFields, "aggregate field limit exceeded.");
            int previous = 0, previousComponent = -1;
            foreach (var field in type.Fields)
            {
                Require(field is not null && field.Id is not null && field.Id.Owner == id
                    && DefinitionToken(field.Id.Token, 0x04) && (field.Id.Token > previous && field.Id.Component is -1 or 0
                        || field.Id.Token == previous && field.Id.Component == previousComponent + 1 && previousComponent >= 0)
                    && (field.Id.Component == -1 || model.Integer64) && field.Id.Component < ObjectModelLimits.MaximumFields
                    && fieldIdentities.Add((id.ContentHash, id.Specialization, field.Id.Token, field.Id.Component)), "fields need their declaring owner and ordered unique image-scoped FieldDef tokens.");
                previous = field.Id.Token; previousComponent = field.Id.Component;
                Require(field.Name is not null && FieldAttributesSupported(field.Attributes), "unsupported field attributes/name.");
                Require(inlineFields && model.Integer64 && field.Id.Component == -1 && field.Storage is ObjectStorage.Value value && Identity(value.Type, 0x02)
                    || field.Storage is ObjectStorage.Int32 || model.Integer64 && field.Storage is ObjectStorage.Int64 || field.Storage is ObjectStorage.Reference reference
                    && ReferenceIdentity(reference.Type, model.ObjectBase), "unsupported field storage.");
            }
            Require(id == model.ObjectBase
                ? type.BaseType is null && type.Fields.IsEmpty && type.Interfaces.IsEmpty && (type.Attributes & TypeAttributes.Sealed) == 0
                : Identity(type.BaseType, 0x02), "classes need a canonical base; the selected object base must be fieldless, unsealed and parentless.");
        }
        foreach (var contract in model.Interfaces)
        {
            Require(contract?.Source is not null && contract.Source.AssemblyIdentity is not null && contract.Source.Name is not null
                && Hash(contract.Source.ContentHash), "invalid interface source.");
            var id = ObjectTypeId.From(contract.Source);
            Require(Identity(id, 0x02) && !types.ContainsKey(id) && interfaces.TryAdd(id, contract), "invalid or duplicate class/interface identity.");
            Require(InterfaceAttributes(contract.Attributes) && contract.ExternallyVisible && Edges(contract.Parents),
                "interfaces require public visibility, public enclosing types and valid parent identities.");
            Require(!contract.Arguments.IsDefault && contract.Arguments.Length <= ObjectModelLimits.MaximumInterfaceArguments - argumentCount
                && (contract.Arguments.IsEmpty ? id.Specialization.Length == 0 : model.Integer64 && id.Specialization.Length == 64),
                "closed interface arguments require the explicit profile, a specialized identity and bounded argument facts.");
            Require(Enum.IsDefined(contract.ArrayContract) && (contract.ArrayContract == ObjectArrayInterfaceKind.None
                || model.Integer64 && id.ContentHash == model.ObjectBase.ContentHash
                    && contract.Arguments.Length == (contract.ArrayContract == ObjectArrayInterfaceKind.Enumerable ? 0 : 1)),
                "runtime vector contracts require selected-library identity and exact argument arity.");
            if (contract.ArrayContract != ObjectArrayInterfaceKind.None)
            {
                string expectedName = contract.ArrayContract switch {
                    ObjectArrayInterfaceKind.Enumerable => "System.Collections.IEnumerable",
                    ObjectArrayInterfaceKind.GenericEnumerable => "System.Collections.Generic.IEnumerable`1",
                    ObjectArrayInterfaceKind.Collection => "System.Collections.Generic.ICollection`1",
                    ObjectArrayInterfaceKind.List => "System.Collections.Generic.IList`1",
                    ObjectArrayInterfaceKind.ReadOnlyCollection => "System.Collections.Generic.IReadOnlyCollection`1",
                    _ => "System.Collections.Generic.IReadOnlyList`1" };
                Require(contract.Source.Name == expectedName, "runtime vector contract name contradicts its selected operation.");
            }
            var arrayKey = (id.ContentHash, id.Token);
            Require(!arrayContracts.TryGetValue(arrayKey, out var priorArrayContract) || priorArrayContract == contract.ArrayContract,
                "closed instances disagree on their runtime vector contract.");
            arrayContracts[arrayKey] = contract.ArrayContract;
            argumentCount += contract.Arguments.Length;
            foreach (var argument in contract.Arguments)
            {
                Require(argument is not null && argument.Identity is { Length: 64 } && Specialization(argument.Identity)
                    && argument.Variance is GenericParameterAttributes.None or GenericParameterAttributes.Covariant or GenericParameterAttributes.Contravariant
                    && (argument.Reference is null || ReferenceIdentity(argument.Reference, model.ObjectBase)), "invalid interface argument identity/variance.");
                if (argumentReferences.TryGetValue(argument.Identity, out var prior))
                    Require(prior == argument.Reference, "one argument identity cannot describe different reference types.");
                else argumentReferences.Add(argument.Identity, argument.Reference);
            }
            var parameterKey = (id.ContentHash, id.Token);
            var variance = contract.Arguments.Select(a => a.Variance).ToArray();
            if (interfaceParameters.TryGetValue(parameterKey, out var priorParameters))
                Require(priorParameters.SequenceEqual(variance), "closed instances disagree on interface parameter declarations.");
            else interfaceParameters.Add(parameterKey, variance);
        }
        Require(model.String is null || !types.ContainsKey(model.String) && !interfaces.ContainsKey(model.String),
            "selected String cannot also declare class fields or interface contracts.");
        Require(delegates.All(id => !types.ContainsKey(id) && !interfaces.ContainsKey(id) && id != model.String),
            "delegate identity collides with another storage kind.");
        bool Known(ObjectTypeId id) => delegates.Contains(id) || types.ContainsKey(id) || interfaces.ContainsKey(id) || arrays.Contains(id) || id == model.String;
        foreach (var array in arrays)
            Require(array.ArrayElement!.Type is not { } element || types.ContainsKey(element) || interfaces.ContainsKey(element) || element == model.String || delegates.Contains(element),
                "array reference element is outside the nominal catalog.");
        Require(model.Roots.All(Known) && types.ContainsKey(model.ObjectBase), "roots/base are outside the model.");
        foreach (var type in model.Types)
        {
            if (type.BaseType is { } parent && (!types.TryGetValue(parent, out var baseLayout) || (baseLayout.Attributes & TypeAttributes.Sealed) != 0))
                Require(false, "base class is missing or sealed for " + type.Source.Name + ".");
            foreach (var reference in type.Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>())
                Require(Known(reference.Type), "reference field targets are outside the model.");
            Require(type.Interfaces.All(interfaces.ContainsKey), "class interface edges must name included interface contracts.");
        }
        foreach (var contract in interfaces.Values)
        {
            Require(contract.Parents.All(interfaces.ContainsKey), "interface parents must name included interface contracts.");
            Require(contract.Arguments.All(a => a.Reference is null || Known(a.Reference)), "interface argument reference is outside the catalog.");
        }
        _ = new ObjectTypeRelations(model); // Iterative cycle check and bounded assignment closure.
        var visited = new HashSet<ObjectTypeId>();
        var pending = new Stack<ObjectTypeId>();
        foreach (var root in model.Roots) pending.Push(root);
        pending.Push(model.ObjectBase);
        while (pending.TryPop(out ObjectTypeId? id))
            if (visited.Add(id))
            {
                if (id == model.String || delegates.Contains(id)) pending.Push(model.ObjectBase);
                else if (arrays.Contains(id))
                {
                    if (id.ArrayElement!.Type is { } element) pending.Push(element);
                }
                else if (interfaces.TryGetValue(id, out var contract))
                {
                    foreach (var parent in contract.Parents) pending.Push(parent);
                    foreach (var argument in contract.Arguments) if (argument.Reference is { } reference) pending.Push(reference);
                }
                else
                {
                    if (types[id].BaseType is { } parent) pending.Push(parent);
                    foreach (var edge in types[id].Interfaces) pending.Push(edge);
                    foreach (var reference in types[id].Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>()) pending.Push(reference.Type);
                }
            }
        Require(visited.Count == types.Count + interfaces.Count + arrays.Count + stringCount + delegates.Count, "model contains unrelated types.");
    }

    internal static bool ClassAttributes(TypeAttributes attributes) =>
        (attributes & ~(TypeAttributes.VisibilityMask | TypeAttributes.Sealed | TypeAttributes.SpecialName
            | TypeAttributes.RTSpecialName | TypeAttributes.BeforeFieldInit | SerializableMetadata)) == 0;

    internal static bool InterfaceAttributes(TypeAttributes attributes) =>
        (attributes & (TypeAttributes.Interface | TypeAttributes.Abstract)) == (TypeAttributes.Interface | TypeAttributes.Abstract)
        && (attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic
        && (attributes & ~(TypeAttributes.VisibilityMask | TypeAttributes.Interface | TypeAttributes.Abstract
            | TypeAttributes.SpecialName | TypeAttributes.RTSpecialName | TypeAttributes.BeforeFieldInit)) == 0;

    private static bool Edges(System.Collections.Immutable.ImmutableArray<ObjectTypeId> edges) => !edges.IsDefault
        && edges.Length <= ObjectModelLimits.MaximumTypes && edges.All(id => Identity(id, 0x02)) && edges.Distinct().Count() == edges.Length;

    internal static bool FieldAttributesSupported(FieldAttributes attributes) =>
        (attributes & FieldAttributes.FieldAccessMask) <= FieldAttributes.Public
        && (attributes & ~(FieldAttributes.FieldAccessMask | FieldAttributes.InitOnly | NonSerializedMetadata
            | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName)) == 0;

    private static bool Hash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit);
    internal static bool ReferenceIdentity(ObjectTypeId? id, ObjectTypeId objectBase) => Identity(id, 0x02) || ArrayIdentity(id, objectBase);
    private static bool ArrayIdentity(ObjectTypeId? id, ObjectTypeId objectBase) => id?.ArrayElement is { } element
        && id.Specialization == "" && id.ContentHash == objectBase.ContentHash && id.Token == objectBase.Token
        && (element.Kind == ObjectArrayElementKind.Int32 ? element.Type is null
            : element.Kind == ObjectArrayElementKind.Reference && Identity(element.Type, 0x02));
    internal static bool Identity(ObjectTypeId? id, uint table) => id is not null && id.ArrayElement is null && Hash(id.ContentHash)
        && id.ContentHash == id.ContentHash.ToLowerInvariant() && Specialization(id.Specialization) && DefinitionToken(id.Token, table)
        && (id.Token & 0xffffff) != 1; // TypeDef row 1 is the module, not an instance class.
    internal static bool Specialization(string? key) => key is not null && (key.Length == 0 || key.Length == 64 && key.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'));
    private static bool DefinitionToken(int token, uint table) => (uint)token >> 24 == table && (token & 0xffffff) != 0;
}
