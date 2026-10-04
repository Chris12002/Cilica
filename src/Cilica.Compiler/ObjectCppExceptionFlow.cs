using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Compiler;

internal static class ObjectCppExceptionFlow
{
    internal const string Storage = """
struct ExceptionContinuation { std::uint32_t block = 0, cursor = 0, destination = frame_done; };
struct ExceptionRegionDescriptor {
    std::uint32_t clause, try_start, try_end, handler, filter, caught_root, saved_root, handler_input, filter_input, kind;
    const Descriptor* type;
};

""";
    internal static void Append(StringBuilder text, IrMethod method, ObjectFrameLayout layout, string frame, Func<ObjectTypeId, string> symbol)
    {
        string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        text.Append("static constexpr std::array<std::uint32_t, ").Append(N(method.Blocks.Length)).Append("> exception_blocks = {")
            .AppendJoin(", ", method.Blocks.Select(b => "b" + N(b.Id))).Append("};\n");
        text.Append("static constexpr std::array<ExceptionRegionDescriptor, ").Append(N(method.ExceptionRegions.Length)).Append("> exception_regions = {{\n");
        for (int i = 0; i < method.ExceptionRegions.Length; ++i)
        {
            var region = method.ExceptionRegions[i]; bool handlerInput = region.Kind is IrExceptionClause.Catch or IrExceptionClause.Filter;
            text.Append("    {").Append(N(i)).Append(", ").Append(N(region.TryStart)).Append(", ").Append(N(region.TryEnd))
                .Append(", b").Append(N(region.HandlerStart)).Append(", ").Append(region.FilterStart is int filter ? "b" + N(filter) : "frame_done")
                .Append(", ").Append(N(layout.Index(ObjectRootKind.Exception, i * 2))).Append(", ").Append(N(layout.Index(ObjectRootKind.Exception, i * 2 + 1)))
                .Append(", ").Append(handlerInput ? N(layout.Index(ObjectRootKind.BlockInput, 0, region.HandlerStart)) : "frame_done")
                .Append(", ").Append(region.FilterStart is int first ? N(layout.Index(ObjectRootKind.BlockInput, 0, first)) : "frame_done")
                .Append(", ").Append(N((int)region.Kind)).Append(", ").Append(region.CatchType is { } type ? "&" + symbol(type) + "_descriptor" : "nullptr").Append("},\n");
        }
        text.Append("}};\nstatic constexpr std::array<std::uint32_t, ").Append(N(method.ExceptionRegions.Length)).Append("> exception_order = {")
            .AppendJoin(", ", method.ExceptionRegions.Select((region, index) => (region, index)).OrderBy(p => p.region.TryEnd - p.region.TryStart)
                .ThenBy(p => p.index).Select(p => N(p.index))).Append("};\n");
        text.Append(Helpers.Replace("$FRAME", frame, StringComparison.Ordinal));
    }
    private const string Helpers = """
[[maybe_unused, gnu::noinline]] static std::uint32_t invalid_exception_route(Context& c, $FRAME* f) noexcept {
    f->exception_routed = true; c.exception = nullptr; c.pending = 6; return frame_done;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t enter_catch(Context& c, $FRAME* f, const ExceptionRegionDescriptor& region) noexcept {
    f->roots[region.caught_root] = c.exception; f->exception_origins[region.clause * 2] = exception_origin(c);
    f->roots[region.handler_input] = c.exception;
    c.exception = nullptr; set_exception_origin(c, {}); return region.handler;
}
[[maybe_unused, gnu::noinline]] static std::uint32_t search_exception(Context& c, $FRAME* f,
    std::uint32_t origin, std::uint32_t cursor, std::uint32_t destination) noexcept {
    f->exception_routed = true;
    for (; cursor < exception_order.size(); ++cursor) {
        const auto& region = exception_regions[exception_order[cursor]];
        if (origin < region.try_start || origin >= region.try_end) continue;
        if (c.pending == 0) {
            if (region.kind != 2 || (destination >= region.try_start && destination < region.try_end)) continue;
        } else if (region.kind == 0) {
            if (derives_from(c.exception->type, region.type)) return enter_catch(c, f, region);
            continue;
        }
        if (c.pending != 0) {
            if (!suspend_exception(c, f->roots[region.saved_root], f->exception_origins[region.clause * 2 + 1]))
                return invalid_exception_route(c, f);
        } else { f->roots[region.saved_root] = nullptr; f->exception_origins[region.clause * 2 + 1] = {}; }
        f->exception_continuations[region.clause] = {origin, cursor + 1, destination};
        if (region.kind == 1) {
            f->roots[region.filter_input] = f->roots[region.saved_root]; return region.filter;
        }
        return region.handler;
    }
    return c.pending != 0 || destination == frame_done ? frame_done : exception_blocks[destination];
}
[[maybe_unused, gnu::noinline]] static std::uint32_t dispatch_exception(Context& c, $FRAME* f) noexcept {
    (void)materialize_exception(c);
    if (c.exception == nullptr || (c.pending != 13 && runtime_exception(c.pending) == nullptr)) return frame_done;
    return search_exception(c, f, f->active_block, 0, frame_done);
}
[[maybe_unused, gnu::noinline]] static std::uint32_t leave_exception_scope(Context& c, $FRAME* f, std::uint32_t destination) noexcept {
    if (c.pending != 0 || c.exception != nullptr || destination >= exception_blocks.size()) return invalid_exception_route(c, f);
    return search_exception(c, f, f->active_block, 0, destination);
}
[[maybe_unused, gnu::noinline]] static std::uint32_t finish_exception_scope(Context& c, $FRAME* f, std::uint32_t clause, bool accepted) noexcept {
    if (clause >= exception_regions.size() || c.pending != 0 || c.exception != nullptr) return invalid_exception_route(c, f);
    const auto& region = exception_regions[clause]; const auto origin = f->exception_origins[clause * 2 + 1];
    if (origin.status != 0 && !restore_exception(c, f->roots[region.saved_root], origin)) return invalid_exception_route(c, f);
    f->roots[region.saved_root] = nullptr; f->exception_origins[clause * 2 + 1] = {};
    f->exception_routed = true;
    if (region.kind == 1 && accepted) return enter_catch(c, f, region);
    const auto continuation = f->exception_continuations[clause];
    return search_exception(c, f, continuation.block, continuation.cursor, continuation.destination);
}
[[maybe_unused, gnu::noinline]] static void rethrow_exception(Context& c, $FRAME* f, std::uint32_t clause) noexcept {
    if (clause >= exception_regions.size() || !restore_exception(c, f->roots[exception_regions[clause].caught_root], f->exception_origins[clause * 2]))
        (void)invalid_exception_route(c, f);
}

""";
}
