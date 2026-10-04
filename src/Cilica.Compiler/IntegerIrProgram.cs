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
