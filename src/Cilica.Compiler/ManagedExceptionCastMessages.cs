using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

internal sealed record ExceptionCastText(ObjectTypeId From, bool FromBox, ObjectTypeId To, bool ToBox, bool NullableTarget, string Text);
internal static class ManagedExceptionCastMessages
{
    internal static ImmutableArray<ExceptionCastText> Build(IEnumerable<IrMethod> methods, ObjectTypeCatalog catalog, ImmutableArray<ValueTypeLayout> values)
    {
        var targets = methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression switch
        {
            IrExpression.CastClass cast => (cast.Target, false, false), IrExpression.UnboxValue box => (box.Type, true, box.NullableTarget),
            IrExpression.UnboxLocation box => (box.Type, true, false), IrExpression.CheckBox box => (box.Type, true, box.NullableTarget),
            IrExpression.AddressBoxSlot box => (box.Type, true, false), _ => ((ObjectTypeId?)null, false, false),
        }).Where(p => p.Item1 is not null).Select(p => (p.Item1!, p.Item2, p.Item3)).Distinct().ToArray();
        if (targets.Length == 0) return [];
        string Name(TypeSource source)
        {
            string name = source.RuntimeName ?? source.Name.Replace('/', '+');
            if (name.Length > 4096 || name.Any(ch => !char.IsLetterOrDigit(ch) && ch is not ('.' or '_' or '`' or '+' or '[' or ']' or ',' or '<' or '>')))
                throw new ArgumentException("Cast exception names require bounded nominal or generated type names: " + name[..Math.Min(name.Length, 160)] + ".");
            if (source.Specialization.Length != 0 && source.RuntimeName is null)
                throw new ArgumentException("Closed generic cast messages require an explicit runtime type display name.");
            return name;
        }
        var names = catalog.Types.Select(t => (ObjectTypeId.From(t.Source), Name: Name(t.Source)))
            .Concat(catalog.Delegates.Select(t => (ObjectTypeId.From(t.Source), Name: Name(t.Source))))
            .Concat(catalog.Interfaces.Select(t => (ObjectTypeId.From(t.Source), Name: Name(t.Source)))).ToDictionary(p => p.Item1, p => p.Name);
        if (catalog.String is { } text) names.Add(text, "System.String");
        var boxes = values.ToDictionary(t => ObjectTypeId.From(t.Source), t => Name(t.Source));
        foreach (var array in catalog.Arrays)
        {
            string elementName;
            if (array.ArrayElement!.Kind == ObjectArrayElementKind.Int32) elementName = "System.Int32";
            else if (!(array.ArrayElement.Kind == ObjectArrayElementKind.Value ? boxes : names).TryGetValue(array.ArrayElement.Type!, out elementName!))
                throw new ArgumentException("Array cast messages require the exact element's runtime type display name.");
            names.Add(array, elementName + "[]");
        }
        var interfaces = catalog.Interfaces.Select(t => ObjectTypeId.From(t.Source)).ToHashSet();
        if (!catalog.Integer64 && targets.Any(t => !t.Item2 && interfaces.Contains(t.Item1)))
            throw new ArgumentException("Managed exception cast messages currently require class targets; interface cast messages need a supported lowering.");
        var sources = names.Where(p => !interfaces.Contains(p.Key)).Select(p => (p.Key, Box: false, p.Value)).Concat(boxes.Select(p => (p.Key, Box: true, p.Value)));
        var result = ImmutableArray.CreateBuilder<ExceptionCastText>(); int units = 0;
        foreach (var (target, boxed, nullableTarget) in targets)
        foreach (var (source, sourceBox, sourceName) in sources)
        {
            string targetName = boxed ? boxes[target] : names[target];
            if (nullableTarget) targetName = "System.Nullable`1[" + targetName + "]";
            string message = "Unable to cast object of type '" + sourceName + "' to type '" + targetName + "'.";
            if ((units += message.Length) > ObjectStringLimits.MaximumTotalLiteralCodeUnits)
                throw new ArgumentException("Cast exception message resources exceed the UTF-16 budget.");
            result.Add(new(source, sourceBox, target, boxed, nullableTarget, message));
        }
        return result.ToImmutable();
    }
}
