using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeObjectDispatchTests(ITestOutputHelper output)
{
    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_virtual_calls_match_dotnet_and_preserve_roots_faults_and_budgets(bool optimized)
    {
        string[] names = [nameof(ClassDispatchProbe.Run), nameof(ClassDispatchProbe.ArgumentOrder), nameof(ClassDispatchProbe.Allocating), nameof(ClassDispatchProbe.Recursive)];
        Func<int, int, int>[] oracles = [ClassDispatchProbe.Run, ClassDispatchProbe.ArgumentOrder, ClassDispatchProbe.Allocating, ClassDispatchProbe.Recursive];
        var programs = names.Select(ObjectDispatchTests.Translate).ToArray(); var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Layout(int i, System.Type type) => Type(ObjectTypeId.From(programs[i].Flow.Types.Types.Single(t => t.Source.Token == type.MetadataToken).Source));
        string Symbol(int i, System.Type owner, string name) => Method(IrMethodId.From(programs[i].Flow.Methods.Single(m => m.Source.Token == owner.GetMethod(name)!.MetadataToken).Source));
        var source = new StringBuilder(); for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        var cross = ObjectDispatchTests.CrossImage(); var crossArtifact = ObjectCppEmitter.EmitProgram(cross); source.Append(crossArtifact.Text);
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <cstring>
            #include <iostream>
            #include <pthread.h>
            namespace {{Scope(2)}} {
            static Object* first_result = nullptr;
            static unsigned observations = 0;
            static std::size_t required_objects = 0;
            static void observe(Context& c) noexcept {
                const auto live = mark_reachable(c); assert(live.status == MarkStatus::complete);
                if (required_objects < live.reachable + 1) required_objects = live.reachable + 1;
                assert(valid_roots(c) && c.top != nullptr);
                Frame* entry = c.top; while (entry->previous != nullptr) entry = entry->previous;
                if (c.instance.count == 4) first_result = c.instance.head;
                if (c.instance.count >= 4) {
                    bool found = false; for (std::size_t i = 0; i < entry->count; ++i) found |= entry->slots[i] == first_result;
                    assert(found && first_result != nullptr); ++observations;
                }
            }
            }
            [[gnu::noinline]] static void roots_and_limits() {
                using namespace {{Scope(2)}};
                Instance heap; Context c(heap); c.observe = observe;
                assert({{Method(programs[2].Flow.Entry)}}(c, 1, 19) == {{ClassDispatchProbe.Allocating(1, 19)}} && c.pending == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0 && heap.count == 8 && observations > 0);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Method(programs[2].Flow.Entry)}}(exact, 1, 19) == {{ClassDispatchProbe.Allocating(1, 19)}} && exact.pending == 0);
                Instance short_heap; Context short_frame(short_heap, c.peak - 1); (void){{Method(programs[2].Flow.Entry)}}(short_frame, 1, 19);
                assert(short_frame.pending == 9 && short_frame.used == 0 && short_frame.top == nullptr && short_frame.depth == 0);
                for (std::size_t limit = 0; limit < 8; ++limit) {
                    Instance limited; limited.object_limit = limit; Context fault(limited);
                    const auto value = {{Method(programs[2].Flow.Entry)}}(fault, 1, 19);
                    // Pressure is determined by live roots, not cumulative allocations.
                    assert(fault.pending == (limit < required_objects ? 5u : 0u));
                    if (fault.pending == 0) assert(value == {{ClassDispatchProbe.Allocating(1, 19)}});
                    assert(limited.count <= limit && fault.top == nullptr && fault.used == 0 && fault.depth == 0);
                }
            }
            [[gnu::noinline]] static void ownership_and_direct_calls() {
                using namespace {{Scope(0)}}; Instance heap; Context c(heap);
                Object* leaf = allocate<{{Layout(0, typeof(ExactDispatchLeaf))}}>(c, {{Layout(0, typeof(ExactDispatchLeaf))}}_descriptor);
                assert({{Symbol(0, typeof(ClassDispatchProbe), nameof(ClassDispatchProbe.ThroughMiddle))}}(c, leaf, 7) == 307 && c.pending == 0);
                assert({{Symbol(0, typeof(ExactDispatchLeaf), nameof(ExactDispatchLeaf.BaseRead))}}(c, static_cast<Object*>(nullptr), 7) == 207 && c.pending == 0);
                Instance foreign; Context wrong(foreign); (void){{Symbol(0, typeof(ClassDispatchProbe), nameof(ClassDispatchProbe.ThroughMiddle))}}(wrong, leaf, 7);
                assert(wrong.pending == 6 && wrong.used == 0 && wrong.top == nullptr);
                Context invalid(heap); (void){{Symbol(0, typeof(ClassDispatchProbe), nameof(ClassDispatchProbe.ThroughMiddle))}}(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}), 7);
                assert(invalid.pending == 6 && invalid.used == 0 && invalid.top == nullptr);
                Context null_receiver(heap); (void){{Symbol(0, typeof(ClassDispatchProbe), nameof(ClassDispatchProbe.ThroughMiddle))}}(null_receiver, static_cast<Object*>(nullptr), 7);
                assert(null_receiver.pending == 4 && null_receiver.used == 0 && null_receiver.depth == 0);
                static const Descriptor forged = {{Layout(0, typeof(ExactDispatchLeaf))}}_descriptor;
                Object* missing_type = allocate<{{Layout(0, typeof(ExactDispatchLeaf))}}>(c, forged);
                Context missing(heap); (void){{Symbol(0, typeof(ClassDispatchProbe), nameof(ClassDispatchProbe.ThroughMiddle))}}(missing, missing_type, 7);
                assert(missing.pending == 6 && missing.used == 0 && missing.top == nullptr);
            }
            static void* recursion(void*) noexcept {
                #if defined(__APPLE__)
                const auto stack_size = pthread_get_stacksize_np(pthread_self());
                assert(stack_size >= 256u * 1024u && stack_size <= 320u * 1024u);
                #endif
                using namespace {{Scope(3)}};
                Instance heap; Context c(heap);
                assert({{Method(programs[3].Flow.Entry)}}(c, 1, 62) == 131 && c.pending == 0 && c.used == 0);
                (void){{Method(programs[3].Flow.Entry)}}(c, 1, 1000);
                assert(c.pending == 8 && c.method == {{typeof(VirtualNodeLeaf).GetMethod(nameof(VirtualNodeLeaf.Recurse))!.MetadataToken}}u && c.il == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                c.pending = 0; c.method = 0; c.il = 0; c.image = nullptr;
                assert({{Method(programs[3].Flow.Entry)}}(c, 0, 7) == 14 && c.pending == 0 && c.used == 0);
                return nullptr;
            }
            [[gnu::noinline]] static void cross_image() {
                using namespace {{crossArtifact.EntryPoint[..^4] + "_closure"}}; Instance heap; Context c(heap);
                assert({{Method(cross.Flow.Entry)}}(c, nullptr) == 53 && c.pending == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0);
            }
            int main() {
                roots_and_limits(); ownership_and_direct_calls();
                cross_image();
                pthread_attr_t attributes; assert(pthread_attr_init(&attributes) == 0);
                assert(pthread_attr_setstacksize(&attributes, 256u * 1024u) == 0);
                pthread_t worker; assert(pthread_create(&worker, &attributes, recursion, nullptr) == 0);
                assert(pthread_join(worker, nullptr) == 0 && pthread_attr_destroy(&attributes) == 0);
                int which = 0, kind = 0, input = 0;
                while (std::cin >> which >> kind >> input) {
                    std::int32_t value = -123456789; std::uint32_t status = 0, method = 0, il = 0;
                    switch (which) {
            """);
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $$"""
            case {{i}}: {
                using namespace {{Scope(i)}}; Instance heap; Context c(heap);
                const auto returned = {{Method(programs[i].Flow.Entry)}}(c, kind, input);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                status = c.pending; method = c.method; il = c.il; if (status == 0) value = returned;
                const std::int32_t parameters[] = {kind, input}; std::int32_t boundary = -123456789;
                std::uint32_t bm = 0, bi = 0; const char* bh = nullptr;
                assert({{artifacts[i].EntryPoint}}(&boundary, &bm, &bi, &bh, parameters, 2) == status);
                assert(boundary == value && bm == method && bi == il && ((bh == nullptr && c.image == nullptr)
                    || (bh != nullptr && c.image != nullptr && std::strcmp(bh, c.image) == 0)));
                break; }
            """);
        source.Append("default: return 2; } std::cout << status << ' ' << value << ' ' << method << ' ' << il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("virtual-native"), cpp = Path.Combine(directory, "virtual.cpp"), executable = Path.Combine(directory, "virtual");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler,
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-pthread",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        // Inspect uninstrumented static stack reports; ASan can label fixed
        // source frames dynamic because of its fake-stack/realignment paths.
        var measure = await NativeProcess.Run(compiler,
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-pthread", optimized ? "-O2" : "-O0", "-fstack-usage",
                "-Werror", "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => { Assert.Equal("static", row[^1]); Assert.InRange(int.Parse(row[^2], CultureInfo.InvariantCulture), 0, 2048); });
        output.WriteLine($"O{(optimized ? 2 : 0)}: {rows.Length} native functions measured; maximum {rows.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} stack bytes.");
        var inputs = new StringBuilder(); var expected = new List<string>();
        int[] edges = [int.MinValue, -65536, -1, 0, 1, 19, 65536, int.MaxValue];
        for (int which = 0; which < programs.Length; which++)
            foreach (int kind in which < 2 ? Enumerable.Range(-1, 9) : Enumerable.Range(0, 2))
                foreach (int input in which == 3 ? new[] { -1, 0, 1, 7, 31, 62 } : edges)
                {
                    int value = -123456789, status = 0, token = 0, offset = 0;
                    try { value = oracles[which](kind, input); }
                    catch (DivideByZeroException) { status = 1; }
                    catch (NullReferenceException) { status = 4; }
                    if (status != 0)
                    {
                        token = programs[which].Flow.Entry.Token;
                        var root = programs[which].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[which].Flow.Entry);
                        offset = root.Blocks.SelectMany(b => b.Values).First(v => status == 1
                            ? v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }
                            : v.Expression is IrExpression.Call { VirtualDispatch: true }).ILOffset;
                    }
                    inputs.Append(CultureInfo.InvariantCulture, $"{which} {kind} {input}\n");
                    expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
                }
        var run = await NativeProcess.Run(executable, [], directory, inputs.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} .NET/native virtual outcomes, exact origins, rooted returns, resource cleanup and recursion within a 320 KiB stack ceiling at O{(optimized ? 2 : 0)}.");
    }
}
