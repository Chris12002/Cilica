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

public sealed class NativeObjectCastTests(ITestOutputHelper output)
{
    internal static ObjectIrProgram Translate(string name = nameof(ObjectCastProbe.Tests))
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ObjectCastProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ObjectCastProbe).GetMethod(name)!.MetadataToken), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }
    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Class_casts_identity_roots_and_fault_order_match_dotnet(bool optimized)
    {
        var programs = new[] { Translate(), Translate(nameof(ObjectCastProbe.CastOrder)) };
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Symbol(int i, string name) => Method(IrMethodId.From(programs[i].Flow.Methods.Single(m => m.Source.Token == typeof(ObjectCastProbe).GetMethod(name)!.MetadataToken).Source));
        string Layout(int i, System.Type type) => Type(ObjectTypeId.From(programs[i].Flow.Types.Types.Single(t => t.Source.Token == type.MetadataToken).Source));
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        var crossImageChecks = new StringBuilder();
        foreach (bool checkedCast in new[] { false, true })
        {
            var cross = ObjectCastTranslationTests.CrossImage(checkedCast);
            var emitted = ObjectCppEmitter.EmitProgram(cross); source.Append(emitted.Text);
            crossImageChecks.Append(CultureInfo.InvariantCulture, $$"""
                { using namespace {{emitted.EntryPoint[..^4] + "_closure"}}; Instance heap; Context c(heap);
                  const auto result = {{Method(cross.Flow.Entry)}}(c, nullptr);
                  assert(c.pending == {{(checkedCast ? 7 : 0)}}u && result == {{(checkedCast ? 0 : 1)}});
                  assert(c.used == 0 && c.depth == 0 && c.top == nullptr);
                  {{(checkedCast ? "assert(c.method == " + cross.Flow.Entry.Token.ToString(CultureInfo.InvariantCulture) + "u && c.il == 5u);" : "")}}
                }
                """);
        }
        var testMethod = programs[0].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[0].Flow.Entry);
        var roots = ObjectRootAssignment.Assign(programs[0]).Single(r => r.Method == programs[0].Flow.Entry);
        var cast = testMethod.Blocks.SelectMany(b => b.Values.Select(v => (Block: b.Id, Value: v))).Single(v => v.Value.Expression is IrExpression.CastClass);
        int castSlot = roots.Slots.Single(s => s.Kind == ObjectRootKind.Value && s.Block == cast.Block && s.Index == cast.Value.Id).Slot;
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <cstring>
            #include <iostream>
            namespace {{Scope(0)}} {
            static bool saw_cast_root = false;
            static void observe(Context& c) noexcept {
                assert(valid_roots(c));
                Frame* entry = c.top; while (entry->previous != nullptr) entry = entry->previous;
                assert(entry->count == {{roots.Slots.Length}}u);
                if (c.instance.count >= 2) {
                    Object* p = entry->slots[{{castSlot}}];
                    assert(p != nullptr && p->type == &{{Layout(0, typeof(CastLeaf))}}_descriptor);
                    saw_cast_root = true;
                }
            }
            }
            [[gnu::noinline]] static void test_roots() {
                using namespace {{Scope(0)}};
                Instance heap; Context c(heap); c.observe = observe;
                assert({{Method(programs[0].Flow.Entry)}}(c, 1, 19) == {{ObjectCastProbe.Tests(1, 19)}} && c.pending == 0 && saw_cast_root);
                assert(c.used == 0 && c.depth == 0 && c.top == nullptr);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Method(programs[0].Flow.Entry)}}(exact, 1, 19) == {{ObjectCastProbe.Tests(1, 19)}} && exact.pending == 0);
                Instance short_heap; Context short_frame(short_heap, c.peak - 1);
                (void){{Method(programs[0].Flow.Entry)}}(short_frame, 1, 19);
                assert(short_frame.pending == 9 && short_frame.used == 0 && short_frame.top == nullptr && short_frame.depth == 0);
            }
            [[gnu::noinline]] static void test_cast_boundary() {
                using namespace {{Scope(1)}};
                Instance heap; Context c(heap);
                Object* leaf = allocate<{{Layout(1, typeof(CastLeaf))}}>(c, {{Layout(1, typeof(CastLeaf))}}_descriptor);
                Object* sibling = allocate<{{Layout(1, typeof(CastSibling))}}>(c, {{Layout(1, typeof(CastSibling))}}_descriptor);
                assert({{Symbol(1, nameof(ObjectCastProbe.Checked))}}(c, leaf) == leaf && c.pending == 0);
                assert({{Symbol(1, nameof(ObjectCastProbe.Checked))}}(c, static_cast<Object*>(nullptr)) == nullptr && c.pending == 0);
                assert({{Symbol(1, nameof(ObjectCastProbe.Checked))}}(c, sibling) == nullptr && c.pending == 7);
                assert(c.used == 0 && c.depth == 0 && c.top == nullptr);
                c.pending = 0; c.image = nullptr; c.method = 0; c.il = 0;
                assert({{Symbol(1, nameof(ObjectCastProbe.Checked))}}(c, leaf) == leaf && c.pending == 0);
                Instance foreign; Context wrong(foreign); (void){{Symbol(1, nameof(ObjectCastProbe.Checked))}}(wrong, leaf);
                assert(wrong.pending == 6 && wrong.used == 0 && wrong.depth == 0 && wrong.top == nullptr);
                Context invalid(heap); (void){{Symbol(1, nameof(ObjectCastProbe.Checked))}}(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}));
                assert(invalid.pending == 6 && invalid.used == 0 && invalid.top == nullptr);
            }
            [[gnu::noinline]] static void test_isinst_boundary() {
                using namespace {{Scope(0)}};
                Instance heap; Context c(heap);
                Object* leaf = allocate<{{Layout(0, typeof(CastLeaf))}}>(c, {{Layout(0, typeof(CastLeaf))}}_descriptor);
                Object* parent = allocate<{{Layout(0, typeof(CastBase))}}>(c, {{Layout(0, typeof(CastBase))}}_descriptor);
                assert({{Symbol(0, nameof(ObjectCastProbe.Tested))}}(c, leaf) == leaf && c.pending == 0);
                assert({{Symbol(0, nameof(ObjectCastProbe.Tested))}}(c, parent) == nullptr && c.pending == 0);
                assert({{Symbol(0, nameof(ObjectCastProbe.Tested))}}(c, static_cast<Object*>(nullptr)) == nullptr && c.pending == 0);
                Context invalid(heap); (void){{Symbol(0, nameof(ObjectCastProbe.Tested))}}(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}));
                assert(invalid.pending == 6 && invalid.used == 0 && invalid.top == nullptr);
            }
            int main() {
                test_roots(); test_cast_boundary(); test_isinst_boundary();
                {{crossImageChecks}}
                int which = 0, kind = 0, input = 0;
                while (std::cin >> which >> kind >> input) {
                    std::int32_t value = -123456789; std::uint32_t status = 0, method = 0, il = 0;
                    switch (which) {
            """);
        for (int i = 0; i < programs.Length; i++)
            source.Append(CultureInfo.InvariantCulture, $$"""
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
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("casts-native"), cpp = Path.Combine(directory, "casts.cpp"), executable = Path.Combine(directory, "casts");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var inputs = new StringBuilder(); var expected = new List<string>();
        Func<int, int, int>[] oracles = [ObjectCastProbe.Tests, ObjectCastProbe.CastOrder];
        int[] values = [int.MinValue, -65536, -1, 0, 1, 65536, int.MaxValue];
        for (int kind = -1; kind <= 4; kind++) foreach (int input in values) for (int i = 0; i < programs.Length; i++)
        {
            int status = 0, value = -123456789, token = 0, offset = 0;
            try { value = oracles[i](kind, input); }
            catch (InvalidCastException) { status = 7; }
            catch (DivideByZeroException) { status = 1; }
            catch (NullReferenceException) { status = 4; }
            if (status != 0)
            {
                token = typeof(ObjectCastProbe).GetMethod(status == 7 ? nameof(ObjectCastProbe.Checked)
                    : status == 4 ? nameof(ObjectCastProbe.Read) : nameof(ObjectCastProbe.CastOrder))!.MetadataToken;
                offset = programs[i].Flow.Methods.Single(m => m.Source.Token == token).Blocks.SelectMany(b => b.Values)
                    .Single(v => status == 7 ? v.Expression is IrExpression.CastClass : status == 4 ? v.Expression is IrExpression.LoadField
                        : v.Expression is IrExpression.Binary { Operation: Int32Operation.DivideSigned }).ILOffset;
            }
            inputs.Append(CultureInfo.InvariantCulture, $"{i} {kind} {input}\n");
            expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
        }
        var run = await NativeProcess.Run(executable, [], directory, inputs.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} .NET/native outcomes, identity, roots, budgets and exact fault origins; O{(optimized ? 2 : 0)} with ASan/UBSan and 2 KiB gate.");
    }
}
