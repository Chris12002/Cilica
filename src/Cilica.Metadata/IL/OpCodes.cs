using System.Collections.Generic;

namespace Cilica.Metadata.IL;

internal enum OperandKind
{
    None,
    Int8,
    UInt8,
    Int32,
    Int64,
    Float32,
    Float64,
    ShortVariable,
    Variable,
    ShortBranch,
    Branch,
    Switch,
    MethodToken,
    FieldToken,
    TypeToken,
    StringToken,
    SignatureToken,
    AnyToken,
}

internal enum FlowKind
{
    Next,
    Branch,
    ConditionalBranch,
    Switch,
    Return,
    Throw,
    Leave,
    EndFinally,
    EndFilter,
    Jump,
    Prefix,
}

/// <summary>
/// One CIL instruction definition from ECMA-335 Partition III. A stack count of
/// <see cref="OpCodes.Variable"/> depends on the operand's signature or the method's return type.
/// </summary>
internal sealed record OpCode(ushort Value, string Name, OperandKind Operand, int Pops, int Pushes, FlowKind Flow)
{
    public int Size => Value > 0xFF ? 2 : 1;
}

internal static class OpCodes
{
    public const int Variable = -1;

    private static readonly OpCode?[] OneByte = new OpCode?[256];
    private static readonly OpCode?[] TwoByte = new OpCode?[256];

    static OpCodes()
    {
        foreach (OpCode code in Definitions())
        {
            if (code.Value > 0xFF)
            {
                TwoByte[code.Value & 0xFF] = code;
            }
            else
            {
                OneByte[code.Value] = code;
            }
        }
    }

    public static OpCode? OneByteCode(byte value) => OneByte[value];

    public static OpCode? TwoByteCode(byte value) => TwoByte[value];

