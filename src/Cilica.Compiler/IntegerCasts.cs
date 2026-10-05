using System;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public enum IntegerCastTarget { Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64 }

internal static class IntegerCasts
{
    internal static IntegerCastTarget? NarrowStorage(TypeSignature signature) => signature switch
    {
        TypeSignature.Primitive { Code: PrimitiveTypeCode.SByte } => IntegerCastTarget.Int8,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.Byte or PrimitiveTypeCode.Boolean } => IntegerCastTarget.UInt8,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.Int16 } => IntegerCastTarget.Int16,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.UInt16 or PrimitiveTypeCode.Char } => IntegerCastTarget.UInt16,
        _ => null,
    };
    internal static int Bits(IntegerCastTarget target) => target switch
    {
        IntegerCastTarget.Int8 or IntegerCastTarget.UInt8 => 8,
        IntegerCastTarget.Int16 or IntegerCastTarget.UInt16 => 16,
        IntegerCastTarget.Int32 or IntegerCastTarget.UInt32 => 32,
        IntegerCastTarget.Int64 or IntegerCastTarget.UInt64 => 64,
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
    internal static bool Unsigned(IntegerCastTarget target) => target is IntegerCastTarget.UInt8 or IntegerCastTarget.UInt16 or IntegerCastTarget.UInt32 or IntegerCastTarget.UInt64;
    internal static IrType Result(IntegerCastTarget target) => Bits(target) == 64 ? IrType.Int64 : IrType.Int32;

    internal static IrExpression.IntegerCast? FromOpcode(string opcode, int operand)
    {
        bool checkedRange = opcode.StartsWith("conv.ovf.", StringComparison.Ordinal);
        if (!checkedRange && !opcode.StartsWith("conv.", StringComparison.Ordinal)) return null;
        bool unsignedSource = opcode.EndsWith(".un", StringComparison.Ordinal);
        string target = opcode[(checkedRange ? 9 : 5)..];
        if (unsignedSource) target = target[..^3];
        IntegerCastTarget? kind = target switch
        {
            "i1" => IntegerCastTarget.Int8, "u1" => IntegerCastTarget.UInt8,
            "i2" => IntegerCastTarget.Int16, "u2" => IntegerCastTarget.UInt16,
            "i4" => IntegerCastTarget.Int32, "u4" => IntegerCastTarget.UInt32,
            "i8" => IntegerCastTarget.Int64, "u8" => IntegerCastTarget.UInt64,
            _ => null,
        };
        return kind is { } value ? new(value, operand, checkedRange, unsignedSource || !checkedRange && value == IntegerCastTarget.UInt64) : null;
    }
}
