using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;

namespace Cilica.Metadata.IL;

/// <summary>The stack effect of a call-like instruction, derived from its signature.</summary>
internal readonly record struct CallShape(int Pops, int Pushes);

/// <summary>
/// Validates a decoded method body against ECMA-335 structural rules and computes
/// basic blocks, stack depths and exception filter kinds. Stack checking is by depth
/// only; type-level verification belongs to IR construction in milestone 2.
/// </summary>
internal sealed class ControlFlowAnalyzer
{
    private enum Part
    {
        Try,
        Filter,
        Handler,
    }

    private sealed record Region(
        int Index,
        ExceptionRegionKind Kind,
        int TryStart,
        int TryEnd,
        int HandlerStart,
        int HandlerEnd,
        int FilterStart);

    private readonly record struct Scope(int Region, Part Part, int Start, int End);

    private readonly List<Instruction> _instructions;
    private readonly int _codeSize;
    private readonly int _maxStack;
    private readonly int _argumentCount;
    private readonly int _localCount;
    private readonly bool _returnsValue;
    private readonly Func<Instruction, CallShape?> _callShape;
    private readonly Action<string, string, int> _report;
    private readonly Dictionary<int, int> _indexOfOffset = [];
    private readonly List<Region> _regions = [];

    /// <param name="callShape">Stack effect of call, callvirt, newobj and calli; null when the operand is invalid.</param>
    /// <param name="report">Records a diagnostic: identifier, message and IL offset.</param>
    public ControlFlowAnalyzer(
        List<Instruction> instructions,
        int codeSize,
        int maxStack,
        int argumentCount,
        int localCount,
        bool returnsValue,
        Func<Instruction, CallShape?> callShape,
        Action<string, string, int> report)
    {
        _instructions = instructions;
        _codeSize = codeSize;
        _maxStack = maxStack;
        _argumentCount = argumentCount;
        _localCount = localCount;
        _returnsValue = returnsValue;
        _callShape = callShape;
        _report = report;
        for (int index = 0; index < instructions.Count; index++)
        {
            _indexOfOffset[instructions[index].Offset] = index;
        }
    }

    public sealed record Result(
        ImmutableArray<BasicBlockReport> Blocks,
        ImmutableDictionary<int, string> FilterKinds);

    public Result Analyze(ImmutableArray<ExceptionRegion> regions)
    {
        ValidateVariables();
        ValidateBranchTargets();
        ValidateRegions(regions);
        Scope[][] scopes = ComputeScopes();
        ValidateTransfers(scopes);
        int?[] depths = ComputeStackDepths();
        ImmutableArray<BasicBlockReport> blocks = BuildBlocks(depths);
        var filterKinds = _regions
            .Where(r => r.Kind == ExceptionRegionKind.Filter)
            .ToImmutableDictionary(r => r.Index, ClassifyFilter);
        return new Result(blocks, filterKinds);
    }

    private bool IsBoundary(int offset) => offset == _codeSize || IsInstruction(offset);

    // Prefixes and their following opcode form one instruction for control transfers
    // and exception boundaries. Keep all offsets in the index for sequential stack analysis.
    private bool IsInstruction(int offset) => _indexOfOffset.TryGetValue(offset, out int index)
        && (index == 0 || _instructions[index - 1].Code.Flow != FlowKind.Prefix);

    private void ValidateVariables()
    {
        foreach (Instruction instruction in _instructions)
        {
            if (instruction.AccessesArgument is not bool isArgument)
            {
                continue;
            }
            int limit = isArgument ? _argumentCount : _localCount;
            if (instruction.VariableIndex >= limit)
            {
                string kind = isArgument ? "argument" : "local";
                _report(DiagnosticIds.InvalidOperand,
                    $"{instruction.Code.Name} refers to {kind} {instruction.VariableIndex}, but the method has {limit} {kind}s.",
                    instruction.Offset);
            }
        }
    }

    private void ValidateBranchTargets()
    {
        foreach (Instruction instruction in _instructions)
        {
            foreach (int target in instruction.Targets)
            {
                if (!IsInstruction(target))
                {
                    _report(DiagnosticIds.InvalidControlTransfer,
                        $"{instruction.Code.Name} targets {ILDecoder.Label(target)}, which is not the start of an instruction.",
                        instruction.Offset);
                }
            }
        }
    }

