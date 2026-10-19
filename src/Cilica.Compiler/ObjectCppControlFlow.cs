using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

/// <summary>Two native renderings of the same checked steps; no C++ parsing or IR rewriting.</summary>
internal static class ObjectCppControlFlow
{
    // Covers the measured DiffPlex closure. Wide methods retain bounded helpers
    // until their complete native call-chain stack budgets have been measured.
    internal static bool Eligible(IrMethod method, int steps) => method.ExceptionRegions.IsEmpty && steps <= 512
        && method.Blocks.All(b => b.Terminator is not (IrLeave or IrEndFinally or IrEndFilter or IrRethrow));

    internal static void AppendHelpers(StringBuilder code, IReadOnlyList<string> steps, string frame, bool direct)
    {
        if (direct) code.Append("#if !CILICA_DIRECT_CONTROL_FLOW\n");
        code.Append("#define CILICA_STEP_RETURN(target) return target\n");
        for (int i = 0; i < steps.Count; ++i)
            code.Append("[[gnu::noinline]] static std::uint32_t s").Append(N(i))
                .Append("([[maybe_unused]] Context& context, [[maybe_unused]] ").Append(frame)
                .Append("* f) noexcept {\n").Append(steps[i]).Append("}\n");
        code.Append("static constexpr std::uint32_t (*table[])(Context&, ").Append(frame)
            .Append("*) noexcept = {").Append(string.Join(", ", Enumerable.Range(0, steps.Count).Select(i => "s" + N(i))))
            .Append("};\n#undef CILICA_STEP_RETURN\n");
        if (direct) code.Append("#endif\n");
    }

    internal static void AppendDriver(StringBuilder code, IReadOnlyList<string> steps,
        IReadOnlyDictionary<string, int> labels, string scope, bool handlers, bool direct)
    {
        if (direct)
        {
            code.Append("#if CILICA_DIRECT_CONTROL_FLOW\n")
                .Append("    // Direct native edges; roots and arena storage remain live across every edge.\n")
                .Append("#define CILICA_STEP_RETURN(target) do { if (context.pending != 0u) goto cilica_step_fault; goto cilica_step_ ## target; } while (false)\n")
                .Append("    goto cilica_step_0u;\n");
            for (int i = 0; i < steps.Count; ++i)
            {
                foreach (var label in labels.Where(p => p.Value == i))
                    code.Append("    [[maybe_unused]] cilica_step_").Append(label.Key).Append(":\n");
                code.Append("    [[maybe_unused]] cilica_step_").Append(N(i)).Append("u: {\n")
                    .Append(steps[i]).Append("    }\n");
            }
            code.Append("    cilica_step_fault:\n");
            // With no local regions, routing always exits this method. Retain
            // materialization at the same live frame/roots as the step driver.
            if (handlers) code.Append("    (void)").Append(scope).Append("::dispatch_exception(context, f);\n");
            code.Append("    cilica_step_frame_done:;\n#undef CILICA_STEP_RETURN\n#else\n");
        }
        if (handlers)
            code.Append("    std::uint32_t step = 0;\n    while (step != frame_done) {\n        f->exception_routed = false;\n        step = ")
                .Append(scope).Append("::table[step](context, f);\n        if (context.pending != 0u && !f->exception_routed) step = ")
                .Append(scope).Append("::dispatch_exception(context, f);\n    }\n");
        else
            code.Append("    std::uint32_t step = 0;\n    while (step != frame_done && context.pending == 0u) step = ")
                .Append(scope).Append("::table[step](context, f);\n");
        if (direct) code.Append("#endif\n");
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
