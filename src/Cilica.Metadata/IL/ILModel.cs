using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Metadata.IL;

/// <summary>Decoded method bodies of one input file, in deterministic order.</summary>
public sealed record ILReport(string InputName, ImmutableArray<MethodBodyReport> Methods, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

/// <param name="Name">The method as <c>Type::Name(parameter types)</c>.</param>
/// <param name="Locals">Local variable types in slot order.</param>
public sealed record MethodBodyReport(
    string Name,
    int CodeSize,
    int MaxStack,
    bool LocalsInitialized,
    ImmutableArray<string> Locals,
    ImmutableArray<ExceptionRegionReport> Regions,
    ImmutableArray<BasicBlockReport> Blocks,
    ImmutableArray<InstructionReport> Instructions);

/// <param name="Kind">catch, filter, finally or fault.</param>
/// <param name="FilterKind">For filters: <c>type-test</c> when evaluation order is unobservable
/// (ADR 0004 stage A), otherwise <c>general</c>.</param>
public sealed record ExceptionRegionReport(
    string Kind,
    int TryStart,
    int TryEnd,
    int HandlerStart,
    int HandlerEnd,
    int? FilterStart,
    string? CatchType,
    string? FilterKind);

/// <param name="StackOnEntry">Evaluation-stack depth on entry, or null when the block is unreachable.</param>
/// <param name="Successors">Indices of blocks reached by falling through or branching, excluding exceptional flow.</param>
public sealed record BasicBlockReport(int Index, int Start, int End, int? StackOnEntry, ImmutableArray<int> Successors);

public sealed record InstructionReport(int Offset, string OpCode, string? Operand);