    private static IEnumerable<OpCode> Definitions()
    {
        const int V = Variable;
        const OperandKind None = OperandKind.None;
        const FlowKind Next = FlowKind.Next;

        yield return new(0x00, "nop", None, 0, 0, Next);
        yield return new(0x01, "break", None, 0, 0, Next);
        yield return new(0x02, "ldarg.0", None, 0, 1, Next);
        yield return new(0x03, "ldarg.1", None, 0, 1, Next);
        yield return new(0x04, "ldarg.2", None, 0, 1, Next);
        yield return new(0x05, "ldarg.3", None, 0, 1, Next);
        yield return new(0x06, "ldloc.0", None, 0, 1, Next);
        yield return new(0x07, "ldloc.1", None, 0, 1, Next);
        yield return new(0x08, "ldloc.2", None, 0, 1, Next);
        yield return new(0x09, "ldloc.3", None, 0, 1, Next);
        yield return new(0x0A, "stloc.0", None, 1, 0, Next);
        yield return new(0x0B, "stloc.1", None, 1, 0, Next);
        yield return new(0x0C, "stloc.2", None, 1, 0, Next);
        yield return new(0x0D, "stloc.3", None, 1, 0, Next);
        yield return new(0x0E, "ldarg.s", OperandKind.ShortVariable, 0, 1, Next);
        yield return new(0x0F, "ldarga.s", OperandKind.ShortVariable, 0, 1, Next);
        yield return new(0x10, "starg.s", OperandKind.ShortVariable, 1, 0, Next);
        yield return new(0x11, "ldloc.s", OperandKind.ShortVariable, 0, 1, Next);
        yield return new(0x12, "ldloca.s", OperandKind.ShortVariable, 0, 1, Next);
        yield return new(0x13, "stloc.s", OperandKind.ShortVariable, 1, 0, Next);
        yield return new(0x14, "ldnull", None, 0, 1, Next);
        yield return new(0x15, "ldc.i4.m1", None, 0, 1, Next);
        yield return new(0x16, "ldc.i4.0", None, 0, 1, Next);
        yield return new(0x17, "ldc.i4.1", None, 0, 1, Next);
        yield return new(0x18, "ldc.i4.2", None, 0, 1, Next);
        yield return new(0x19, "ldc.i4.3", None, 0, 1, Next);
        yield return new(0x1A, "ldc.i4.4", None, 0, 1, Next);
        yield return new(0x1B, "ldc.i4.5", None, 0, 1, Next);
        yield return new(0x1C, "ldc.i4.6", None, 0, 1, Next);
        yield return new(0x1D, "ldc.i4.7", None, 0, 1, Next);
        yield return new(0x1E, "ldc.i4.8", None, 0, 1, Next);
        yield return new(0x1F, "ldc.i4.s", OperandKind.Int8, 0, 1, Next);
        yield return new(0x20, "ldc.i4", OperandKind.Int32, 0, 1, Next);
        yield return new(0x21, "ldc.i8", OperandKind.Int64, 0, 1, Next);
        yield return new(0x22, "ldc.r4", OperandKind.Float32, 0, 1, Next);
        yield return new(0x23, "ldc.r8", OperandKind.Float64, 0, 1, Next);
        yield return new(0x25, "dup", None, 1, 2, Next);
        yield return new(0x26, "pop", None, 1, 0, Next);
        yield return new(0x27, "jmp", OperandKind.MethodToken, 0, 0, FlowKind.Jump);
        yield return new(0x28, "call", OperandKind.MethodToken, V, V, Next);
        yield return new(0x29, "calli", OperandKind.SignatureToken, V, V, Next);
        yield return new(0x2A, "ret", None, V, 0, FlowKind.Return);
        yield return new(0x2B, "br.s", OperandKind.ShortBranch, 0, 0, FlowKind.Branch);
        yield return new(0x2C, "brfalse.s", OperandKind.ShortBranch, 1, 0, FlowKind.ConditionalBranch);
        yield return new(0x2D, "brtrue.s", OperandKind.ShortBranch, 1, 0, FlowKind.ConditionalBranch);
        yield return new(0x2E, "beq.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x2F, "bge.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x30, "bgt.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x31, "ble.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x32, "blt.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x33, "bne.un.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x34, "bge.un.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x35, "bgt.un.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x36, "ble.un.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x37, "blt.un.s", OperandKind.ShortBranch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x38, "br", OperandKind.Branch, 0, 0, FlowKind.Branch);
        yield return new(0x39, "brfalse", OperandKind.Branch, 1, 0, FlowKind.ConditionalBranch);
        yield return new(0x3A, "brtrue", OperandKind.Branch, 1, 0, FlowKind.ConditionalBranch);
        yield return new(0x3B, "beq", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x3C, "bge", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x3D, "bgt", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x3E, "ble", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x3F, "blt", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x40, "bne.un", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x41, "bge.un", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x42, "bgt.un", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x43, "ble.un", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x44, "blt.un", OperandKind.Branch, 2, 0, FlowKind.ConditionalBranch);
        yield return new(0x45, "switch", OperandKind.Switch, 1, 0, FlowKind.Switch);
        yield return new(0x46, "ldind.i1", None, 1, 1, Next);
        yield return new(0x47, "ldind.u1", None, 1, 1, Next);
        yield return new(0x48, "ldind.i2", None, 1, 1, Next);
        yield return new(0x49, "ldind.u2", None, 1, 1, Next);
        yield return new(0x4A, "ldind.i4", None, 1, 1, Next);
        yield return new(0x4B, "ldind.u4", None, 1, 1, Next);
        yield return new(0x4C, "ldind.i8", None, 1, 1, Next);
        yield return new(0x4D, "ldind.i", None, 1, 1, Next);
        yield return new(0x4E, "ldind.r4", None, 1, 1, Next);
        yield return new(0x4F, "ldind.r8", None, 1, 1, Next);
        yield return new(0x50, "ldind.ref", None, 1, 1, Next);
        yield return new(0x51, "stind.ref", None, 2, 0, Next);
        yield return new(0x52, "stind.i1", None, 2, 0, Next);
        yield return new(0x53, "stind.i2", None, 2, 0, Next);
        yield return new(0x54, "stind.i4", None, 2, 0, Next);
        yield return new(0x55, "stind.i8", None, 2, 0, Next);
        yield return new(0x56, "stind.r4", None, 2, 0, Next);
        yield return new(0x57, "stind.r8", None, 2, 0, Next);
        yield return new(0x58, "add", None, 2, 1, Next);
        yield return new(0x59, "sub", None, 2, 1, Next);
        yield return new(0x5A, "mul", None, 2, 1, Next);
        yield return new(0x5B, "div", None, 2, 1, Next);
        yield return new(0x5C, "div.un", None, 2, 1, Next);
        yield return new(0x5D, "rem", None, 2, 1, Next);
        yield return new(0x5E, "rem.un", None, 2, 1, Next);
        yield return new(0x5F, "and", None, 2, 1, Next);
        yield return new(0x60, "or", None, 2, 1, Next);
        yield return new(0x61, "xor", None, 2, 1, Next);
        yield return new(0x62, "shl", None, 2, 1, Next);
        yield return new(0x63, "shr", None, 2, 1, Next);
        yield return new(0x64, "shr.un", None, 2, 1, Next);
        yield return new(0x65, "neg", None, 1, 1, Next);
        yield return new(0x66, "not", None, 1, 1, Next);
        yield return new(0x67, "conv.i1", None, 1, 1, Next);
        yield return new(0x68, "conv.i2", None, 1, 1, Next);
        yield return new(0x69, "conv.i4", None, 1, 1, Next);
        yield return new(0x6A, "conv.i8", None, 1, 1, Next);
        yield return new(0x6B, "conv.r4", None, 1, 1, Next);
        yield return new(0x6C, "conv.r8", None, 1, 1, Next);
        yield return new(0x6D, "conv.u4", None, 1, 1, Next);
        yield return new(0x6E, "conv.u8", None, 1, 1, Next);
        yield return new(0x6F, "callvirt", OperandKind.MethodToken, V, V, Next);
        yield return new(0x70, "cpobj", OperandKind.TypeToken, 2, 0, Next);
        yield return new(0x71, "ldobj", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0x72, "ldstr", OperandKind.StringToken, 0, 1, Next);
        yield return new(0x73, "newobj", OperandKind.MethodToken, V, 1, Next);
        yield return new(0x74, "castclass", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0x75, "isinst", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0x76, "conv.r.un", None, 1, 1, Next);
        yield return new(0x79, "unbox", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0x7A, "throw", None, 1, 0, FlowKind.Throw);
        yield return new(0x7B, "ldfld", OperandKind.FieldToken, 1, 1, Next);
        yield return new(0x7C, "ldflda", OperandKind.FieldToken, 1, 1, Next);
        yield return new(0x7D, "stfld", OperandKind.FieldToken, 2, 0, Next);
        yield return new(0x7E, "ldsfld", OperandKind.FieldToken, 0, 1, Next);
        yield return new(0x7F, "ldsflda", OperandKind.FieldToken, 0, 1, Next);
        yield return new(0x80, "stsfld", OperandKind.FieldToken, 1, 0, Next);
        yield return new(0x81, "stobj", OperandKind.TypeToken, 2, 0, Next);
        yield return new(0x82, "conv.ovf.i1.un", None, 1, 1, Next);
        yield return new(0x83, "conv.ovf.i2.un", None, 1, 1, Next);
        yield return new(0x84, "conv.ovf.i4.un", None, 1, 1, Next);
        yield return new(0x85, "conv.ovf.i8.un", None, 1, 1, Next);
        yield return new(0x86, "conv.ovf.u1.un", None, 1, 1, Next);
        yield return new(0x87, "conv.ovf.u2.un", None, 1, 1, Next);
        yield return new(0x88, "conv.ovf.u4.un", None, 1, 1, Next);
        yield return new(0x89, "conv.ovf.u8.un", None, 1, 1, Next);
        yield return new(0x8A, "conv.ovf.i.un", None, 1, 1, Next);
        yield return new(0x8B, "conv.ovf.u.un", None, 1, 1, Next);
        yield return new(0x8C, "box", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0x8D, "newarr", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0x8E, "ldlen", None, 1, 1, Next);
        yield return new(0x8F, "ldelema", OperandKind.TypeToken, 2, 1, Next);
        yield return new(0x90, "ldelem.i1", None, 2, 1, Next);
        yield return new(0x91, "ldelem.u1", None, 2, 1, Next);
        yield return new(0x92, "ldelem.i2", None, 2, 1, Next);
        yield return new(0x93, "ldelem.u2", None, 2, 1, Next);
        yield return new(0x94, "ldelem.i4", None, 2, 1, Next);
        yield return new(0x95, "ldelem.u4", None, 2, 1, Next);
        yield return new(0x96, "ldelem.i8", None, 2, 1, Next);
        yield return new(0x97, "ldelem.i", None, 2, 1, Next);
        yield return new(0x98, "ldelem.r4", None, 2, 1, Next);
        yield return new(0x99, "ldelem.r8", None, 2, 1, Next);
        yield return new(0x9A, "ldelem.ref", None, 2, 1, Next);
        yield return new(0x9B, "stelem.i", None, 3, 0, Next);
        yield return new(0x9C, "stelem.i1", None, 3, 0, Next);
        yield return new(0x9D, "stelem.i2", None, 3, 0, Next);
        yield return new(0x9E, "stelem.i4", None, 3, 0, Next);
        yield return new(0x9F, "stelem.i8", None, 3, 0, Next);
        yield return new(0xA0, "stelem.r4", None, 3, 0, Next);
        yield return new(0xA1, "stelem.r8", None, 3, 0, Next);
        yield return new(0xA2, "stelem.ref", None, 3, 0, Next);
        yield return new(0xA3, "ldelem", OperandKind.TypeToken, 2, 1, Next);
        yield return new(0xA4, "stelem", OperandKind.TypeToken, 3, 0, Next);
        yield return new(0xA5, "unbox.any", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0xB3, "conv.ovf.i1", None, 1, 1, Next);
        yield return new(0xB4, "conv.ovf.u1", None, 1, 1, Next);
        yield return new(0xB5, "conv.ovf.i2", None, 1, 1, Next);
        yield return new(0xB6, "conv.ovf.u2", None, 1, 1, Next);
        yield return new(0xB7, "conv.ovf.i4", None, 1, 1, Next);
        yield return new(0xB8, "conv.ovf.u4", None, 1, 1, Next);
        yield return new(0xB9, "conv.ovf.i8", None, 1, 1, Next);
        yield return new(0xBA, "conv.ovf.u8", None, 1, 1, Next);
        yield return new(0xC2, "refanyval", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0xC3, "ckfinite", None, 1, 1, Next);
        yield return new(0xC6, "mkrefany", OperandKind.TypeToken, 1, 1, Next);
        yield return new(0xD0, "ldtoken", OperandKind.AnyToken, 0, 1, Next);
        yield return new(0xD1, "conv.u2", None, 1, 1, Next);
        yield return new(0xD2, "conv.u1", None, 1, 1, Next);
        yield return new(0xD3, "conv.i", None, 1, 1, Next);
        yield return new(0xD4, "conv.ovf.i", None, 1, 1, Next);
        yield return new(0xD5, "conv.ovf.u", None, 1, 1, Next);
        yield return new(0xD6, "add.ovf", None, 2, 1, Next);
        yield return new(0xD7, "add.ovf.un", None, 2, 1, Next);
        yield return new(0xD8, "mul.ovf", None, 2, 1, Next);
        yield return new(0xD9, "mul.ovf.un", None, 2, 1, Next);
        yield return new(0xDA, "sub.ovf", None, 2, 1, Next);
        yield return new(0xDB, "sub.ovf.un", None, 2, 1, Next);
        yield return new(0xDC, "endfinally", None, 0, 0, FlowKind.EndFinally);
        yield return new(0xDD, "leave", OperandKind.Branch, 0, 0, FlowKind.Leave);
        yield return new(0xDE, "leave.s", OperandKind.ShortBranch, 0, 0, FlowKind.Leave);
        yield return new(0xDF, "stind.i", None, 2, 0, Next);
        yield return new(0xE0, "conv.u", None, 1, 1, Next);

        yield return new(0xFE00, "arglist", None, 0, 1, Next);
        yield return new(0xFE01, "ceq", None, 2, 1, Next);
        yield return new(0xFE02, "cgt", None, 2, 1, Next);
        yield return new(0xFE03, "cgt.un", None, 2, 1, Next);
        yield return new(0xFE04, "clt", None, 2, 1, Next);
        yield return new(0xFE05, "clt.un", None, 2, 1, Next);
        yield return new(0xFE06, "ldftn", OperandKind.MethodToken, 0, 1, Next);
        yield return new(0xFE07, "ldvirtftn", OperandKind.MethodToken, 1, 1, Next);
        yield return new(0xFE09, "ldarg", OperandKind.Variable, 0, 1, Next);
        yield return new(0xFE0A, "ldarga", OperandKind.Variable, 0, 1, Next);
        yield return new(0xFE0B, "starg", OperandKind.Variable, 1, 0, Next);
        yield return new(0xFE0C, "ldloc", OperandKind.Variable, 0, 1, Next);
        yield return new(0xFE0D, "ldloca", OperandKind.Variable, 0, 1, Next);
        yield return new(0xFE0E, "stloc", OperandKind.Variable, 1, 0, Next);
        yield return new(0xFE0F, "localloc", None, 1, 1, Next);
        yield return new(0xFE11, "endfilter", None, 1, 0, FlowKind.EndFilter);
        yield return new(0xFE12, "unaligned.", OperandKind.UInt8, 0, 0, FlowKind.Prefix);
        yield return new(0xFE13, "volatile.", None, 0, 0, FlowKind.Prefix);
        yield return new(0xFE14, "tail.", None, 0, 0, FlowKind.Prefix);
        yield return new(0xFE15, "initobj", OperandKind.TypeToken, 1, 0, Next);
        yield return new(0xFE16, "constrained.", OperandKind.TypeToken, 0, 0, FlowKind.Prefix);
        yield return new(0xFE17, "cpblk", None, 3, 0, Next);
        yield return new(0xFE18, "initblk", None, 3, 0, Next);
        yield return new(0xFE19, "no.", OperandKind.UInt8, 0, 0, FlowKind.Prefix);
        yield return new(0xFE1A, "rethrow", None, 0, 0, FlowKind.Throw);
        yield return new(0xFE1C, "sizeof", OperandKind.TypeToken, 0, 1, Next);
        yield return new(0xFE1D, "refanytype", None, 1, 1, Next);
        yield return new(0xFE1E, "readonly.", None, 0, 0, FlowKind.Prefix);
    }
}
