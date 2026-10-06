using System.Globalization;
using System.Linq;

namespace Cilica.Compiler;

internal static class EmbeddingRecordAbi
{
    internal const string Types = """
        #ifndef CILICA_V3_RECORD
        #define CILICA_V3_RECORD UINT32_C(4)
        #define CILICA_V3_CAP_RECORDS UINT64_C(2048)
        #endif
        """;
    internal static string Declaration(string prefix) => $$"""
        CILICA_V3_API uint32_t {{prefix}}_invoke_records(uint64_t runtime, uint32_t export_id, const cilica_v3_value* arguments, uint32_t count, cilica_v3_value* fields, uint32_t capacity, cilica_v3_result* result, uint64_t* exception) CILICA_V3_NOEXCEPT;
        """;
    internal static string Emit(string prefix, bool capture) => $$"""
        extern "C" uint32_t {{prefix}}_invoke_records(uint64_t runtime, uint32_t export_id, const cilica_v3_value* parameters, uint32_t count, cilica_v3_value* fields, uint32_t capacity, cilica_v3_result* result, uint64_t* exception) noexcept {
            {{(capture ? "" : "if (exception != nullptr) return CILICA_V3_INVALID_ARGUMENT;")}}
            return {{prefix}}_invoke_core(runtime, export_id, parameters, count, result, exception, fields, capacity, true);
        }
        """;

    internal static string ReturnHelper(EmbeddingRecordPlan.Record record, IrMethod method, uint exportId, int inputCount)
    {
        var slots = record.Layout.Slots;
        int narrow = 0, wide = 0;
        var indices = new int[slots.Length];
        for (int i = 0; i < slots.Length; ++i) indices[i] = slots[i].Storage is ObjectStorage.Int64 ? wide++ : narrow++;
        string number(uint value) => value.ToString(CultureInfo.InvariantCulture);
        string origin = $"context.image = \"{method.Source.ContentHash.ToLowerInvariant()}\"; context.method = {method.Source.Token.ToString(CultureInfo.InvariantCulture)}u; context.il = {method.Blocks[0].ILOffset.ToString(CultureInfo.InvariantCulture)}u;";
        return $$"""
            [[gnu::noinline]] static void return_{{number(exportId)}}(Context& context, LocationArgument* arguments, cilica_v3_value& result, cilica_v3_value* fields) noexcept {
                struct Returned { std::array<uint32_t, {{narrow.ToString(CultureInfo.InvariantCulture)}}> narrow{}; std::array<uint64_t, {{wide.ToString(CultureInfo.InvariantCulture)}}> wide{}; };
                struct SlotShape { uint32_t kind; std::size_t index; };
                static constexpr SlotShape native_slots[] = {
                    {{string.Join(", ", slots.Select((slot, i) => "{" + number((uint)EmbeddingRecordPlan.Kind(slot.Storage)) + "u, " + indices[i].ToString(CultureInfo.InvariantCulture) + "u}"))}}
                };
                static constexpr SlotShape host_slots[] = {
                    {{string.Join(", ", record.Fields.Select(field => "{" + number((uint)field.Binding.Kind) + "u, " + indices[field.NativeSlot].ToString(CultureInfo.InvariantCulture) + "u}"))}}
                };
                Storage<Returned> returned(context);
                if (returned.value == nullptr) { context.pending = 9u; {{origin}} }
                else {
                    Frame frame; RootScope roots(context, frame, nullptr, 0);
                    BorrowScope borrows(context, frame, returned.value->narrow.data(), returned.value->narrow.size(), nullptr,
                        returned.value->wide.data(), returned.value->wide.size());
                    if (borrows.status != LocationStatus::complete) {
                        context.pending = borrows.status == LocationStatus::limit ? 8u : 6u; {{origin}}
                    } else {
                        // Reuse one temporary carrier per iteration, including at O0/ASan.
                        for (std::size_t i = 0; i < {{slots.Length.ToString(CultureInfo.InvariantCulture)}}u; ++i)
                            arguments[{{inputCount.ToString(CultureInfo.InvariantCulture)}}u + i].location = {nullptr, borrows.frame.token, native_slots[i].index, nullptr,
                                native_slots[i].kind == 1u ? LocationOrigin::frame_integer : LocationOrigin::frame_wide_integer, false};
                        {{CppEmitter.Symbol(IrMethodId.From(method.Source))}}(context, arguments);
                        if (context.pending == 0) {
                            for (std::size_t i = 0; i < {{slots.Length.ToString(CultureInfo.InvariantCulture)}}u; ++i) {
                                const auto& slot = host_slots[i];
                                fields[i] = {slot.kind, 0u, slot.kind == 1u ? returned.value->narrow[slot.index] : returned.value->wide[slot.index]};
                            }
                            result = {4u, 0u, {{number(record.Binding.Id)}}u};
                        }
                    }
                }
            }
            """;
    }
}
