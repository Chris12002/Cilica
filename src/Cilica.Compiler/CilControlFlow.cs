using System;
using System.Collections.Generic;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>Shared supported-branch vocabulary and instruction-boundary partitioning.</summary>
internal static class CilControlFlow
{
    internal sealed record Partitioned(List<(int Start, int End)> Ranges, Dictionary<int, int> BlockAt);
    internal static Partitioned Partition(MethodInput input, Action<string, string, int> fail)
    {
        var indices = input.Instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(p => p.Offset, p => p.index);
        var leaders = new SortedSet<int> { 0 };
        for (int index = 0; index < input.Instructions.Length; index++)
        {
            MethodInstruction instruction = input.Instructions[index];
            bool branch = instruction.OpCode is "br" or "br.s" || BranchComparison(instruction.OpCode) is not null;
            if (branch)
            {
                if (instruction.Targets.Length != 1 || !indices.TryGetValue(instruction.Targets[0], out int target))
                    fail(DiagnosticIds.InvalidControlTransfer, "Branch target must be an instruction boundary inside this method.", instruction.Offset);
                else leaders.Add(target);
            }
            if ((branch || instruction.OpCode == "ret") && index + 1 < input.Instructions.Length) leaders.Add(index + 1);
            if (leaders.Count > IrLimits.MaximumBlocks) fail(DiagnosticIds.TranslationLimit, "Basic-block limit exceeded.", instruction.Offset);
        }
        int[] starts = [.. leaders]; var ranges = new List<(int, int)>(); var at = new Dictionary<int, int>();
        for (int id = 0; id < starts.Length; id++)
        {
            ranges.Add((starts[id], id + 1 < starts.Length ? starts[id + 1] : input.Instructions.Length));
            at.Add(input.Instructions[starts[id]].Offset, id);
        }
        return new(ranges, at);
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
