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
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new ArgumentException("Invalid object layout model: " + message, nameof(model));
        }
        Require(Identity(model.Entry, 0x02) && Identity(model.ObjectBase, 0x02), "entry/base need canonical TypeDef identities.");
        Require(!model.Types.IsDefault && model.Types.Length is > 0 and <= ObjectModelLimits.MaximumTypes, "invalid type count.");
        var types = new Dictionary<ObjectTypeId, ObjectTypeLayout>();
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
                ? type.BaseType is null && type.Fields.IsEmpty && (type.Attributes & TypeAttributes.Sealed) == 0
                : type.BaseType == model.ObjectBase, "classes must derive directly from the fieldless selected object base.");
        }
        Require(types.ContainsKey(model.Entry) && types.ContainsKey(model.ObjectBase), "entry/base are outside the model.");
        foreach (var type in model.Types)
            foreach (var reference in type.Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>())
                Require(types.ContainsKey(reference.Type), "reference field targets are outside the model.");
        var visited = new HashSet<ObjectTypeId>();
        var pending = new Stack<ObjectTypeId>(); pending.Push(model.Entry); pending.Push(model.ObjectBase);
        while (pending.TryPop(out ObjectTypeId? id))
            if (visited.Add(id))
                foreach (var reference in types[id].Fields.Select(f => f.Storage).OfType<ObjectStorage.Reference>()) pending.Push(reference.Type);
        Require(visited.Count == types.Count, "model contains unrelated types.");
    }

    internal static bool ClassAttributes(TypeAttributes attributes) =>
        (attributes & ~(TypeAttributes.VisibilityMask | TypeAttributes.Sealed | TypeAttributes.SpecialName
            | TypeAttributes.RTSpecialName | TypeAttributes.BeforeFieldInit | SerializableMetadata)) == 0;

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
