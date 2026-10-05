using System;
using System.Collections.Generic;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Shared supported-branch vocabulary and instruction-boundary partitioning.</summary>
internal static class CilControlFlow
{
    internal sealed record Partitioned(List<(int Start, int End)> Ranges, Dictionary<int, int> BlockAt)
    {
        // Extra comparison blocks occupy the switch operand span in IR source order.
        // Input control transfers still resolve only through real instruction leaders.
        internal Dictionary<int, int> SwitchCases { get; init; } = [];
    }
    internal static Partitioned Partition(MethodInput input, Action<string, string, int> fail, IEnumerable<int>? boundaries = null, bool exceptions = false, bool switches = false)
    {
        var indices = input.Instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(p => p.Offset, p => p.index);
        var leaders = new SortedSet<int> { 0 };
        if (boundaries is not null)
            foreach (int offset in boundaries)
            {
                if (offset == input.CodeSize) continue;
                if (!indices.TryGetValue(offset, out int leader)) fail(DiagnosticIds.InvalidControlTransfer, "Exception boundary is not an instruction boundary.", offset);
                else if (leader > 0 && input.Instructions[leader - 1].OpCode.EndsWith(".", StringComparison.Ordinal))
                    fail(DiagnosticIds.InvalidControlTransfer, "Exception boundaries cannot split an instruction prefix sequence.", offset);
                else leaders.Add(leader);
            }
        for (int index = 0; index < input.Instructions.Length; index++)
        {
            MethodInstruction instruction = input.Instructions[index];
            bool branch = instruction.OpCode is "br" or "br.s" || exceptions && instruction.OpCode is ("leave" or "leave.s") || BranchComparison(instruction.OpCode) is not null;
            bool table = switches && instruction.OpCode == "switch";
            if (table)
            {
                if (instruction.Targets.IsDefault || instruction.Targets.Length > IrLimits.MaximumBlocks
                    || instruction.Length != 5L + 4L * instruction.Targets.Length)
                    fail(DiagnosticIds.TranslationLimit, "Switch table is malformed or exceeds the block budget.", instruction.Offset);
                foreach (int destination in instruction.Targets)
                {
                    if (!indices.TryGetValue(destination, out int target))
                        fail(DiagnosticIds.InvalidControlTransfer, "Switch target must be an instruction boundary inside this method.", instruction.Offset);
                    else if (target > 0 && input.Instructions[target - 1].OpCode.EndsWith(".", StringComparison.Ordinal))
                        fail(DiagnosticIds.InvalidControlTransfer, "Switch targets cannot enter an instruction prefix sequence.", instruction.Offset);
                    else leaders.Add(target);
                }
            }
            if (branch)
            {
                if (instruction.Targets.Length != 1 || !indices.TryGetValue(instruction.Targets[0], out int target))
                    fail(DiagnosticIds.InvalidControlTransfer, "Branch target must be an instruction boundary inside this method.", instruction.Offset);
                else if (target > 0 && input.Instructions[target - 1].OpCode.EndsWith(".", StringComparison.Ordinal))
                    fail(DiagnosticIds.InvalidControlTransfer, "Branch targets cannot enter an instruction prefix sequence.", instruction.Offset);
                else leaders.Add(target);
            }
            if ((branch || table || instruction.OpCode == "ret" || exceptions && instruction.OpCode is ("throw" or "rethrow" or "endfinally" or "endfilter")) && index + 1 < input.Instructions.Length) leaders.Add(index + 1);
            if (leaders.Count > IrLimits.MaximumBlocks) fail(DiagnosticIds.TranslationLimit, "Basic-block limit exceeded.", instruction.Offset);
        }
        int[] starts = [.. leaders]; var ranges = new List<(int, int)>(); var at = new Dictionary<int, int>();
        var cases = new Dictionary<int, int>();
        for (int index = 0; index < starts.Length; index++)
        {
            int end = index + 1 < starts.Length ? starts[index + 1] : input.Instructions.Length;
            at.Add(input.Instructions[starts[index]].Offset, ranges.Count);
            ranges.Add((starts[index], end));
            var last = input.Instructions[end - 1];
            if (switches && last.OpCode == "switch")
            {
                cases.Add(ranges.Count - 1, 0);
                for (int item = 1; item < last.Targets.Length; ++item)
                {
                    if (ranges.Count == IrLimits.MaximumBlocks)
                        fail(DiagnosticIds.TranslationLimit, "Expanded switch exceeds the basic-block limit.", last.Offset);
                    cases.Add(ranges.Count, item); ranges.Add((end - 1, end));
                }
            }
            if (ranges.Count > IrLimits.MaximumBlocks)
                fail(DiagnosticIds.TranslationLimit, "Expanded switch exceeds the basic-block limit.", last.Offset);
        }
        return new(ranges, at) { SwitchCases = cases };
    }

    internal static Int32Comparison? BranchComparison(string opcode) => (opcode.EndsWith(".s", StringComparison.Ordinal) ? opcode[..^2] : opcode) switch
    {
        "brfalse" => Int32Comparison.Zero, "brtrue" => Int32Comparison.Nonzero,
        "beq" => Int32Comparison.Equal, "bne.un" => Int32Comparison.NotEqual,
        "blt" => Int32Comparison.LessSigned, "bgt" => Int32Comparison.GreaterSigned,
        "ble" => Int32Comparison.LessOrEqualSigned, "bge" => Int32Comparison.GreaterOrEqualSigned,
        "blt.un" => Int32Comparison.LessUnsigned, "bgt.un" => Int32Comparison.GreaterUnsigned,
        "ble.un" => Int32Comparison.LessOrEqualUnsigned, "bge.un" => Int32Comparison.GreaterOrEqualUnsigned,
        _ => null,
    };
}
