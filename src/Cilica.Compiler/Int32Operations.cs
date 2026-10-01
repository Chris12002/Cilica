namespace Cilica.Compiler;

/// <summary>Shared effects for known operations; caller IR must still be validated.</summary>
internal static class Int32Operations
{
    internal static Int32Operation? FromOpcode(string opcode) => opcode switch
    {
        "add" => Int32Operation.AddWrapping, "sub" => Int32Operation.SubtractWrapping, "mul" => Int32Operation.MultiplyWrapping,
        "and" => Int32Operation.BitwiseAnd, "or" => Int32Operation.BitwiseOr, "xor" => Int32Operation.BitwiseXor,
        "shl" => Int32Operation.ShiftLeft, "shr" => Int32Operation.ShiftRightSigned, "shr.un" => Int32Operation.ShiftRightUnsigned,
        "div" => Int32Operation.DivideSigned, "div.un" => Int32Operation.DivideUnsigned,
        "rem" => Int32Operation.RemainderSigned, "rem.un" => Int32Operation.RemainderUnsigned,
        "add.ovf" => Int32Operation.AddCheckedSigned, "sub.ovf" => Int32Operation.SubtractCheckedSigned, "mul.ovf" => Int32Operation.MultiplyCheckedSigned,
        "add.ovf.un" => Int32Operation.AddCheckedUnsigned, "sub.ovf.un" => Int32Operation.SubtractCheckedUnsigned, "mul.ovf.un" => Int32Operation.MultiplyCheckedUnsigned,
        _ => null,
    };

    internal static bool IsSignedChecked(Int32Operation operation) => operation is
        Int32Operation.AddCheckedSigned or Int32Operation.SubtractCheckedSigned or Int32Operation.MultiplyCheckedSigned;

    internal static bool IsChecked(Int32Operation operation) => IsSignedChecked(operation) || operation is
        Int32Operation.AddCheckedUnsigned or Int32Operation.SubtractCheckedUnsigned or Int32Operation.MultiplyCheckedUnsigned;

    internal static bool MayFault(Int32Operation operation) => IsChecked(operation) || operation is
        Int32Operation.DivideSigned or Int32Operation.DivideUnsigned or Int32Operation.RemainderSigned or Int32Operation.RemainderUnsigned;
}
