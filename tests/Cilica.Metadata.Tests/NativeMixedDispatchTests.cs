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

public sealed class NativeMixedDispatchTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_calls_match_dotnet_with_distinct_slots_roots_and_fault_cleanup(bool optimized)
    {
        string[] names = [nameof(MixedDispatchProbe.ClassOnly), nameof(MixedDispatchProbe.ClassFirst),
            nameof(MixedDispatchProbe.InterfaceFirst), nameof(MixedDispatchProbe.Allocating)];
        Func<int, int, int>[] oracles = [MixedDispatchProbe.ClassOnly, MixedDispatchProbe.ClassFirst,
            MixedDispatchProbe.InterfaceFirst, MixedDispatchProbe.Allocating];
        var programs = names.Select(MixedDispatchTests.Translate).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Root(int i) => "cilica_m_" + programs[i].Flow.Entry.ContentHash + "_" + programs[i].Flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        var cross = ObjectCppEmitter.EmitProgram(MixedDispatchTests.CrossImage()); source.Append(cross.Text);
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <cstring>
            #include <iostream>
            namespace {{Scope(3)}} {
            static Object* first_result = nullptr;
            static unsigned observations = 0;
            static void observe(Context& c) noexcept {
                assert(valid_roots(c) && c.top != nullptr);
                Frame* entry = c.top; while (entry->previous != nullptr) entry = entry->previous;
                if (c.instance.count == 3) first_result = c.instance.head;
                if (c.instance.count >= 3) {
                    bool found = false; for (std::size_t i = 0; i < entry->count; ++i) found |= entry->slots[i] == first_result;
                    assert(first_result != nullptr && found); ++observations;
                }
            }
            }
            [[gnu::noinline]] static void roots_and_limits() {
                using namespace {{Scope(3)}};
                Instance heap; Context c(heap); c.observe = observe;
                assert({{Root(3)}}(c, 1, 19) == {{MixedDispatchProbe.Allocating(1, 19)}} && c.pending == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0 && heap.count == 7 && observations > 0);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Root(3)}}(exact, 1, 19) == {{MixedDispatchProbe.Allocating(1, 19)}} && exact.pending == 0);
                Instance short_heap; Context short_frame(short_heap, c.peak - 1);
                (void){{Root(3)}}(short_frame, 1, 19);
                assert(short_frame.pending == 9 && short_frame.used == 0 && short_frame.top == nullptr && short_frame.depth == 0);
                for (std::size_t limit = 0; limit < 7; ++limit) {
                    Instance limited; limited.object_limit = limit; Context fault(limited);
                    (void){{Root(3)}}(fault, 1, 19);
                    assert(fault.pending == 5 && limited.count == limit && fault.used == 0 && fault.top == nullptr && fault.depth == 0);
                    assert(fault.method != 0 && fault.image != nullptr);
                    limited.object_limit = 4096; fault.pending = 0; fault.method = 0; fault.il = 0; fault.image = nullptr;
                    assert({{Root(3)}}(fault, 1, 7) == {{MixedDispatchProbe.Allocating(1, 7)}} && fault.pending == 0);
                    assert(fault.used == 0 && fault.top == nullptr && fault.depth == 0);
                }
            }
            struct Outcome { std::int32_t value = -123456789; std::uint32_t status = 0, method = 0, il = 0; };
            [[gnu::noinline]] static void cross_image() {
                std::int32_t value = 0; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                assert({{cross.EntryPoint}}(&value, &method, &il, &image, nullptr, 0) == 0 && value == 50);
            }
            """);
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $$"""
            [[gnu::noinline]] static Outcome outcome_{{i}}(int kind, int input) {
                Outcome o; using namespace {{Scope(i)}}; Instance heap; Context c(heap);
                const auto returned = {{Root(i)}}(c, kind, input);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                o.status = c.pending; o.method = c.method; o.il = c.il; if (o.status == 0) o.value = returned;
                const std::int32_t parameters[] = {kind, input}; std::int32_t value = -123456789;
                std::uint32_t method = 0, il = 0; const char* image = nullptr;
                assert({{artifacts[i].EntryPoint}}(&value, &method, &il, &image, parameters, 2) == o.status);
                assert(value == o.value && method == o.method && il == o.il && ((image == nullptr && c.image == nullptr)
                    || (image != nullptr && c.image != nullptr && std::strcmp(image, c.image) == 0)));
                return o;
            }
            """);
        source.Append("int main() { roots_and_limits(); cross_image(); int which = 0, kind = 0, input = 0; while (std::cin >> which >> kind >> input) { Outcome o; switch (which) {\n");
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $"case {i}: o = outcome_{i}(kind, input); break;\n");
        source.Append("default: return 2; } std::cout << o.status << ' ' << o.value << ' ' << o.method << ' ' << o.il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("mixed-native"), cpp = Path.Combine(directory, "mixed.cpp"), executable = Path.Combine(directory, "mixed");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler,
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var measure = await NativeProcess.Run(compiler,
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", optimized ? "-O2" : "-O0", "-fstack-usage", "-Werror",
             "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => { Assert.Equal("static", row[^1]); Assert.InRange(int.Parse(row[^2], CultureInfo.InvariantCulture), 0, 2048); });
        output.WriteLine($"O{(optimized ? 2 : 0)}: maximum native stack frame {rows.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} bytes.");
        var inputText = new StringBuilder(); var expected = new List<string>();
        int[] edges = [int.MinValue, -65536, -1, 0, 1, 19, 65536, int.MaxValue];
        for (int which = 0; which < programs.Length; which++)
            foreach (int kind in which == 3 ? Enumerable.Range(0, 2) : Enumerable.Range(-1, 7))
                foreach (int input in edges)
                {
                    int status = 0, value = -123456789, token = 0, offset = 0;
                    try { value = oracles[which](kind, input); }
                    catch (DivideByZeroException) { status = 1; }
                    catch (NullReferenceException) { status = 4; }
                    if (status != 0)
                    {
                        token = programs[which].Flow.Entry.Token;
                        offset = programs[which].Flow.Methods.Single(m => m.Source.Token == token).Blocks.SelectMany(b => b.Values)
                            .First(v => status == 1 ? v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }
                                : v.Expression is IrExpression.Call { VirtualDispatch: true }).ILOffset;
                    }
                    inputText.Append(CultureInfo.InvariantCulture, $"{which} {kind} {input}\n");
                    expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
                }
        var run = await NativeProcess.Run(executable, [], directory, inputText.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} mixed-dispatch .NET/native outcomes with origins, roots, budgets and cleanup at O{(optimized ? 2 : 0)}.");
    }
}