    private void ValidateRegions(ImmutableArray<ExceptionRegion> regions)
    {
        var candidates = new List<Region>();
        for (int index = 0; index < regions.Length; index++)
        {
            ExceptionRegion source = regions[index];
            var region = new Region(
                index,
                source.Kind,
                source.TryOffset,
                source.TryOffset + source.TryLength,
                source.HandlerOffset,
                source.HandlerOffset + source.HandlerLength,
                source.Kind == ExceptionRegionKind.Filter ? source.FilterOffset : -1);
            string? problem = RegionProblem(region);
            if (problem is null)
            {
                candidates.Add(region);
            }
            else
            {
                _report(DiagnosticIds.InvalidExceptionRegion, $"Exception region {index}: {problem}", region.TryStart);
            }
        }

        // Every pair of protected blocks, handlers and filters must be disjoint or nested,
        // except that several handlers may protect one identical try block.
        var rejected = new HashSet<int>();
        List<Scope> intervals = [.. candidates.SelectMany(ScopesOf)];
        for (int first = 0; first < intervals.Count; first++)
        {
            for (int second = first + 1; second < intervals.Count; second++)
            {
                Scope a = intervals[first];
                Scope b = intervals[second];
                bool disjoint = a.End <= b.Start || b.End <= a.Start;
                bool nested = (a.Start >= b.Start && a.End <= b.End) || (b.Start >= a.Start && b.End <= a.End);
                bool sharedTry = a.Part == Part.Try && b.Part == Part.Try && a.Start == b.Start && a.End == b.End;
                bool sameRegion = a.Region == b.Region;
                bool identicalNonTry = a.Start == b.Start && a.End == b.End && !sharedTry;
                if ((!disjoint && !nested) || (identicalNonTry && !sameRegion) || (sameRegion && !disjoint))
                {
                    int reported = Math.Max(a.Region, b.Region);
                    if (rejected.Add(reported))
                    {
                        _report(DiagnosticIds.InvalidExceptionRegion,
                            $"Exception region {reported} overlaps region {Math.Min(a.Region, b.Region)} without nesting.",
                            Math.Max(a.Start, b.Start));
                    }
                }
            }
        }
        _regions.AddRange(candidates.Where(r => !rejected.Contains(r.Index)));
    }

    private string? RegionProblem(Region region)
    {
        if (region.TryEnd <= region.TryStart || region.HandlerEnd <= region.HandlerStart)
        {
            return "a protected block or handler is empty.";
        }
        if (region.TryEnd > _codeSize || region.HandlerEnd > _codeSize)
        {
            return "a protected block or handler extends past the end of the method body.";
        }
        if (!IsBoundary(region.TryStart) || !IsBoundary(region.TryEnd) || !IsBoundary(region.HandlerStart) || !IsBoundary(region.HandlerEnd))
        {
            return "a protected block or handler does not start and end on instruction boundaries.";
        }
        if (region.Kind == ExceptionRegionKind.Filter
            && (!IsInstruction(region.FilterStart) || region.FilterStart >= region.HandlerStart))
        {
            return "the filter does not start on an instruction before its handler.";
        }
        if (region.Kind != ExceptionRegionKind.Catch && region.Kind != ExceptionRegionKind.Filter
            && region.Kind != ExceptionRegionKind.Finally && region.Kind != ExceptionRegionKind.Fault)
        {
            return "its kind is unknown.";
        }
        return null;
    }

    private static IEnumerable<Scope> ScopesOf(Region region)
    {
        yield return new Scope(region.Index, Part.Try, region.TryStart, region.TryEnd);
        if (region.Kind == ExceptionRegionKind.Filter)
        {
            yield return new Scope(region.Index, Part.Filter, region.FilterStart, region.HandlerStart);
        }
        yield return new Scope(region.Index, Part.Handler, region.HandlerStart, region.HandlerEnd);
    }

    /// <summary>For each instruction, the protected blocks, filters and handlers that contain it.</summary>
    private Scope[][] ComputeScopes()
    {
        List<Scope> all = [.. _regions.SelectMany(ScopesOf)];
        var result = new Scope[_instructions.Count][];
        for (int index = 0; index < _instructions.Count; index++)
        {
            int offset = _instructions[index].Offset;
            result[index] = [.. all.Where(s => s.Start <= offset && offset < s.End)];
        }
        return result;
    }

