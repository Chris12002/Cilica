using System;
using System.Collections.Generic;
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

public sealed class NativeObjectInterfaceCastTests(ITestOutputHelper output)
{
    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static ObjectIrProgram Translate(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(InterfaceCastProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(InterfaceCastProbe).GetMethod(name)!.MetadataToken), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interface_casts_preserve_identity_roots_budgets_ownership_and_dotnet_fault_order(bool optimized)
    {
        string[] names = [nameof(InterfaceCastProbe.Tests), nameof(InterfaceCastProbe.Order), nameof(InterfaceCastProbe.ReverseOrder)];
        Func<int, int, int>[] oracles = [InterfaceCastProbe.Tests, InterfaceCastProbe.Order, InterfaceCastProbe.ReverseOrder];
        var programs = names.Select(Translate).ToArray(); var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Layout(int i, System.Type type) => Type(ObjectTypeId.From(programs[i].Flow.Types.Types.Single(t => t.Source.Token == type.MetadataToken).Source));
        string Symbol(int i, string name) => Method(IrMethodId.From(programs[i].Flow.Methods.Single(m => m.Source.Token == typeof(InterfaceCastProbe).GetMethod(name)!.MetadataToken).Source));
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        var extraCalls = new StringBuilder(); int extra = 0;
        foreach (bool cast in new[] { false, true }) foreach (bool cross in new[] { false, true })
        {
            var program = cross ? ObjectCastTranslationTests.CrossImage(cast, contract: true) : ObjectCastTranslationTests.Raw(cast, "interface").Program!;
            var artifact = ObjectCppEmitter.EmitProgram(program); source.Append(artifact.Text);
            source.Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void extra_{{extra}}() {
                    using namespace {{artifact.EntryPoint[..^4] + "_closure"}}; Instance heap; Context c(heap);
                    const auto result = {{Method(program.Flow.Entry)}}(c, nullptr);
                    assert(c.pending == {{(cross && cast ? 7 : 0)}}u && result == {{(cross && cast ? 0 : 1)}});
                    assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                    {{(cross && cast ? "assert(c.method == " + program.Flow.Entry.Token.ToString(CultureInfo.InvariantCulture) + "u && c.il == 5u);" : "")}}
                }
                """).AppendLine();
            extraCalls.Append(CultureInfo.InvariantCulture, $"extra_{extra++}(); ");
        }
        var entry = programs[0].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[0].Flow.Entry);
        var roots = ObjectRootAssignment.Assign(programs[0]).Single(r => r.Method == programs[0].Flow.Entry);
        int RootSlot(bool cast)
        {
            var operation = entry.Blocks.SelectMany(b => b.Values.Select(v => (Block: b.Id, Value: v))).Single(p =>
                (cast ? p.Value.Expression is IrExpression.CastClass : p.Value.Expression is IrExpression.IsInstance)
                && p.Value.Type.Class!.Token == typeof(IExactRead).MetadataToken);
            return roots.Slots.Single(s => s.Kind == ObjectRootKind.Value && s.Block == operation.Block && s.Index == operation.Value.Id).Slot;
        }
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cstring>
            #include <iostream>
            namespace {{Scope(0)}} {
            static unsigned observations = 0;
            static std::size_t required_objects = 0;
            static void observe(Context& c) noexcept {
                const auto live = mark_reachable(c); assert(live.status == MarkStatus::complete);
                if (required_objects < live.reachable + 1) required_objects = live.reachable + 1;
                assert(valid_roots(c));
                Frame* entry = c.top; while (entry->previous != nullptr) entry = entry->previous;
                if (c.instance.count >= 2) {
                    Object* cast = entry->slots[{{RootSlot(true)}}];
                    Object* test = entry->slots[{{RootSlot(false)}}];
                    assert(cast != nullptr && cast == test && c.instance.owns(cast));
                    assert(cast->type == &{{Layout(0, typeof(ExactInterfaceDiamond))}}_descriptor); ++observations;
                }
            }
            }
            [[gnu::noinline]] static void roots_and_limits() {
                using namespace {{Scope(0)}}; Instance heap; Context c(heap); c.observe = observe;
                assert({{Method(programs[0].Flow.Entry)}}(c, 0, 19) == {{InterfaceCastProbe.Tests(0, 19)}} && c.pending == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0 && heap.count == 4 && observations > 0);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Method(programs[0].Flow.Entry)}}(exact, 0, 19) == {{InterfaceCastProbe.Tests(0, 19)}} && exact.pending == 0);
                Instance short_heap; Context short_frame(short_heap, c.peak - 1);
                (void){{Method(programs[0].Flow.Entry)}}(short_frame, 0, 19);
                assert(short_frame.pending == 9 && short_frame.used == 0 && short_frame.top == nullptr && short_frame.depth == 0);
                assert(short_frame.method != 0 && short_frame.image != nullptr);
                short_frame.pending = 0; short_frame.image = nullptr; short_frame.method = 0; short_frame.il = 0;
                assert({{Symbol(0, nameof(InterfaceCastProbe.Tested))}}(short_frame, static_cast<Object*>(nullptr)) == nullptr && short_frame.pending == 0);
                assert(short_frame.used == 0 && short_frame.top == nullptr && short_frame.depth == 0);
                for (std::size_t limit = 0; limit < 4; ++limit) {
                    Instance limited; limited.object_limit = limit; Context fault(limited);
                    const auto value = {{Method(programs[0].Flow.Entry)}}(fault, 0, 19);
                    // Pressure is determined by live roots, not cumulative allocations.
                    assert(fault.pending == (limit < required_objects ? 5u : 0u));
                    if (fault.pending == 0) assert(value == {{InterfaceCastProbe.Tests(0, 19)}});
                    assert(limited.count <= limit && fault.top == nullptr && fault.used == 0 && fault.depth == 0);
                    limited.object_limit = 4096; fault.pending = 0; fault.image = nullptr; fault.method = 0; fault.il = 0;
                    assert({{Method(programs[0].Flow.Entry)}}(fault, 0, 7) == {{InterfaceCastProbe.Tests(0, 7)}} && fault.pending == 0);
                    assert(fault.used == 0 && fault.top == nullptr && fault.depth == 0);
                }
            }
            """).AppendLine();
        for (int i = 0; i < 2; i++)
        {
            string helper = i == 0 ? nameof(InterfaceCastProbe.Tested) : nameof(InterfaceCastProbe.Checked);
            int mismatch = i == 0 ? 0 : 7;
            source.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static void boundary_{{i}}() {
                    using namespace {{Scope(i)}}; Instance heap; Context c(heap);
                    Object* value = allocate<{{Layout(i, typeof(ExactInterfaceDiamond))}}>(c, {{Layout(i, typeof(ExactInterfaceDiamond))}}_descriptor);
                    Object* unrelated = allocate<{{Layout(i, typeof(InterfaceCastUnrelated))}}>(c, {{Layout(i, typeof(InterfaceCastUnrelated))}}_descriptor);
                    assert({{Symbol(i, helper)}}(c, value) == value && c.pending == 0);
                    assert({{Symbol(i, helper)}}(c, static_cast<Object*>(nullptr)) == nullptr && c.pending == 0);
                    assert({{Symbol(i, helper)}}(c, unrelated) == nullptr && c.pending == {{mismatch}});
                    assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                    c.pending = 0; c.image = nullptr; c.method = 0; c.il = 0;
                    assert({{Symbol(i, helper)}}(c, value) == value && c.pending == 0);
                    Instance foreign; Context wrong(foreign); (void){{Symbol(i, helper)}}(wrong, value);
                    assert(wrong.pending == 6 && wrong.used == 0 && wrong.top == nullptr && wrong.depth == 0);
                    Context invalid(heap); (void){{Symbol(i, helper)}}(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}));
                    assert(invalid.pending == 6 && invalid.used == 0 && invalid.top == nullptr && invalid.depth == 0);
                    static const Descriptor copied = {{Layout(i, typeof(ExactInterfaceDiamond))}}_descriptor;
                    const Descriptor* original = value->type;
                    value->type = &copied;
                    Context duplicate(heap); assert({{Symbol(i, helper)}}(duplicate, value) == nullptr && duplicate.pending == {{mismatch}});
                    assert(duplicate.used == 0 && duplicate.top == nullptr && duplicate.depth == 0);
                    value->type = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                    Context unreadable(heap); assert({{Symbol(i, helper)}}(unreadable, value) == nullptr && unreadable.pending == {{mismatch}});
                    assert(unreadable.used == 0 && unreadable.top == nullptr && unreadable.depth == 0);
                    value->type = original;
                }
                """).AppendLine();
        }
        source.Append("struct Outcome { std::int32_t value = -123456789; std::uint32_t status = 0, method = 0, il = 0; };\n");
        for (int i = 0; i < programs.Length; i++)
            source.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static Outcome outcome_{{i}}(int kind, int input) {
                    Outcome outcome; using namespace {{Scope(i)}}; Instance heap; Context c(heap);
                    const auto returned = {{Method(programs[i].Flow.Entry)}}(c, kind, input);
                    assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                    outcome.status = c.pending; outcome.method = c.method; outcome.il = c.il; if (outcome.status == 0) outcome.value = returned;
                    const std::int32_t parameters[] = {kind, input}; std::int32_t boundary = -123456789;
                    std::uint32_t bm = 0, bi = 0; const char* bh = nullptr;
                    assert({{artifacts[i].EntryPoint}}(&boundary, &bm, &bi, &bh, parameters, 2) == outcome.status);
                    assert(boundary == outcome.value && bm == outcome.method && bi == outcome.il && ((bh == nullptr && c.image == nullptr)
                        || (bh != nullptr && c.image != nullptr && std::strcmp(bh, c.image) == 0)));
                    return outcome;
                }
                """).AppendLine();
        source.Append("int main() { roots_and_limits(); boundary_0(); boundary_1(); ").Append(extraCalls)
            .Append("int which = 0, kind = 0, input = 0; while (std::cin >> which >> kind >> input) { Outcome outcome; switch (which) {\n");
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $"case {i}: outcome = outcome_{i}(kind, input); break;\n");
        source.Append("default: return 2; } std::cout << outcome.status << ' ' << outcome.value << ' ' << outcome.method << ' ' << outcome.il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("interface-casts-native");
        string cpp = Path.Combine(directory, "casts.cpp"), executable = Path.Combine(directory, "casts");
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
        output.WriteLine($"O{(optimized ? 2 : 0)}: {rows.Length} functions measured; maximum {rows.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} stack bytes.");
        var inputs = new StringBuilder(); var expected = new List<string>();
        int[] values = [int.MinValue, -65536, -1, 0, 1, 19, 65536, int.MaxValue];
        for (int kind = -1; kind <= 4; kind++) foreach (int value in values) for (int i = 0; i < programs.Length; i++)
        {
            int status = 0, result = -123456789, token = 0, offset = 0;
            try { result = oracles[i](kind, value); }
            catch (InvalidCastException) { status = 7; }
            catch (DivideByZeroException) { status = 1; }
            catch (NullReferenceException) { status = 4; }
            if (status != 0)
            {
                token = typeof(InterfaceCastProbe).GetMethod(status == 7 ? nameof(InterfaceCastProbe.Checked)
                    : status == 4 ? i == 1 ? nameof(InterfaceCastProbe.Read) : nameof(InterfaceCastProbe.Take) : names[i])!.MetadataToken;
                offset = programs[i].Flow.Methods.Single(m => m.Source.Token == token).Blocks.SelectMany(b => b.Values).Single(v =>
                    status == 7 ? v.Expression is IrExpression.CastClass : status == 4 ? v.Expression is IrExpression.Call { VirtualDispatch: true }
                    : v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }).ILOffset;
            }
            inputs.Append(CultureInfo.InvariantCulture, $"{i} {kind} {value}\n"); expected.Add(FormattableString.Invariant($"{status} {result} {token} {offset}"));
        }
        var run = await NativeProcess.Run(executable, [], directory, inputs.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr); Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} .NET/native interface cast outcomes with exact origins, identity, roots, budgets and ownership at O{(optimized ? 2 : 0)}.");
    }
}
