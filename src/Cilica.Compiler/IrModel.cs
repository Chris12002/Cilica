using System.Collections.Immutable;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

public enum IrType { Int32 }

/// <summary>32-bit operations with explicit wrapping/checked arithmetic; shifts mask the count to five bits.</summary>
public enum Int32Operation
{
    AddWrapping, SubtractWrapping, MultiplyWrapping, BitwiseAnd, BitwiseOr, BitwiseXor,
    ShiftLeft, ShiftRightSigned, ShiftRightUnsigned,
    DivideSigned, DivideUnsigned,
    AddCheckedSigned, SubtractCheckedSigned, MultiplyCheckedSigned,
    AddCheckedUnsigned, SubtractCheckedUnsigned, MultiplyCheckedUnsigned,
}

public enum Int32UnaryOperation { BitwiseNot }

/// <summary>Current primitive profile propagates faults to the method's caller.</summary>
public enum IrExceptionTarget { MethodExit }
public enum Int32Fault { None = 0, DivideByZero = 1, Overflow = 2 }

public enum Int32Comparison
{
    Equal, NotEqual, LessSigned, GreaterSigned, LessOrEqualSigned, GreaterOrEqualSigned,
    LessUnsigned, GreaterUnsigned, LessOrEqualUnsigned, GreaterOrEqualUnsigned, Zero, Nonzero,
}

public abstract record IrExpression
{
    public sealed record Constant(int Value) : IrExpression;
    public sealed record Parameter(int Index) : IrExpression;
    public sealed record Local(int Index) : IrExpression;
    public sealed record StackInput(int Index) : IrExpression;
    public sealed record Binary(Int32Operation Operation, int Left, int Right) : IrExpression;
    public sealed record Unary(Int32UnaryOperation Operation, int Operand) : IrExpression;
    public sealed record Compare(Int32Comparison Comparison, int Left, int Right) : IrExpression;
    public sealed record Call(IrMethodId Target, ImmutableArray<int> Arguments) : IrExpression;
}

/// <summary>Block-local typed temporary; operands always refer to earlier values in that block.</summary>
public sealed record IrValue(int Id, IrType Type, int ILOffset, IrExpression Expression);
public abstract record IrStatement
{
    public sealed record Define(IrValue Value, IrExceptionTarget? Exceptional = null) : IrStatement;
    public sealed record StoreLocal(int ILOffset, int Local, int Value) : IrStatement;
}

/// <summary>Arguments are immutable value IDs, ordered from stack bottom to top.</summary>
public sealed record IrEdge(int Target, ImmutableArray<int> Arguments);
public abstract record IrTerminator(int ILOffset);
public sealed record IrReturn(int ILOffset, int Value) : IrTerminator(ILOffset);
public sealed record IrJump(int ILOffset, IrEdge Edge) : IrTerminator(ILOffset);
public sealed record IrBranch(int ILOffset, Int32Comparison Comparison, int Left, int? Right,
    IrEdge Taken, IrEdge NotTaken) : IrTerminator(ILOffset);

public sealed record IrBlock(int Id, int ILOffset, ImmutableArray<IrType> Inputs,
    ImmutableArray<IrStatement> Statements, IrTerminator Terminator)
{
    public ImmutableArray<IrValue> Values => Statements.IsDefault ? default :
        [.. Statements.OfType<IrStatement.Define>().Select(s => s.Value)];
}

/// <summary>Detached translation; no readers, assembly execution or generated native code.</summary>
public sealed record IrMethod(MethodSource Source, ImmutableArray<IrType> Parameters,
    IrType ReturnType, ImmutableArray<IrBlock> Blocks, ImmutableArray<IrType> Locals)
{
    public const string Profile = IrProgram.Profile;
}

/// <summary>Aggregate bounds also cover synthetic stack inputs and edge copies.</summary>
public static class IrLimits
{
    public const int MaximumBlocks = 4096;
    public const int MaximumStatements = 65536;
    public const int MaximumInputs = 65536;
    public const int MaximumEdgeArguments = 65536;
    public const int MaximumParameters = 4096;
    public const int MaximumLocals = 4096;
}

/// <summary>A failed translation returns diagnostics and no consumable partial IR.</summary>
public sealed record TranslationResult(IrMethod? Method, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Method is not null;
}

/// <summary>Content-scoped MethodDef identity, independent of display names and readers.</summary>
public sealed record IrMethodId(string ContentHash, int Token)
{
    public static IrMethodId From(MethodSource source) =>
        new(source.ContentHash.ToLowerInvariant(), source.Token);
}

/// <summary>One explicit entry and its complete reachable direct-call closure.</summary>
public sealed record IrProgram(IrMethodId Entry, ImmutableArray<IrMethod> Methods)
{
    public const string Profile = "int32-pending-fault-v1";
}

public static class ProgramLimits
{
    public const int MaximumMethods = 256;
    public const int MaximumBodyBytes = 1048576;
    public const int MaximumInstructions = 65536;
    public const int MaximumCallArguments = 65536;
    // Existing IR limits also apply to totals across all methods in a program.
}

public sealed record ProgramTranslationResult(IrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