    private void ValidateTransfers(Scope[][] scopes)
    {
        for (int index = 0; index < _instructions.Count; index++)
        {
            Instruction instruction = _instructions[index];
            Scope[] source = scopes[index];
            FlowKind flow = instruction.Code.Flow;

            if (flow == FlowKind.Return && source.Length > 0)
            {
                _report(DiagnosticIds.InvalidControlTransfer, "ret inside a protected block, filter or handler; use leave.", instruction.Offset);
            }
            if (flow == FlowKind.EndFinally && !source.Any(s => s.Part == Part.Handler && IsFinallyOrFault(s.Region)))
            {
                _report(DiagnosticIds.InvalidControlTransfer, "endfinally outside a finally or fault handler.", instruction.Offset);
            }
            if (flow == FlowKind.EndFilter && !source.Any(s => s.Part == Part.Filter))
            {
                _report(DiagnosticIds.InvalidControlTransfer, "endfilter outside a filter.", instruction.Offset);
            }
            if (instruction.Code.Name == "rethrow" && !IsCatchContext(source))
            {
                _report(DiagnosticIds.InvalidControlTransfer, "rethrow outside a catch handler.", instruction.Offset);
            }

            foreach (int target in instruction.Targets.Where(IsInstruction))
            {
                Scope[] destination = scopes[_indexOfOffset[target]];
                string? problem = flow == FlowKind.Leave
                    ? LeaveProblem(source, destination, target)
                    : TransferProblem(source, destination, target);
                if (problem is not null)
                {
                    _report(DiagnosticIds.InvalidControlTransfer, $"{instruction.Code.Name} to {ILDecoder.Label(target)} {problem}", instruction.Offset);
                }
            }

            if (FallsThrough(flow) && index + 1 < _instructions.Count)
            {
                string? problem = TransferProblem(source, scopes[index + 1], _instructions[index + 1].Offset);
                if (problem is not null)
                {
                    _report(DiagnosticIds.InvalidControlTransfer, $"Falling through to {ILDecoder.Label(_instructions[index + 1].Offset)} {problem}", instruction.Offset);
                }
            }
        }
    }

    private bool IsFinallyOrFault(int region) =>
        _regions.First(r => r.Index == region).Kind is ExceptionRegionKind.Finally or ExceptionRegionKind.Fault;

    private bool IsCatchContext(Scope[] scopes)
    {
        // A nested try is still in its enclosing catch, but a nested filter/finally/fault
        // is a different exception context. A filter's handler is also a catch handler.
        Scope[] handlers = [.. scopes.Where(s => s.Part != Part.Try)];
        if (handlers.Length == 0)
        {
            return false;
        }
        Scope innermost = handlers.MinBy(s => s.End - s.Start);
        return innermost.Part == Part.Handler && !IsFinallyOrFault(innermost.Region);
    }

    private static bool FallsThrough(FlowKind flow) =>
        flow is FlowKind.Next or FlowKind.ConditionalBranch or FlowKind.Switch or FlowKind.Prefix;

    /// <summary>Ordinary transfers stay within the same scopes, or enter a protected block at its first instruction.</summary>
    private static string? TransferProblem(Scope[] source, Scope[] destination, int target)
    {
        if (source.Any(s => !destination.Contains(s)))
        {
            return "leaves a protected block, filter or handler without leave, endfinally or endfilter.";
        }
        if (destination.Any(s => !source.Contains(s) && !(s.Part == Part.Try && s.Start == target)))
        {
            return "enters a protected block, filter or handler other than at the start of a protected block.";
        }
        return null;
    }

    /// <summary>leave exits protected blocks and catch handlers, but not filters, finally or fault handlers.</summary>
    private string? LeaveProblem(Scope[] source, Scope[] destination, int target)
    {
        foreach (Scope exited in source.Where(s => !destination.Contains(s)))
        {
            if (exited.Part == Part.Filter)
            {
                return "exits a filter; filters end with endfilter.";
            }
            if (exited.Part == Part.Handler && IsFinallyOrFault(exited.Region))
            {
                return "exits a finally or fault handler; they end with endfinally.";
            }
        }
        if (destination.Any(s => !source.Contains(s) && !(s.Part == Part.Try && s.Start == target)))
        {
            return "enters a protected block, filter or handler other than at the start of a protected block.";
        }
        return null;
    }

