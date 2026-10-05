namespace Cilica.Compiler;

// Shared detached signature rules for the exact managed vector helper recipes.
internal static class ObjectArrayInterfaceRecipes
{
    internal static string? Select(ObjectInterfaceDeclaration declaration, ObjectArrayInterfaceKind kind, ObjectTypeId objectBase)
    {
        if (declaration.ReturnType == IrType.Void)
        {
            if (declaration.Name == "Add" && kind == ObjectArrayInterfaceKind.Collection && declaration.Parameters.Length == 2) return "Add";
            if (declaration.Name == "set_Item" && kind == ObjectArrayInterfaceKind.List && declaration.Parameters.Length == 3 && declaration.Parameters[1] == IrType.Int32) return "Set";
        }
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
        "Add" or "Set" => IrType.Void,
        "Get" => vector.ArrayElement!.Type is { } element ? IrType.Reference(element) : IrType.Int32,
        "Identity" => IrType.Reference(vector), _ => null };
    internal static int Arity(string name) => name == "Set" ? 3 : name is "Get" or "Add" ? 2 : 1;
    internal static bool Signature(ObjectDispatchMethod method) =>
        !method.Parameters.IsDefaultOrEmpty && method.Parameters[0].Class is { ArrayElement: not null } vector
        && method.Parameters.Length == Arity(method.Name)
        && (method.Name is not ("Get" or "Set") || method.Parameters[1] == IrType.Int32)
        && (method.Name is not ("Add" or "Set") || method.Parameters[^1] == Result("Get", vector))
        && Result(method.Name, vector) == method.ReturnType;
}
