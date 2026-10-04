namespace Cilica.Compiler;

/// <summary>Explicit mixed-width scalar IR; no object, narrow-integer or native-pointer storage.</summary>
public sealed record IntegerIrProgram(IrProgram Flow)
{
    public const string Profile = "integer-arithmetic-ir-v1";
}

public enum Int64Operation
{
    // Ordinals form the private C++ helper selector, covered by differential tests for every member.
    AddWrapping, SubtractWrapping, MultiplyWrapping, BitwiseAnd, BitwiseOr, BitwiseXor,
    ShiftLeft, ShiftRightSigned, ShiftRightUnsigned, DivideSigned, DivideUnsigned,
    AddCheckedSigned, SubtractCheckedSigned, MultiplyCheckedSigned,
    AddCheckedUnsigned, SubtractCheckedUnsigned, MultiplyCheckedUnsigned, RemainderSigned, RemainderUnsigned,
}

public enum IntegerConversion { SignExtend32, ZeroExtend32, Truncate64 }

internal static class Int64Operations
{
    internal static Int64Operation FromOpcode(string opcode) => opcode switch
    {
        "add" => Int64Operation.AddWrapping, "sub" => Int64Operation.SubtractWrapping, "mul" => Int64Operation.MultiplyWrapping,
        "and" => Int64Operation.BitwiseAnd, "or" => Int64Operation.BitwiseOr, "xor" => Int64Operation.BitwiseXor,
        "shl" => Int64Operation.ShiftLeft, "shr" => Int64Operation.ShiftRightSigned, "shr.un" => Int64Operation.ShiftRightUnsigned,
        "div" => Int64Operation.DivideSigned, "div.un" => Int64Operation.DivideUnsigned,
        "rem" => Int64Operation.RemainderSigned, "rem.un" => Int64Operation.RemainderUnsigned,
        "add.ovf" => Int64Operation.AddCheckedSigned, "sub.ovf" => Int64Operation.SubtractCheckedSigned, "mul.ovf" => Int64Operation.MultiplyCheckedSigned,
        "add.ovf.un" => Int64Operation.AddCheckedUnsigned, "sub.ovf.un" => Int64Operation.SubtractCheckedUnsigned, "mul.ovf.un" => Int64Operation.MultiplyCheckedUnsigned,
        _ => throw new System.InvalidOperationException("Unsupported Int64 instruction."),
    };
    internal static bool Shift(Int64Operation operation) => operation is Int64Operation.ShiftLeft or Int64Operation.ShiftRightSigned or Int64Operation.ShiftRightUnsigned;
    internal static bool MayFault(Int64Operation operation) => operation is Int64Operation.DivideSigned or Int64Operation.DivideUnsigned
        or Int64Operation.AddCheckedSigned or Int64Operation.SubtractCheckedSigned or Int64Operation.MultiplyCheckedSigned
        or Int64Operation.AddCheckedUnsigned or Int64Operation.SubtractCheckedUnsigned or Int64Operation.MultiplyCheckedUnsigned
        or Int64Operation.RemainderSigned or Int64Operation.RemainderUnsigned;
    internal static bool Convert(IntegerConversion conversion, IrType input, IrType output) => conversion switch
    {
        IntegerConversion.SignExtend32 or IntegerConversion.ZeroExtend32 => input == IrType.Int32 && output == IrType.Int64,
        IntegerConversion.Truncate64 => input == IrType.Int64 && output == IrType.Int32,
        _ => false,
    };
}