    /// <summary>Computes the evaluation-stack depth on entry to each instruction.</summary>
    private int?[] ComputeStackDepths()
    {
        var depths = new int?[_instructions.Count];
        var reported = new HashSet<int>();
        bool maximumReported = false;
        var work = new Stack<int>();
        var tryStarts = _regions.Select(r => r.TryStart).ToHashSet();

        void Enter(int index, int depth, int from)
        {
            if (depths[index] is int existing)
            {
                if (existing != depth && reported.Add(index))
                {
                    _report(DiagnosticIds.InconsistentStack,
                        $"Stack depth at {ILDecoder.Label(_instructions[index].Offset)} is {existing} on one path and {depth} on another.", from);
                }
                return;
            }
            if (depth != 0 && tryStarts.Contains(_instructions[index].Offset))
            {
                _report(DiagnosticIds.InconsistentStack,
                    $"The stack must be empty on entry to the protected block at {ILDecoder.Label(_instructions[index].Offset)}, but holds {depth} values.", from);
                return;
            }
            depths[index] = depth;
            work.Push(index);
        }

        if (_instructions.Count > 0)
        {
            Enter(0, 0, 0);
        }
        foreach (Region region in _regions)
        {
            int handlerDepth = region.Kind is ExceptionRegionKind.Catch or ExceptionRegionKind.Filter ? 1 : 0;
            Enter(_indexOfOffset[region.HandlerStart], handlerDepth, region.HandlerStart);
            if (region.Kind == ExceptionRegionKind.Filter)
            {
                Enter(_indexOfOffset[region.FilterStart], 1, region.FilterStart);
            }
        }

        while (work.Count > 0)
        {
            int index = work.Pop();
            Instruction instruction = _instructions[index];
            int depth = depths[index]!.Value;
            (int pops, int pushes)? effect = StackEffect(instruction);
            if (effect is not var (pops, pushes))
            {
                continue;
            }
            if (depth < pops)
            {
                _report(DiagnosticIds.InconsistentStack,
                    $"{instruction.Code.Name} needs {pops} stack values but {depth} are available.", instruction.Offset);
                continue;
            }

            int after = depth - pops + pushes;
            if (after > _maxStack && !maximumReported)
            {
                maximumReported = true;
                _report(DiagnosticIds.InconsistentStack,
                    $"The stack reaches {after} values, exceeding the declared maximum of {_maxStack}.", instruction.Offset);
            }

            switch (instruction.Code.Flow)
            {
                case FlowKind.Return:
                    if (after != 0)
                    {
                        _report(DiagnosticIds.InconsistentStack, $"ret leaves {after} extra values on the stack.", instruction.Offset);
                    }
                    continue;
                case FlowKind.EndFilter:
                    if (depth != 1)
                    {
                        _report(DiagnosticIds.InconsistentStack, $"endfilter requires exactly one stack value but {depth} are present.", instruction.Offset);
                    }
                    continue;
                case FlowKind.Jump:
                    if (depth != 0)
                    {
                        _report(DiagnosticIds.InconsistentStack, "jmp requires an empty stack.", instruction.Offset);
                    }
                    continue;
                case FlowKind.Throw or FlowKind.EndFinally:
                    continue;
                case FlowKind.Leave:
                    after = 0;
                    break;
            }

            foreach (int target in instruction.Targets)
            {
                if (_indexOfOffset.TryGetValue(target, out int targetIndex))
                {
                    Enter(targetIndex, after, instruction.Offset);
                }
            }
            if (FallsThrough(instruction.Code.Flow))
            {
                if (index + 1 < _instructions.Count)
                {
                    Enter(index + 1, after, instruction.Offset);
                }
                else
                {
                    _report(DiagnosticIds.InvalidControlTransfer, "Control falls through the end of the method body.", instruction.Offset);
                }
            }
        }
        return depths;
    }

    private (int Pops, int Pushes)? StackEffect(Instruction instruction)
    {
        OpCode code = instruction.Code;
        if (code.Flow == FlowKind.Return)
        {
            return (_returnsValue ? 1 : 0, 0);
        }
        if (code.Pops != OpCodes.Variable && code.Pushes != OpCodes.Variable)
        {
            return (code.Pops, code.Pushes);
        }
        CallShape? shape = _callShape(instruction);
        return shape is CallShape known ? (known.Pops, known.Pushes) : null;
    }

