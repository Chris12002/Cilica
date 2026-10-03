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
        Validate(new ObjectTypeCatalog([model.Entry], model.ObjectBase, model.Types) { Interfaces = model.Interfaces });
    }

    public static void Validate(ObjectTypeCatalog model)
    {
        ArgumentNullException.ThrowIfNull(model);
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Invalid object layout model: " + message, nameof(model));
        }
        Require(!model.Roots.IsDefault && model.Roots.Length is > 0 and <= ObjectModelLimits.MaximumTypes
            && model.Roots.All(r => Identity(r, 0x02)) && model.Roots.Distinct().Count() == model.Roots.Length
            && Identity(model.ObjectBase, 0x02), "roots/base need unique canonical TypeDef identities.");
        Require(!model.Types.IsDefault && model.Types.Length is > 0 and <= ObjectModelLimits.MaximumTypes, "invalid type count.");
        Require(!model.Interfaces.IsDefault && model.Interfaces.Length <= ObjectModelLimits.MaximumTypes
            && model.Types.Length + model.Interfaces.Length <= ObjectModelLimits.MaximumTypes, "combined class/interface type limit exceeded.");
        var types = new Dictionary<ObjectTypeId, ObjectTypeLayout>();
        var interfaces = new Dictionary<ObjectTypeId, ObjectInterface>();
        var fieldIdentities = new HashSet<(string Hash, int Token)>();
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
            int previous = 0;
            foreach (var field in type.Fields)
            {
                Require(field is not null && field.Id is not null && field.Id.Owner == id
                    && DefinitionToken(field.Id.Token, 0x04) && field.Id.Token > previous
                    && fieldIdentities.Add((id.ContentHash, field.Id.Token)), "fields need their declaring owner and ordered unique image-scoped FieldDef tokens.");
                previous = field.Id.Token;
                Require(field.Name is not null && FieldAttributesSupported(field.Attributes), "unsupported field attributes/name.");
                Require(field.Storage is ObjectStorage.Int32 || field.Storage is ObjectStorage.Reference reference
                    && Identity(reference.Type, 0x02), "unsupported field storage.");
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
        }
        bool Known(ObjectTypeId id) => types.ContainsKey(id) || interfaces.ContainsKey(id);
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
            Require(contract.Parents.All(interfaces.ContainsKey), "interface parents must name included interface contracts.");
        _ = new ObjectTypeRelations(model); // Iterative cycle check and bounded assignment closure.
        var visited = new HashSet<ObjectTypeId>();
        var pending = new Stack<ObjectTypeId>();
        foreach (var root in model.Roots) pending.Push(root);
        pending.Push(model.ObjectBase);
        while (pending.TryPop(out ObjectTypeId? id))
            if (visited.Add(id))
            {
                if (interfaces.TryGetValue(id, out var contract))
                    foreach (var parent in contract.Parents) pending.Push(parent);
                else
                {
                    if (types[id].BaseType is { } parent) pending.Push(parent);
                    foreach (var edge in types[id].Interfaces) pending.Push(edge);
                    foreach (var reference in types[id].Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>()) pending.Push(reference.Type);
                }
            }
        Require(visited.Count == types.Count + interfaces.Count, "model contains unrelated types.");
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
    private static bool Identity(ObjectTypeId? id, uint table) => id is not null && Hash(id.ContentHash)
        && id.ContentHash == id.ContentHash.ToLowerInvariant() && DefinitionToken(id.Token, table)
        && (id.Token & 0xffffff) != 1; // TypeDef row 1 is the module, not an instance class.
    private static bool DefinitionToken(int token, uint table) => (uint)token >> 24 == table && (token & 0xffffff) != 0;
}
