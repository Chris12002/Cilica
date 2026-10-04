using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeReferenceJoinTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inferred_class_interface_and_loop_inputs_match_dotnet_with_roots_and_faults(bool optimized)
    {
        string[] names = [nameof(ReferenceJoinProbe.Class), nameof(ReferenceJoinProbe.Interface), nameof(ReferenceJoinProbe.Parents),
            nameof(ReferenceJoinProbe.Nullable), nameof(ReferenceJoinProbe.Rooted), nameof(ReferenceJoinProbe.Field)];
        var programs = names.Select(ReferenceJoinTests.Authored).Append(ReferenceJoinFixtures.LiveLoop()).ToArray();
        var oracles = names.Select(n => typeof(ReferenceJoinProbe).GetMethod(n)!.CreateDelegate<Func<int, int, int>>()).Append(LoopOracle()).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Root(int i) => "cilica_m_" + programs[i].Flow.Entry.ContentHash + "_" + programs[i].Flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
        int InputSlot(int i)
        {
            var entry = programs[i].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[i].Flow.Entry);
            var block = entry.Blocks.Single(b => b.Inputs.Any(t => t.Kind == IrTypeKind.Reference)
                && b.Values.Any(v => v.Expression is IrExpression.NewObject allocation
                    && allocation.Constructor.Token == typeof(ProbeNode).GetConstructor([typeof(int)])!.MetadataToken));
            return ObjectRootAssignment.Assign(programs[i]).Single(p => p.Method == programs[i].Flow.Entry).Slots
                .Single(s => s.Kind == ObjectRootKind.BlockInput && s.Block == block.Id && s.Index == 0).Slot;
        }
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        source.Append("#include <cassert>\n#include <cstring>\n#include <iostream>\n");
        foreach (int i in new[] { 4, 6 }) source.Append(CultureInfo.InvariantCulture, $$"""
            namespace {{Scope(i)}} {
            static unsigned observations = 0;
            static std::size_t required_objects = 0;
            static void observe_join(Context& c) noexcept {
                const auto live = mark_reachable(c); assert(live.status == MarkStatus::complete);
                if (required_objects < live.reachable + 1) required_objects = live.reachable + 1;
                assert(valid_roots(c) && c.top != nullptr);
                if (c.instance.count % 2 == 1) {
                    Frame* entry = c.top; while (entry->previous != nullptr) entry = entry->previous;
                    assert(entry->slots[{{InputSlot(i)}}] == c.instance.head); ++observations;
                }
            }
            }
            [[gnu::noinline]] static void roots_{{i}}() {
                using namespace {{Scope(i)}}; Instance heap; Context c(heap); c.observe = observe_join;
                assert({{Root(i)}}(c, 2, 19) == {{oracles[i](2, 19)}} && c.pending == 0);
                assert(c.top == nullptr && c.used == 0 && c.depth == 0 && observations == {{(i == 4 ? 1 : 3)}});
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Root(i)}}(exact, 2, 19) == {{oracles[i](2, 19)}} && exact.pending == 0);
                Instance short_heap; Context short_frame(short_heap, c.peak - 1);
                (void){{Root(i)}}(short_frame, 2, 19);
                assert(short_frame.pending == 9 && short_frame.top == nullptr && short_frame.used == 0 && short_frame.depth == 0);
                for (std::size_t limit = 0; limit < heap.count; ++limit) {
                    Instance limited; limited.object_limit = limit; Context fault(limited);
                    const auto value = {{Root(i)}}(fault, 2, 19);
                    // Pressure is determined by live roots, not cumulative allocations.
                    assert(fault.pending == (limit < required_objects ? 5u : 0u));
                    if (fault.pending == 0) assert(value == {{oracles[i](2, 19)}});
                    assert(limited.count <= limit && fault.top == nullptr && fault.used == 0 && fault.depth == 0);
                    limited.object_limit = 4096; fault.pending = 0; fault.method = 0; fault.il = 0; fault.image = nullptr;
                    assert({{Root(i)}}(fault, 2, 7) == {{oracles[i](2, 7)}} && fault.pending == 0);
                    assert(fault.top == nullptr && fault.used == 0 && fault.depth == 0);
                }
            }
            """);
        source.Append("struct Outcome { std::uint32_t status = 0, method = 0, il = 0; std::int32_t value = -123456789; };\n");
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $$"""
            [[gnu::noinline]] static Outcome outcome_{{i}}(int kind, int input) {
                using namespace {{Scope(i)}}; Instance heap; Context c(heap); Outcome o;
                const auto returned = {{Root(i)}}(c, kind, input);
                assert(c.top == nullptr && c.used == 0 && c.depth == 0);
                o.status = c.pending; o.method = c.method; o.il = c.il; if (o.status == 0) o.value = returned;
                std::int32_t value = -123456789; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                const std::int32_t parameters[] = {kind, input};
                assert({{artifacts[i].EntryPoint}}(&value, &method, &il, &image, parameters, 2) == o.status);
                assert(value == o.value && method == o.method && il == o.il && ((image == nullptr && c.image == nullptr)
                    || (image != nullptr && c.image != nullptr && std::strcmp(image, c.image) == 0)));
                return o;
            }
            """);
        source.Append("int main() { roots_4(); roots_6(); int which = 0, kind = 0, input = 0; while (std::cin >> which >> kind >> input) { Outcome o; switch (which) {\n");
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $"case {i}: o = outcome_{i}(kind, input); break;\n");
        source.Append("default: return 2; } std::cout << o.status << ' ' << o.value << ' ' << o.method << ' ' << o.il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-joins"), cpp = Path.Combine(directory, "joins.cpp"), executable = Path.Combine(directory, "joins");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var measure = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", optimized ? "-O2" : "-O0",
            "-fstack-usage", "-Werror", "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows); Assert.All(rows, r => { Assert.Equal("static", r[^1]); Assert.InRange(int.Parse(r[^2], CultureInfo.InvariantCulture), 0, 2048); });
        var inputText = new StringBuilder(); var expected = new List<string>();
        for (int which = 0; which < programs.Length; which++)
            foreach (int kind in new[] { -1, 0, 1, 2, 5 })
                foreach (int input in new[] { int.MinValue, -1, 0, 1, 19, int.MaxValue })
                {
                    int status = 0, value = -123456789, token = 0, offset = 0;
                    try { value = oracles[which](kind, input); }
                    catch (DivideByZeroException) { status = 1; }
                    catch (NullReferenceException) { status = 4; }
                    if (status != 0)
                    {
                        token = programs[which].Flow.Entry.Token;
                        offset = programs[which].Flow.Methods.Single(m => m.Source.Token == token && m.Source.ContentHash == programs[which].Flow.Entry.ContentHash)
                            .Blocks.SelectMany(b => b.Values).Single(v => status == 1
                                ? v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }
                                : v.Expression is IrExpression.Call { VirtualDispatch: true }).ILOffset;
                    }
                    inputText.Append(CultureInfo.InvariantCulture, $"{which} {kind} {input}\n");
                    expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
                }
        var run = await NativeProcess.Run(executable, [], directory, inputText.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr); Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} .NET/native join outcomes at O{(optimized ? 2 : 0)}; maximum static frame {rows.Max(r => int.Parse(r[^2], CultureInfo.InvariantCulture))} bytes.");
    }

    private static Func<int, int, int> LoopOracle()
    {
        var method = new DynamicMethod("LiveReferenceJoinOracle", typeof(int), [typeof(int), typeof(int)]);
        var il = method.GetILGenerator(); var count = il.DeclareLocal(typeof(int)); var loop = il.DefineLabel(); var end = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stloc, count);
        il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Newobj, typeof(JoinContractLeft).GetConstructor([typeof(int)])!); il.Emit(OpCodes.Br, loop);
        il.MarkLabel(loop); il.Emit(OpCodes.Ldloc, count); il.Emit(OpCodes.Newobj, typeof(ProbeNode).GetConstructor([typeof(int)])!); il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldloc, count); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ble, end);
        il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Newobj, typeof(JoinContractRight).GetConstructor([typeof(int)])!);
        il.Emit(OpCodes.Ldloc, count); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Sub); il.Emit(OpCodes.Stloc, count); il.Emit(OpCodes.Br, loop);
        il.MarkLabel(end); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Callvirt, typeof(IJoinRead).GetMethod(nameof(IJoinRead.Read))!); il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<int, int, int>>();
    }
}
