namespace Cilica.Compiler;

// Shared detached signature rules for the exact managed vector helper recipes.
internal static class ObjectArrayInterfaceRecipes
{
    internal static string? Select(ObjectInterfaceDeclaration declaration, ObjectArrayInterfaceKind kind, ObjectTypeId objectBase)
    {
        if (declaration.Parameters.Length == 2 && declaration.Parameters[1] == IrType.Int32
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
    internal static IrType? Result(string name, ObjectTypeId vector) => name switch {
        "Count" or "False" or "True" => IrType.Int32,
        "Get" => vector.ArrayElement!.Type is { } element ? IrType.Reference(element) : IrType.Int32,
        "Identity" => IrType.Reference(vector), _ => null };
    internal static bool Signature(ObjectDispatchMethod method) =>
        !method.Parameters.IsDefaultOrEmpty && method.Parameters[0].Class is { ArrayElement: not null } vector
        && method.Parameters.Length == (method.Name == "Get" ? 2 : 1)
        && (method.Name != "Get" || method.Parameters[1] == IrType.Int32)
        && Result(method.Name, vector) == method.ReturnType;
}
