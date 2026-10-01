using System.Collections.Immutable;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

public enum IrType { Int32 }

/// <summary>Each operation wraps modulo 2^32; it never uses native signed overflow.</summary>
public enum Int32Operation { AddWrapping, SubtractWrapping, MultiplyWrapping }

public abstract record IrExpression
{
    public sealed record Constant(int Value) : IrExpression;
    public sealed record Parameter(int Index) : IrExpression;
    public sealed record Binary(Int32Operation Operation, int Left, int Right) : IrExpression;
}

/// <summary>Ordered typed temporary; binary operand IDs always refer to earlier values.</summary>
public sealed record IrValue(int Id, IrType Type, int ILOffset, IrExpression Expression);
public sealed record IrReturn(int ILOffset, int Value);
public sealed record IrBlock(int Id, ImmutableArray<IrValue> Values, IrReturn Return);

/// <summary>Detached translation; no readers, assembly execution or generated native code.</summary>
public sealed record IrMethod(MethodSource Source, ImmutableArray<IrType> Parameters,
    IrType ReturnType, ImmutableArray<IrBlock> Blocks)
{
    public const string Profile = "int32-straight-line-v1";
}

/// <summary>A failed translation returns diagnostics and no consumable partial IR.</summary>
public sealed record TranslationResult(IrMethod? Method, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Method is not null;
}
