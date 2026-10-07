using System.Linq;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

// Shared detached signature rules for the exact managed vector helper recipes.
internal static class ObjectArrayInterfaceRecipes
{
    internal static string? Select(ObjectInterfaceDeclaration declaration, ObjectArrayInterfaceKind kind, ObjectTypeId objectBase, bool scalarValue = false)
    {
        if (declaration.Name == "GetEnumerator" && kind == ObjectArrayInterfaceKind.GenericEnumerable
            && declaration.Parameters.Length == 1 && declaration.ReturnType.Kind == IrTypeKind.Reference) return "Enumerate";
        if (declaration.Name == "GetEnumerator" && kind == ObjectArrayInterfaceKind.Enumerable
            && declaration.Parameters.Length == 1 && declaration.ReturnType.Kind == IrTypeKind.Reference) return "EnumerateObjects";
        if (declaration.ReturnType == IrType.Int32 && (scalarValue || declaration.Parameters.Length == 2))
        {
            if (declaration.Name == "IndexOf" && kind == ObjectArrayInterfaceKind.List) return "IndexOf";
            if (declaration.Name is "Contains" or "Remove" && kind == ObjectArrayInterfaceKind.Collection) return declaration.Name;
        }
        if (declaration.ReturnType == IrType.Void)
        {
            if (declaration.Name == "CopyTo" && kind == ObjectArrayInterfaceKind.Collection
                && declaration.Parameters.Length == 3 && declaration.Parameters[1].Class?.ArrayElement is not null
                && declaration.Parameters[2] == IrType.Int32) return "CopyTo";
            if (declaration.Name == "Clear" && declaration.Parameters.Length == 1)
                return kind == ObjectArrayInterfaceKind.Collection ? "ClearReadOnly" : kind == ObjectArrayInterfaceKind.NonGenericList ? "Clear" : null;
            if (declaration.Name == "RemoveAt" && kind is ObjectArrayInterfaceKind.List or ObjectArrayInterfaceKind.NonGenericList
                && declaration.Parameters.Length == 2 && declaration.Parameters[1] == IrType.Int32) return "RemoveAt";
            if (declaration.Name == "Insert" && declaration.Parameters.Length > 1 && declaration.Parameters[1] == IrType.Int32)
            {
                if (kind == ObjectArrayInterfaceKind.List && (scalarValue || declaration.Parameters.Length == 3)) return "Insert";
                if (kind == ObjectArrayInterfaceKind.NonGenericList && declaration.Parameters.Length == 3
                    && declaration.Parameters[2] == IrType.Reference(objectBase)) return "InsertObject";
            }
            if (declaration.Name == "Add" && kind == ObjectArrayInterfaceKind.Collection && (scalarValue || declaration.Parameters.Length == 2)) return "Add";
            if (declaration.Name == "set_Item" && kind == ObjectArrayInterfaceKind.List && (scalarValue || declaration.Parameters.Length == 3) && declaration.Parameters.Length > 1 && declaration.Parameters[1] == IrType.Int32) return "Set";
        }
        if ((scalarValue || declaration.Parameters.Length == 2) && declaration.Parameters.Length > 1 && declaration.Parameters[1] == IrType.Int32
            && declaration.Name == "get_Item" && kind is ObjectArrayInterfaceKind.List or ObjectArrayInterfaceKind.ReadOnlyList)
            return "Get";
        if (declaration.Parameters.Length != 1) return null;
        if (declaration.ReturnType == IrType.Int32)
            return (declaration.Name, kind) switch {
                ("get_Count", ObjectArrayInterfaceKind.Collection or ObjectArrayInterfaceKind.ReadOnlyCollection or ObjectArrayInterfaceKind.NonGenericCollection) => "Count",
                ("get_IsSynchronized", ObjectArrayInterfaceKind.NonGenericCollection) => "False",
                ("get_IsReadOnly", ObjectArrayInterfaceKind.NonGenericList) => "False",
                ("get_IsReadOnly", ObjectArrayInterfaceKind.Collection) => "True",
                ("get_IsFixedSize", ObjectArrayInterfaceKind.NonGenericList) => "True",
                _ => null };
        return declaration.Name == "get_SyncRoot" && kind == ObjectArrayInterfaceKind.NonGenericCollection
            && declaration.ReturnType == IrType.Reference(objectBase) ? "Identity" : null;
    }
    internal static IrType? Result(string name, ObjectTypeId vector, ObjectTypeCatalog? catalog = null) => name switch {
        "Enumerate" => EnumeratorResult(vector, catalog),
        "EnumerateObjects" => ObjectEnumeratorResult(catalog),
        "Count" or "False" or "True" or "IndexOf" or "Contains" or "Remove" => IrType.Int32,
        "CopyTo" or "Add" or "Set" or "Insert" or "InsertObject" or "RemoveAt" or "ClearReadOnly" or "Clear" => IrType.Void,
        "Get" => vector.ArrayElement!.Type is { } element ? vector.ArrayElement.Kind == ObjectArrayElementKind.Value ? IrType.Value(element) : IrType.Reference(element) : IrType.Int32,
        "Identity" => IrType.Reference(vector), _ => null };
    private static IrType? ObjectEnumeratorResult(ObjectTypeCatalog? catalog)
    {
        if (catalog is null) return null;
        var matches = catalog.Interfaces.Where(i => i.Source.ContentHash == catalog.ObjectBase.ContentHash
            && i.Source.Name == "System.Collections.IEnumerator" && i.Arguments.IsEmpty).ToArray();
        return matches.Length == 1 ? IrType.Reference(ObjectTypeId.From(matches[0].Source)) : null;
    }
    private static IrType? EnumeratorResult(ObjectTypeId vector, ObjectTypeCatalog? catalog)
    {
        if (catalog is null) return null;
        string integer = ClosedGenericKey.Create(new([new TypeSignature.Primitive(PrimitiveTypeCode.Int32)]), SignatureList<TypeSignature>.Empty);
        var matches = catalog.Interfaces.Where(i => i.Source.ContentHash == catalog.ObjectBase.ContentHash
            && i.Source.Name == "System.Collections.Generic.IEnumerator`1" && i.Arguments.Length == 1
            && (vector.ArrayElement!.Kind == ObjectArrayElementKind.Int32 ? i.Arguments[0].Identity == integer
                : vector.ArrayElement.Kind == ObjectArrayElementKind.Reference ? i.Arguments[0].Reference == vector.ArrayElement.Type
                : i.Arguments[0].Value == vector.ArrayElement.Type)).ToArray();
        return matches.Length == 1 ? IrType.Reference(ObjectTypeId.From(matches[0].Source)) : null;
    }
    internal static int Arity(string name) => name is "CopyTo" or "Set" or "Insert" or "InsertObject" ? 3 : name is "Get" or "Add" or "RemoveAt" or "IndexOf" or "Contains" or "Remove" ? 2 : 1;
    internal static bool Signature(ObjectDispatchMethod method, ObjectInterfaceDispatchPlan plan)
    {
        if (method.Parameters.IsDefaultOrEmpty || method.Parameters[0].Class is not { ArrayElement: not null } vector || vector.ArrayElement.Narrow)
            return false;
        var element = Result("Get", vector)!;
        if (element.Kind == IrTypeKind.Value && plan.Values is null && method.Name is "Get" or "Set" or "Add" or "Insert" or "IndexOf" or "Contains" or "Remove")
        {
            var shape = plan.Types.ValueArrays.Single(s => s.Type == element.Class);
            IrType Scalar(ObjectStorage storage) => storage switch {
                ObjectStorage.Int32 => IrType.Int32, ObjectStorage.Int64 => IrType.Int64,
                ObjectStorage.Reference r => IrType.Reference(r.Type), _ => throw new System.ArgumentException("Invalid value-array leaf.") };
            var leaves = shape.Empty ? System.Array.Empty<IrType>() : shape.Slots.Select(Scalar).ToArray();
            var prefix = method.Name is "Add" or "IndexOf" or "Contains" or "Remove" ? new[] { IrType.Reference(vector) } : [IrType.Reference(vector), IrType.Int32];
            var tail = method.Name == "Get" ? leaves.Select(t => IrType.Location(t)) : leaves;
            return method.ReturnType == (method.Name == "Get" ? IrType.Void : Result(method.Name, vector))
                && method.Parameters.SequenceEqual(prefix.Concat(tail));
        }
        return method.Parameters.Length == Arity(method.Name)
            && (method.Name != "CopyTo" || method.Parameters[1] == IrType.Reference(vector) && method.Parameters[2] == IrType.Int32)
            && (method.Name is not ("Get" or "Set" or "Insert" or "InsertObject" or "RemoveAt") || method.Parameters[1] == IrType.Int32)
            && (method.Name is not ("Add" or "Set" or "Insert" or "IndexOf" or "Contains" or "Remove") || method.Parameters[^1] == element)
            && (method.Name != "InsertObject" || method.Parameters[2] == IrType.Reference(plan.Types.ObjectBase))
            && Result(method.Name, vector, plan.Types) == method.ReturnType;
    }
}
