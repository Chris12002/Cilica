using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeTracingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Precise_visitors_cover_roots_fields_arrays_literals_and_reject_invalid_references(bool optimized)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(TracingProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var translated = ObjectTranslator.Translate(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TracingProbe).GetMethod(nameof(TracingProbe.Run))!.MetadataToken), library);
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!; var artifact = ObjectCppEmitter.EmitProgram(program);
        string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture)
            + (id.ArrayElement is null ? "" : id.ArrayElement.Kind == ObjectArrayElementKind.Int32 ? "_vector_i4" : "_vector_ref_" + Type(id.ArrayElement.Type!));
        string node = Type(ObjectTypeId.From(program.Flow.Types.Types.Single(t => t.Source.Token == typeof(TraceNode).MetadataToken && t.Source.ContentHash == input.ContentHash).Source));
        string references = Type(program.Flow.Types.Arrays.Single(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Reference));
        string integers = Type(program.Flow.Types.Arrays.Single(a => a.ArrayElement!.Kind == ObjectArrayElementKind.Int32));
        string text = Type(program.Flow.Types.String!);
        string root = "cilica_m_" + program.Flow.Entry.ContentHash + "_" + program.Flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
        var source = new StringBuilder(artifact.Text).Append(NativeObjectAdapters.Emit(program, artifact));
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <algorithm>
            #include <cassert>
            #include <memory>
            #include <vector>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            struct Records {
                std::vector<ReferenceEdge> edges;
                std::size_t stop = static_cast<std::size_t>(-1);
            };
            static bool record(void* state, const ReferenceEdge& edge) noexcept {
                auto& records = *static_cast<Records*>(state); records.edges.push_back(edge);
                return records.edges.size() < records.stop;
            }
            static Object*& field(Object* value, std::size_t index) noexcept {
                return *static_cast<Object**>(field_address(value, index));
            }
            struct Graph {
                Instance heap;
                Context c{heap};
                Object* a = allocate<{{node}}>(c, {{node}}_descriptor);
                Object* b = allocate<{{node}}>(c, {{node}}_descriptor);
                Object* garbage = allocate<{{node}}>(c, {{node}}_descriptor);
                Object* array = allocate_array(c, {{references}}_descriptor, 3);
                Object* empty_array = allocate_array(c, {{references}}_descriptor, 0);
                Object* numbers = allocate_array(c, {{integers}}_descriptor, 2);
                Object* empty = string_literal(c, {{text}}_descriptor, 0, nullptr, 0);
                Object* literal = nullptr;
                Graph() noexcept {
                    const std::uint16_t units[] = {0u, 0xd800u};
                    literal = string_literal(c, {{text}}_descriptor, 17, units, 2);
                    assert(a && b && garbage && array && empty_array && numbers && empty && literal && c.pending == 0);
                    // Next is inherited; Number is an integer, then Alias/Edges/Name.
                    field(a, 0) = b; field(a, 2) = b; field(a, 3) = array; field(a, 4) = nullptr;
                    field(b, 0) = a;
                    assert(store_reference_element(c, array, 0, a));
                    assert(store_reference_element(c, array, 1, b));
                    store_integer_element(numbers, 0, 0xffffffffu);
                    *static_cast<std::uint32_t*>(field_address(a, 1)) = 0xffffffffu;
                }
            };
            struct Walk { std::vector<Object*> values; };
            static bool enqueue(void* state, const ReferenceEdge& edge) noexcept {
                auto& values = static_cast<Walk*>(state)->values;
                if (edge.value != nullptr && std::find(values.begin(), values.end(), edge.value) == values.end()) values.push_back(edge.value);
                return true;
            }
            // Test-owned iterative graph walk consumes the runtime's direct-edge API.
            // This proves cycles/aliases compose without adding a collector to the runtime.
            static Walk reachable(Context& c) {
                Walk walk;
                assert(visit_roots(c, enqueue, &walk) == TraceResult::complete);
                for (std::size_t i = 0; i < walk.values.size(); ++i)
                    assert(visit_object_references(c.instance, walk.values[i], enqueue, &walk) == TraceResult::complete);
                return walk;
            }
            [[gnu::noinline]] static void exact_fields(Graph& g) {
                Records records;
                assert(visit_object_references(g.heap, g.a, record, &records) == TraceResult::complete);
                assert(records.edges.size() == 4);
                const std::size_t indices[] = {0, 2, 3, 4}; Object* expected[] = {g.b, g.b, g.array, nullptr};
                for (std::size_t i = 0; i < 4; ++i) {
                    const auto& edge = records.edges[i];
                    assert(edge.kind == ReferenceKind::field && edge.owner == g.a && edge.frame == nullptr);
                    assert(edge.index == g.a->type->fields[indices[i]] && edge.value == expected[i]);
                }
            }
            [[gnu::noinline]] static void exact_arrays_and_leaves(Graph& g) {
                Records records;
                assert(visit_object_references(g.heap, g.array, record, &records) == TraceResult::complete);
                assert(records.edges.size() == 3);
                for (std::size_t i = 0; i < 3; ++i) {
                    const auto& edge = records.edges[i];
                    assert(edge.kind == ReferenceKind::element && edge.owner == g.array && edge.frame == nullptr && edge.index == i);
                    assert(edge.value == (i == 0 ? g.a : i == 1 ? g.b : nullptr));
                }
                records.edges.clear();
                for (Object* leaf : {g.empty_array, g.numbers, g.empty, g.literal, static_cast<Object*>(nullptr)})
                    assert(visit_object_references(g.heap, leaf, record, &records) == TraceResult::complete);
                assert(records.edges.empty());
            }
            [[gnu::noinline]] static void exact_edges() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph;
                const auto bytes = g.heap.bytes, count = g.heap.count;
                const char* image = "preserved";
                g.c.pending = 2; g.c.method = 31; g.c.il = 47; g.c.image = image;
                exact_fields(g); exact_arrays_and_leaves(g);
                assert(g.heap.bytes == bytes && g.heap.count == count && g.c.pending == 2 && g.c.method == 31 && g.c.il == 47);
                assert(g.c.image == image && g.c.used == 0 && g.c.depth == 0);
            }
            [[gnu::noinline]] static void exact_roots_and_cycles() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph; Records records;
                Object* outer_slots[] = {g.a, nullptr, g.a}; Object* inner_slots[] = {g.array, g.numbers};
                Frame outer, inner;
                {
                    RootScope first(g.c, outer, outer_slots, 3); RootScope second(g.c, inner, inner_slots, 2);
                    assert(visit_roots(g.c, record, &records) == TraceResult::complete && records.edges.size() == 7);
                    for (std::size_t i = 0; i < 5; ++i) {
                        const auto& edge = records.edges[i]; const auto index = i < 2 ? i : i - 2;
                        assert(edge.kind == ReferenceKind::frame && edge.owner == nullptr && edge.index == index);
                        assert(edge.frame == (i < 2 ? &inner : &outer) && edge.value == (i < 2 ? inner_slots[index] : outer_slots[index]));
                    }
                    assert(records.edges[5].kind == ReferenceKind::literal && records.edges[5].value == g.literal && records.edges[5].index == 17);
                    assert(records.edges[6].kind == ReferenceKind::literal && records.edges[6].value == g.empty && records.edges[6].index == 0);
                    auto walk = reachable(g.c);
                    assert(walk.values.size() == 6 && std::find(walk.values.begin(), walk.values.end(), g.garbage) == walk.values.end());
                    // Removing frame roots leaves only literal-cache roots. The heap list,
                    // ownership/header links, integer bits and array buffers are not roots.
                }
                auto walk = reachable(g.c); assert(walk.values.size() == 2 && g.heap.count == 8);
                assert(std::find(walk.values.begin(), walk.values.end(), g.a) == walk.values.end());
                assert(g.c.top == nullptr && g.c.depth == 0 && g.c.used == 0);
            }
            [[gnu::noinline]] static void invalid_edges_and_stop() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph; Records records;
                auto foreign = std::make_unique<Graph>();
                Object* bogus = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(visit_object_references(g.heap, bogus, record, &records) == TraceResult::invalid);
                assert(visit_object_references(g.heap, foreign->a, record, &records) == TraceResult::invalid);
                const Descriptor* saved = g.a->type; const Descriptor copied = *saved;
                g.a->type = &copied; assert(visit_object_references(g.heap, g.a, record, &records) == TraceResult::invalid);
                g.a->type = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                assert(visit_object_references(g.heap, g.a, record, &records) == TraceResult::invalid); g.a->type = saved;
                g.a->owner = &foreign->heap; assert(visit_object_references(g.heap, g.a, record, &records) == TraceResult::invalid); g.a->owner = &g.heap;
                field(g.a, 0) = foreign->b;
                assert(visit_object_references(g.heap, g.a, record, &records) == TraceResult::invalid); field(g.a, 0) = g.b;
                assert(records.edges.empty());
                reinterpret_cast<ArrayObject*>(g.array)->references[1] = bogus;
                assert(visit_object_references(g.heap, g.array, record, &records) == TraceResult::invalid && records.edges.size() == 1);
                reinterpret_cast<ArrayObject*>(g.array)->references[1] = g.b;
                records.edges.clear(); records.stop = 1;
                assert(visit_object_references(g.heap, g.a, record, &records) == TraceResult::stopped && records.edges.size() == 1);
                records.edges.clear(); assert(visit_roots(g.c, record, &records) == TraceResult::stopped && records.edges.size() == 1);
                assert(visit_roots(g.c, nullptr, nullptr) == TraceResult::invalid);
                assert(visit_object_references(g.heap, g.a, nullptr, nullptr) == TraceResult::invalid);
            }
            [[gnu::noinline]] static void invalid_roots() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph; Records records;
                Object* slots[] = {reinterpret_cast<Object*>(std::uintptr_t{1})}; Frame frame;
                {
                    RootScope roots(g.c, frame, slots, 1);
                    assert(visit_roots(g.c, record, &records) == TraceResult::invalid && records.edges.empty());
                    slots[0] = g.a; frame.count = 1025;
                    assert(visit_roots(g.c, record, &records) == TraceResult::invalid && records.edges.empty());
                    frame.count = 1; frame.slots = nullptr;
                    assert(visit_roots(g.c, record, &records) == TraceResult::invalid && records.edges.empty());
                    frame.slots = slots; frame.previous = &frame;
                    assert(visit_roots(g.c, record, &records) == TraceResult::invalid); frame.previous = nullptr;
                }
                g.c.depth = 1; assert(visit_roots(g.c, record, &records) == TraceResult::invalid); g.c.depth = 0;
                StringObject* saved = g.heap.literals;
                g.heap.literals = reinterpret_cast<StringObject*>(std::uintptr_t{1});
                assert(visit_roots(g.c, record, &records) == TraceResult::invalid);
                g.heap.literals = reinterpret_cast<StringObject*>(g.a);
                assert(visit_roots(g.c, record, &records) == TraceResult::invalid); g.heap.literals = saved;
                StringObject* next = saved->literal_next; saved->literal_next = saved;
                assert(visit_roots(g.c, record, &records) == TraceResult::invalid); saved->literal_next = next;
                assert(g.c.pending == 0 && g.c.top == nullptr && g.c.depth == 0);
            }
            [[gnu::noinline]] static void frame_limits() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph; Records records;
                std::vector<Frame> frames(65);
                for (std::size_t i = 1; i < frames.size(); ++i) frames[i].previous = &frames[i - 1];
                g.c.top = &frames[63]; g.c.depth = 64;
                assert(visit_roots(g.c, record, &records) == TraceResult::complete && records.edges.size() == 2);
                g.c.top = &frames[64]; g.c.depth = 65;
                assert(visit_roots(g.c, record, &records) == TraceResult::invalid);
                std::vector<Object*> slots(1024, g.a); records.edges.clear();
                frames[0].slots = slots.data(); frames[0].count = slots.size();
                g.c.top = &frames[0]; g.c.depth = 1;
                assert(visit_roots(g.c, record, &records) == TraceResult::complete && records.edges.size() == 1026);
                g.c.top = nullptr; g.c.depth = 0;
            }
            [[gnu::noinline]] static void literal_limits() {
                Instance heap; Context c(heap); Records records;
                for (std::size_t i = 0; i < 257; ++i) {
                    const auto unit = static_cast<std::uint16_t>(i);
                    assert(string_literal(c, {{text}}_descriptor, i, &unit, i == 0 ? 0 : 1) != nullptr);
                }
                const auto bytes = heap.bytes;
                assert(visit_roots(c, record, &records) == TraceResult::complete && records.edges.size() == 257);
                assert(heap.bytes == bytes && heap.count == 257);
                const std::uint16_t unit = 257;
                assert(string_literal(c, {{text}}_descriptor, 257, &unit, 1) != nullptr);
                assert(visit_roots(c, record, &records) == TraceResult::invalid);
            }
            static void clean_marks(const Instance& heap) {
                assert(!heap.marking);
                for (Object* value = heap.head; value != nullptr; value = value->next)
                    assert(!value->marked && value->mark_next == nullptr);
            }
            struct Classification {
                Context& context;
                const Walk& expected;
                std::vector<Object*> delivered;
                std::size_t stop = static_cast<std::size_t>(-1);
            };
            static bool classify(void* opaque, Object* value, bool live) noexcept {
                auto& report = *static_cast<Classification*>(opaque);
                assert(report.context.instance.marking && value->marked == live);
                assert(live == (std::find(report.expected.values.begin(), report.expected.values.end(), value) != report.expected.values.end()));
                assert(std::find(report.delivered.begin(), report.delivered.end(), value) == report.delivered.end());
                report.delivered.push_back(value);
                const auto nested_mark = mark_reachable(report.context);
                assert(nested_mark.status == MarkStatus::busy && nested_mark.reachable == 0 && nested_mark.unreachable == 0 && nested_mark.edges == 0);
                return report.delivered.size() < report.stop;
            }
            static void failed_mark(Context& c, MarkStatus status, MarkLimits limits = {}) {
                const auto report = mark_reachable(c, limits);
                assert(report.status == status && report.reachable == 0 && report.unreachable == 0 && report.edges == 0);
                clean_marks(c.instance);
            }
            [[gnu::noinline]] static void marking_reports(Graph& g) {
                auto expected = reachable(g.c); Classification classification{g.c, expected, {} };
                const auto report = mark_reachable(g.c, {g.heap.count, 17}, classify, &classification);
                assert(report.status == MarkStatus::complete && report.reachable == 6 && report.unreachable == 2 && report.edges == 17);
                assert(classification.delivered.size() == g.heap.count); clean_marks(g.heap);
                classification.delivered.clear(); classification.stop = 2;
                const auto stopped = mark_reachable(g.c, {}, classify, &classification);
                assert(stopped.status == MarkStatus::stopped && stopped.reachable == 0 && stopped.unreachable == 0 && stopped.edges == 0);
                assert(classification.delivered.size() == 2); clean_marks(g.heap);
            }
            [[gnu::noinline]] static void marking_limits(Graph& g) {
                for (std::size_t edges = 0; edges < 17; ++edges) failed_mark(g.c, MarkStatus::limit, {g.heap.count, edges});
                failed_mark(g.c, MarkStatus::limit, {g.heap.count - 1, 17});
                assert(mark_reachable(g.c, {g.heap.count, 17}).status == MarkStatus::complete); clean_marks(g.heap);
            }
            [[gnu::noinline]] static void marking_graph_and_budgets() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph;
                Object* slots[] = {g.a, g.numbers, nullptr, g.a}; Frame frame;
                RootScope roots(g.c, frame, slots, 4);
                const auto bytes = g.heap.bytes, count = g.heap.count;
                g.heap.limit = bytes; g.heap.object_limit = count; // Marking needs no allocation headroom.
                g.c.pending = 2; g.c.method = 19; g.c.il = 23; const char* image = "mark-origin"; g.c.image = image;
                for (Object* value = g.heap.head; value != nullptr; value = value->next) value->synchronization = 71;
                marking_reports(g); marking_limits(g);
                assert(g.heap.bytes == bytes && g.heap.count == count && g.c.pending == 2 && g.c.method == 19 && g.c.il == 23 && g.c.image == image);
                assert(g.c.top == &frame && g.c.depth == 1 && g.c.used == 0);
                for (Object* value = g.heap.head; value != nullptr; value = value->next) assert(value->synchronization == 71);
                // Changed roots must be traced afresh; no persistent marks authorize later work.
                slots[0] = nullptr; slots[3] = nullptr;
                const auto reduced = mark_reachable(g.c);
                assert(reduced.status == MarkStatus::complete && reduced.reachable == 3 && reduced.unreachable == 5); clean_marks(g.heap);
            }
            [[gnu::noinline]] static void invalid_marking_and_retry() {
                auto graph = std::make_unique<Graph>(); auto& g = *graph;
                Object* slots[] = {g.a}; Frame frame; RootScope roots(g.c, frame, slots, 1);
                auto expected = reachable(g.c); Classification classification{g.c, expected, {} };
                Object* bogus = reinterpret_cast<Object*>(std::uintptr_t{1});
                field(g.b, 0) = bogus;
                const auto invalid = mark_reachable(g.c, {}, classify, &classification);
                assert(invalid.status == MarkStatus::invalid && invalid.reachable == 0 && classification.delivered.empty()); clean_marks(g.heap);
                field(g.b, 0) = g.a; assert(mark_reachable(g.c).status == MarkStatus::complete); clean_marks(g.heap);
                slots[0] = bogus; failed_mark(g.c, MarkStatus::invalid); slots[0] = g.a;
                const Descriptor* saved = g.garbage->type; const Descriptor copied = *saved;
                g.garbage->type = &copied; failed_mark(g.c, MarkStatus::invalid); g.garbage->type = saved;
                ++g.heap.count; failed_mark(g.c, MarkStatus::invalid); --g.heap.count;
                --g.heap.count; failed_mark(g.c, MarkStatus::invalid); ++g.heap.count;
                // Readable allocation-list cycles terminate before any marking writes.
                Object* tail = g.heap.head; while (tail->next != nullptr) tail = tail->next;
                tail->next = g.heap.head;
                const auto cyclic = mark_reachable(g.c); assert(cyclic.status == MarkStatus::invalid);
                tail->next = nullptr; clean_marks(g.heap);
                assert(mark_reachable(g.c).status == MarkStatus::complete); clean_marks(g.heap);
            }
            [[gnu::noinline]] static void deep_marking_and_empty_heap() {
                Instance heap; Context c(heap);
                const auto empty_report = mark_reachable(c, {0, 0});
                assert(empty_report.status == MarkStatus::complete && empty_report.reachable == 0 && empty_report.unreachable == 0 && empty_report.edges == 0);
                Object* first = nullptr; Object* last = nullptr;
                for (std::size_t i = 0; i < 4096; ++i) {
                    Object* node = allocate<{{node}}>(c, {{node}}_descriptor); assert(node != nullptr);
                    if (first == nullptr) first = node;
                    if (last != nullptr) { field(last, 0) = node; field(last, 2) = node; }
                    last = node;
                }
                field(last, 0) = first;
                Object* slots[] = {first}; Frame frame; RootScope roots(c, frame, slots, 1);
                const auto report = mark_reachable(c, {4096, 16385});
                assert(report.status == MarkStatus::complete && report.reachable == 4096 && report.unreachable == 0 && report.edges == 16385);
                clean_marks(heap); failed_mark(c, MarkStatus::limit, {4095, 16385}); failed_mark(c, MarkStatus::limit, {4096, 16384});
                slots[0] = nullptr;
                const auto dead = mark_reachable(c, {4096, 1});
                assert(dead.status == MarkStatus::complete && dead.reachable == 0 && dead.unreachable == 4096 && dead.edges == 1); clean_marks(heap);
            }
            static unsigned observations = 0;
            static bool nested = false;
            static void observe(Context& c) noexcept {
                const auto bytes = c.instance.bytes, count = c.instance.count, used = c.used;
                auto walk = reachable(c); assert(walk.values.size() == count);
                const auto marked = mark_reachable(c);
                assert(marked.status == MarkStatus::complete && marked.reachable == count && marked.unreachable == 0); clean_marks(c.instance);
                assert(c.instance.bytes == bytes && c.instance.count == count && c.used == used && c.pending == 0);
                nested |= c.depth > 1; ++observations;
            }
            [[gnu::noinline]] static void real_generated_frames() {
                Instance heap; Context c(heap); c.observe = observe;
                assert({{root}}(c, 21) == {{TracingProbe.Run(21)}} && c.pending == 0 && nested && observations > 8);
                assert(c.top == nullptr && c.depth == 0 && c.used == 0);
                auto after = reachable(c); assert(after.values.size() == 3 && after.values.size() < heap.count);
                const auto marked = mark_reachable(c);
                assert(marked.status == MarkStatus::complete && marked.reachable == 3 && marked.unreachable == heap.count - 3); clean_marks(heap);
                for (std::size_t limit = 0; limit < heap.count; ++limit) {
                    Instance short_heap; short_heap.object_limit = limit; Context failed(short_heap); failed.observe = observe;
                    (void){{root}}(failed, 21); assert(failed.pending == 5 && failed.top == nullptr && failed.used == 0 && failed.depth == 0);
                    const auto pending = failed.pending, method = failed.method, il = failed.il; const char* image = failed.image;
                    const auto live = reachable(failed);
                    const auto marked_fault = mark_reachable(failed);
                    assert(marked_fault.status == MarkStatus::complete && marked_fault.reachable == live.values.size()); clean_marks(short_heap);
                    assert(failed.pending == pending && failed.method == method && failed.il == il && failed.image == image);
                }
            }
            int main() { exact_edges(); exact_roots_and_cycles(); invalid_edges_and_stop(); invalid_roots(); frame_limits(); literal_limits(); marking_graph_and_budgets(); invalid_marking_and_retry(); deep_marking_and_empty_heap(); real_generated_frames(); }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-tracing");
        string cpp = Path.Combine(directory, "tracing.cpp"), executable = Path.Combine(directory, "tracing");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
        var measured = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", optimized ? "-O2" : "-O0",
            "-fstack-usage", "-Werror", "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measured.ExitCode == 0, measured.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows); Assert.All(rows, row => { Assert.Equal("static", row[^1]); Assert.InRange(int.Parse(row[^2], CultureInfo.InvariantCulture), 0, 2048); });
        output.WriteLine($"Precise visitation and marking O{(optimized ? 2 : 0)}; maximum measured static frame {rows.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} bytes.");
    }
}