    private ImmutableArray<BasicBlockReport> BuildBlocks(int?[] depths)
    {
        if (_instructions.Count == 0)
        {
            return [];
        }

        var leaders = new SortedSet<int> { 0 };
        for (int index = 0; index < _instructions.Count; index++)
        {
            Instruction instruction = _instructions[index];
            foreach (int target in instruction.Targets.Where(IsInstruction))
            {
                leaders.Add(_indexOfOffset[target]);
            }
            // Any instruction that can transfer control ends its block.
            if (instruction.Code.Flow is not (FlowKind.Next or FlowKind.Prefix) && index + 1 < _instructions.Count)
            {
                leaders.Add(index + 1);
            }
        }
        foreach (Region region in _regions)
        {
            foreach (int offset in new[] { region.TryStart, region.TryEnd, region.HandlerStart, region.HandlerEnd, region.FilterStart })
            {
                if (_indexOfOffset.TryGetValue(offset, out int index))
                {
                    leaders.Add(index);
                }
            }
        }

        List<int> starts = [.. leaders];
        var blockOfInstruction = new int[_instructions.Count];
        for (int block = 0; block < starts.Count; block++)
        {
            int end = block + 1 < starts.Count ? starts[block + 1] : _instructions.Count;
            for (int index = starts[block]; index < end; index++)
            {
                blockOfInstruction[index] = block;
            }
        }

        var blocks = ImmutableArray.CreateBuilder<BasicBlockReport>(starts.Count);
        for (int block = 0; block < starts.Count; block++)
        {
            int lastIndex = (block + 1 < starts.Count ? starts[block + 1] : _instructions.Count) - 1;
            Instruction last = _instructions[lastIndex];
            var successors = new SortedSet<int>();
            foreach (int target in last.Targets.Where(IsInstruction))
            {
                successors.Add(blockOfInstruction[_indexOfOffset[target]]);
            }
            if (FallsThrough(last.Code.Flow) && lastIndex + 1 < _instructions.Count)
            {
                successors.Add(blockOfInstruction[lastIndex + 1]);
            }
            blocks.Add(new BasicBlockReport(
                block,
                _instructions[starts[block]].Offset,
                last.End,
                depths[starts[block]],
                [.. successors]));
        }
        return blocks.MoveToImmutable();
    }

    /// <summary>
    /// A filter is <c>type-test</c> when it only tests the exception object's type:
    /// it calls nothing, reads no argument, field or shared local, cannot throw and branches
    /// only within itself without cycles. Its evaluation order relative to inner finally blocks is then
    /// unobservable (ADR 0004 stage A).
    /// </summary>
    private string ClassifyFilter(Region region)
    {
        int first = _indexOfOffset[region.FilterStart];
        int end = _indexOfOffset[region.HandlerStart];
        var filterLocals = new HashSet<int>();
        for (int index = first; index < end; index++)
        {
            Instruction instruction = _instructions[index];
            if (instruction.AccessesArgument == false && instruction.Code.Name.StartsWith("stloc", StringComparison.Ordinal))
            {
                filterLocals.Add(instruction.VariableIndex);
            }
        }

        // A local written by the filter may only be used by this filter and its handler.
        foreach (Instruction instruction in _instructions)
        {
            if (instruction.AccessesArgument != false || !filterLocals.Contains(instruction.VariableIndex))
            {
                continue;
            }
            bool insideRegion = instruction.Offset >= region.FilterStart && instruction.Offset < region.HandlerEnd;
            if (!insideRegion || instruction.Code.Name.StartsWith("ldloca", StringComparison.Ordinal))
            {
                return "general";
            }
        }

        for (int index = first; index < end; index++)
        {
            Instruction instruction = _instructions[index];
            string name = instruction.Code.Name;
            bool allowed = name switch
            {
                "nop" or "isinst" or "ldnull" or "dup" or "pop" or "endfilter"
                    or "ceq" or "cgt" or "cgt.un" or "clt" or "clt.un" or "and" or "or" or "xor" or "not" => true,
                _ when name.StartsWith("ldc.i4", StringComparison.Ordinal) => true,
                _ when instruction.AccessesArgument == false => filterLocals.Contains(instruction.VariableIndex),
                _ when instruction.Code.Flow is FlowKind.Branch or FlowKind.ConditionalBranch =>
                    instruction.Targets.All(t => IsInstruction(t) && t >= region.FilterStart && t < region.HandlerStart),
                _ => false,
            };
            if (!allowed)
            {
                return "general";
            }
        }
        // A nonterminating filter prevents the unwind pass (and its finally side effects).
        // An opcode whitelist alone cannot justify moving it after unwinding.
        return FilterHasCycle(first, end) ? "general" : "type-test";
    }

    private bool FilterHasCycle(int first, int end)
    {
        IEnumerable<int> Successors(int index)
        {
            foreach (int target in _instructions[index].Targets)
            {
                yield return _indexOfOffset[target];
            }
            if (FallsThrough(_instructions[index].Code.Flow) && index + 1 < end)
            {
                yield return index + 1;
            }
        }

        // Topological removal is iterative, so large filters cannot exhaust the host stack.
        var incoming = new int[end - first];
        for (int index = first; index < end; index++)
        {
            foreach (int successor in Successors(index))
            {
                incoming[successor - first]++;
            }
        }
        var ready = new Queue<int>();
        for (int index = first; index < end; index++)
        {
            if (incoming[index - first] == 0) ready.Enqueue(index);
        }
        int removed = 0;
        while (ready.TryDequeue(out int index))
        {
            removed++;
            foreach (int successor in Successors(index))
            {
                if (--incoming[successor - first] == 0) ready.Enqueue(successor);
            }
        }
        return removed != end - first;
    }
}
