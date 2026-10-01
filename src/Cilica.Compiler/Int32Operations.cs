namespace Cilica.Compiler;

/// <summary>Shared effects for known operations; caller IR must still be validated.</summary>
internal static class Int32Operations
{
    internal static bool IsSignedChecked(Int32Operation operation) => operation is
        Int32Operation.AddCheckedSigned or Int32Operation.SubtractCheckedSigned or Int32Operation.MultiplyCheckedSigned;

    internal static bool IsChecked(Int32Operation operation) => IsSignedChecked(operation) || operation is
        Int32Operation.AddCheckedUnsigned or Int32Operation.SubtractCheckedUnsigned or Int32Operation.MultiplyCheckedUnsigned;

    internal static bool MayFault(Int32Operation operation) => IsChecked(operation) || operation is
        Int32Operation.DivideSigned or Int32Operation.DivideUnsigned or Int32Operation.RemainderSigned or Int32Operation.RemainderUnsigned;
}
