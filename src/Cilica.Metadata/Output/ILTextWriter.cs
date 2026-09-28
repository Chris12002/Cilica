using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Output;

/// <summary>Renders an <see cref="ILReport"/> as text grouped by basic block, with LF line endings.</summary>
public static class ILTextWriter
{
    public static string Write(ILReport report)
    {
        var text = new StringBuilder();
        void Line(string line) => text.Append(line).Append('\n');

        Line($"input: {report.InputName}");
        Line($"methods: {Number(report.Methods.Length)}");
        foreach (MethodBodyReport method in report.Methods)
        {
            Line("");
            Line($"method {method.Name}");
            Line($"  code size {Number(method.CodeSize)}, max stack {Number(method.MaxStack)}" + (method.LocalsInitialized ? ", locals initialized" : ""));
            if (!method.Locals.IsEmpty)
            {
                Line("  locals:");
                for (int index = 0; index < method.Locals.Length; index++)
                {
                    Line($"    [{Number(index)}] {method.Locals[index]}");
                }
            }
            if (!method.Regions.IsEmpty)
            {
                Line("  exception regions:");
                foreach (ExceptionRegionReport region in method.Regions)
                {
                    Line("    " + FormatRegion(region));
                }
            }

            if (method.Blocks.IsEmpty)
            {
                foreach (InstructionReport instruction in method.Instructions)
                {
                    Line("    " + FormatInstruction(instruction));
                }
                continue;
            }

            int next = 0;
            foreach (BasicBlockReport block in method.Blocks)
            {
                string stack = block.StackOnEntry is int depth ? "stack " + Number(depth) : "unreachable";
                string successors = block.Successors.IsEmpty ? "" : " -> " + string.Join(", ", block.Successors.Select(s => "B" + Number(s)));
                Line($"  B{Number(block.Index)} [{Label(block.Start)}, {Label(block.End)}) {stack}{successors}");
                while (next < method.Instructions.Length && method.Instructions[next].Offset < block.End)
                {
                    Line("    " + FormatInstruction(method.Instructions[next]));
                    next++;
                }
            }
        }

        Line("");
        List<string> diagnostics = [.. report.Diagnostics.Select(d =>
            $"{d.Id} {d.Severity.ToDisplayString()}: {d.Message}" + (d.Location is null ? "" : $" [{d.Location}]"))];
        if (diagnostics.Count == 0)
        {
            Line("diagnostics: none");
        }
        else
        {
            Line("diagnostics:");
            diagnostics.ForEach(d => Line("  " + d));
        }
        return text.ToString();
    }

    private static string FormatRegion(ExceptionRegionReport region)
    {
        var builder = new StringBuilder($"try [{Label(region.TryStart)}, {Label(region.TryEnd)}) {region.Kind}");
        if (region.CatchType is not null)
        {
            builder.Append(' ').Append(region.CatchType);
        }
        if (region.FilterStart is int filter)
        {
            builder.Append(" at ").Append(Label(filter));
            if (region.FilterKind is not null)
            {
                builder.Append(" (").Append(region.FilterKind).Append(')');
            }
        }
        builder.Append(", handler [").Append(Label(region.HandlerStart)).Append(", ").Append(Label(region.HandlerEnd)).Append(')');
        return builder.ToString();
    }

    private static string FormatInstruction(InstructionReport instruction) =>
        Label(instruction.Offset) + ": " + instruction.OpCode + (instruction.Operand is null ? "" : " " + instruction.Operand);

    private static string Label(int offset) => "IL_" + offset.ToString("x4", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
