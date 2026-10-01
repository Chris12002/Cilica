using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Cilica.Metadata.IL;

/// <summary>A decoded instruction. Branch and switch targets are absolute IL offsets.</summary>
internal sealed class Instruction
{
    public Instruction(int offset, int length, OpCode code, object? operand, int[] targets)
    {
        Offset = offset;
        Length = length;
        Code = code;
        Operand = operand;
        Targets = targets;
    }

    public int Offset { get; }

    public int Length { get; }

    public int End => Offset + Length;

    public OpCode Code { get; }

    /// <summary>An integer, floating-point value, variable index or metadata token, depending on <see cref="OpCode.Operand"/>.</summary>
    public object? Operand { get; }

    public int[] Targets { get; }

    public int Token => (int)Operand!;

    public int VariableIndex => Operand is int index ? index : ImplicitVariableIndex();

    /// <summary>Whether the instruction accesses an argument (true) or a local (false); null otherwise.</summary>
    public bool? AccessesArgument => Code.Name switch
    {
        "ldarg.0" or "ldarg.1" or "ldarg.2" or "ldarg.3" or "ldarg.s" or "ldarga.s" or "starg.s"
            or "ldarg" or "ldarga" or "starg" => true,
        "ldloc.0" or "ldloc.1" or "ldloc.2" or "ldloc.3" or "stloc.0" or "stloc.1" or "stloc.2" or "stloc.3"
            or "ldloc.s" or "ldloca.s" or "stloc.s" or "ldloc" or "ldloca" or "stloc" => false,
        _ => null,
    };

    private int ImplicitVariableIndex() => Code.Name[^1] - '0';
}

internal static class ILDecoder
{
    /// <summary>
    /// Decodes a method body's instructions. Returns an error message for an unknown
    /// opcode, a truncated operand or a trailing prefix; instructions decoded before the
    /// error are still returned.
    /// </summary>
    public static List<Instruction> Decode(BlobReader il, out string? error, int maximumInstructions = int.MaxValue)
    {
        var instructions = new List<Instruction>();
        error = null;
        try
        {
            while (il.RemainingBytes > 0)
            {
                if (instructions.Count >= maximumInstructions)
                {
                    error = "Instruction decoding exceeds the configured instruction limit.";
                    return instructions;
                }
                int offset = il.Offset;
                byte first = il.ReadByte();
                OpCode? code;
                if (first == 0xFE)
                {
                    if (il.RemainingBytes == 0)
                    {
                        error = $"Truncated two-byte opcode at {Label(offset)}.";
                        return instructions;
                    }
                    byte second = il.ReadByte();
                    code = OpCodes.TwoByteCode(second);
                    if (code is null)
                    {
                        error = $"Unknown opcode 0xfe{second:x2} at {Label(offset)}.";
                        return instructions;
                    }
                }
                else
                {
                    code = OpCodes.OneByteCode(first);
                    if (code is null)
                    {
                        error = $"Unknown opcode 0x{first:x2} at {Label(offset)}.";
                        return instructions;
                    }
                }

                object? operand = null;
                int[] targets = [];
                switch (code.Operand)
                {
                    case OperandKind.None:
                        break;
                    case OperandKind.Int8:
                        operand = (int)il.ReadSByte();
                        break;
                    case OperandKind.UInt8:
                    case OperandKind.ShortVariable:
                        operand = (int)il.ReadByte();
                        break;
                    case OperandKind.Variable:
                        operand = (int)il.ReadUInt16();
                        break;
                    case OperandKind.Int32:
                        operand = il.ReadInt32();
                        break;
                    case OperandKind.Int64:
                        operand = il.ReadInt64();
                        break;
                    case OperandKind.Float32:
                        operand = il.ReadSingle();
                        break;
                    case OperandKind.Float64:
                        operand = il.ReadDouble();
                        break;
                    case OperandKind.ShortBranch:
                    {
                        int delta = il.ReadSByte();
                        targets = [il.Offset + delta];
                        break;
                    }
                    case OperandKind.Branch:
                    {
                        int delta = il.ReadInt32();
                        targets = [il.Offset + delta];
                        break;
                    }
                    case OperandKind.Switch:
                    {
                        uint count = il.ReadUInt32();
                        if (count > (uint)il.RemainingBytes / 4)
                        {
                            error = $"Switch at {Label(offset)} declares {count} targets but the body ends first.";
                            return instructions;
                        }
                        var deltas = new int[count];
                        for (int index = 0; index < deltas.Length; index++)
                        {
                            deltas[index] = il.ReadInt32();
                        }
                        int next = il.Offset;
                        targets = Array.ConvertAll(deltas, delta => next + delta);
                        break;
                    }
                    default:
                        operand = il.ReadInt32();
                        break;
                }

                instructions.Add(new Instruction(offset, il.Offset - offset, code, operand, targets));
            }
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception))
        {
            int offset = instructions.Count == 0 ? 0 : instructions[^1].End;
            error = $"Truncated instruction operand at {Label(offset)}.";
            return instructions;
        }

        if (instructions.Count > 0 && instructions[^1].Code.Flow == FlowKind.Prefix)
        {
            error = $"Prefix {instructions[^1].Code.Name} at {Label(instructions[^1].Offset)} is not followed by an instruction.";
        }
        return instructions;
    }

    public static string Label(int offset) =>
        "IL_" + offset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
}
