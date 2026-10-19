using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeStringTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Strings_match_dotnet_with_identity_ordered_faults_and_owned_storage(bool optimized)
    {
        string[] names = ["Length", "CodeUnit", "Equal", "NotEqual", "StaticEquals", "Concat", "NullLength", "NullCodeUnit", "NullConcat",
            "Fields", "Cast", "Test", "Joined", "Dispatch", "Virtual", "LiteralIdentity", "Contents", "Concatenation", "FreshIdentity",
            "Rooted", "OrderedFault", "Boundaries", "CheckedStore", "ConcatCodeUnit"];
        var programs = names.Select(name => {
            var result = ObjectStringTranslationTests.Authored(name);
            Assert.True(result.Success, name + ": " + string.Join("; ", result.Diagnostics)); return result.Program!;
        }).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Root(int i) => Method(programs[i].Flow.Entry);
        string Descriptor(int i) => "t_" + programs[i].Flow.Types.String!.ContentHash + "_" + programs[i].Flow.Types.String!.Token.ToString("x8", CultureInfo.InvariantCulture) + "_descriptor";
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        source.Append("#include <cassert>\n#include <cstring>\n#include <iostream>\n");
        source.Append(CultureInfo.InvariantCulture, $$"""
            [[gnu::noinline]] static void storage_and_literal_identity() {
                using namespace {{Scope(0)}};
                const std::uint16_t units[] = {65u, 0u, 0xd800u};
                for (std::uint32_t length : {0u, 1u, 3u}) {
                    Instance heap; heap.limit = sizeof(StringObject) + length * sizeof(std::uint16_t); Context c(heap);
                    Object* value = string_literal(c, {{Descriptor(0)}}, 1, units, length);
                    assert(value != nullptr && heap.bytes == heap.limit && heap.count == 1);
                    assert(string_length(value) == length);
                    for (std::uint32_t i = 0; i < length; ++i) assert(string_code_unit(value, i) == units[i]);
                    heap.object_limit = 0;
                    assert(string_literal(c, {{Descriptor(0)}}, 1, units, length) == value && heap.count == 1);
                    Instance short_heap; short_heap.limit = heap.limit - 1; Context short_c(short_heap);
                    assert(string_literal(short_c, {{Descriptor(0)}}, 1, units, length) == nullptr);
                    assert(short_heap.bytes == 0 && short_heap.count == 0 && short_heap.head == nullptr && short_heap.literals == nullptr);
                    ++short_heap.limit;
                    Object* other = string_literal(short_c, {{Descriptor(0)}}, 1, units, length);
                    assert(other != nullptr && other != value && string_equals(value, other) == 1);
                }
                Instance heap; Context c(heap);
                assert(allocate_string(c, {{Descriptor(0)}}, nullptr, 0x7fffffffu, nullptr, 1u) == nullptr);
                assert(allocate_string(c, {{Descriptor(0)}}, nullptr, 0xffffffffu, nullptr, 0u) == nullptr);
                assert(allocate_string(c, {{Descriptor(0)}}, nullptr, 0x7fffffffu, nullptr, 0u) == nullptr);
                assert(heap.count == 0 && heap.bytes == 0);
                Object* empty = string_concat(c, {{Descriptor(0)}}, nullptr, nullptr);
                assert(empty != nullptr && empty == string_literal(c, {{Descriptor(0)}}, 0, nullptr, 0));
                Object* a = string_literal(c, {{Descriptor(0)}}, 1, units, 3);
                const auto bytes = heap.bytes;
                heap.limit = bytes;
                assert(string_concat(c, {{Descriptor(0)}}, empty, a) == a);
                assert(string_concat(c, {{Descriptor(0)}}, a, nullptr) == a);
                assert(string_concat(c, {{Descriptor(0)}}, a, a) == nullptr && heap.bytes == bytes && heap.count == 2);
                heap.limit += sizeof(StringObject) + 12;
                Object* joined = string_concat(c, {{Descriptor(0)}}, a, a);
                assert(joined != nullptr && joined != a && heap.bytes == heap.limit && heap.count == 3);
                for (std::uint32_t i = 0; i < 6; ++i) assert(string_code_unit(joined, i) == units[i % 3]);
                Object* slots[] = {reinterpret_cast<Object*>(std::uintptr_t{1})}; Frame frame;
                RootScope roots(c, frame, slots, 1);
                assert(allocate_string(c, {{Descriptor(0)}}, units, 1, nullptr, 0) == nullptr && c.pending == 6);
            }
            """);
        int boundary = Array.IndexOf(names, "Boundaries");
        string read = Method(IrMethodId.From(programs[boundary].Flow.Methods.Single(m => m.Source.Token == typeof(StringProbe).GetMethod("Read")!.MetadataToken).Source));
        source.Append(CultureInfo.InvariantCulture, $$"""
            [[gnu::noinline]] static void owned_arguments() {
                using namespace {{Scope(boundary)}}; Instance heap; Context c(heap);
                const std::uint16_t units[] = {0xdfffu};
                Object* value = string_literal(c, {{Descriptor(boundary)}}, 1, units, 1);
                assert({{read}}(c, value, 0) == 0xdfff && c.pending == 0);
                const Descriptor* original = value->type; const Descriptor copied = *original;
                value->type = &copied; (void){{read}}(c, value, 0); assert(c.pending == 6);
                value->type = reinterpret_cast<const Descriptor*>(std::uintptr_t{1}); c.pending = 0;
                (void){{read}}(c, value, 0); assert(c.pending == 6); value->type = original;
                Instance other; Context foreign(other); c.pending = 0;
                Object* alien = string_literal(foreign, {{Descriptor(boundary)}}, 1, units, 1);
                (void){{read}}(c, alien, 0); assert(c.pending == 6);
                c.pending = 0; (void){{read}}(c, reinterpret_cast<Object*>(std::uintptr_t{1}), 0); assert(c.pending == 6);
                c.pending = 0; (void){{read}}(c, nullptr, -1); assert(c.pending == 4);
                assert(c.top == nullptr && c.used == 0 && c.depth == 0);
            }
            """);
        int rooted = Array.IndexOf(names, "Rooted");
        var entry = programs[rooted].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[rooted].Flow.Entry);
        var concat = entry.Blocks.SelectMany(b => b.Values.Select(v => (Block: b.Id, Value: v))).Single(p => p.Value.Expression is IrExpression.StringConcat);
        var operation = (IrExpression.StringConcat)concat.Value.Expression;
        var literalOffsets = entry.Blocks.SelectMany(b => b.Values).Where(v => v.Expression is IrExpression.StringLiteral).Select(v => v.ILOffset).Order().ToArray();
        var roots = ObjectRootAssignment.Assign(programs[rooted]).Single(p => p.Method == programs[rooted].Flow.Entry);
        int Slot(int id) => roots.Slots.Single(s => s.Kind == ObjectRootKind.Value && s.Block == concat.Block && s.Index == id).Slot;
        source.Append(CultureInfo.InvariantCulture, $$"""
            namespace {{Scope(rooted)}} {
            static int observations = 0;
            static void observe_strings(Context& c) noexcept {
                assert(valid_roots(c) && c.top != nullptr);
                if (c.instance.count >= 2) {
                    assert(c.top->slots[{{Slot(operation.Left)}}] != nullptr && c.top->slots[{{Slot(operation.Right)}}] != nullptr);
                    ++observations;
                }
            }
            }
            [[gnu::noinline]] static void roots_and_budgets() {
                using namespace {{Scope(rooted)}}; Instance heap; Context c(heap); c.observe = observe_strings;
                assert({{Root(rooted)}}(c, 3) == {{StringProbe.Rooted(3)}} && c.pending == 0 && observations == 2);
                assert(c.top == nullptr && c.used == 0 && c.depth == 0);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Root(rooted)}}(exact, 3) == {{StringProbe.Rooted(3)}} && exact.pending == 0);
                Instance short_heap; Context short_c(short_heap, c.peak - 1); (void){{Root(rooted)}}(short_c, 3);
                assert(short_c.pending == 9 && short_c.used == 0 && short_c.top == nullptr && short_c.depth == 0);
                for (std::size_t count = 0; count < heap.count; ++count) {
                    Instance limited; limited.object_limit = count; Context failed(limited); (void){{Root(rooted)}}(failed, 3);
                    assert(failed.pending == 5 && limited.count == count && failed.used == 0 && failed.top == nullptr && failed.depth == 0);
                    assert(failed.method == {{entry.Source.Token}}u && failed.image != nullptr);
                    assert(failed.il == (count == 0 ? {{literalOffsets[0]}}u : count == 1 ? {{literalOffsets[1]}}u : {{concat.Value.ILOffset}}u));
                    failed.pending = 0; limited.object_limit = 4096;
                    assert({{Root(rooted)}}(failed, 2) == {{StringProbe.Rooted(2)}} && failed.pending == 0);
                }
            }
            """);
        source.Append("struct Outcome { std::uint32_t status = 0, method = 0, il = 0; std::int32_t value = -123456789; };\n");
        for (int i = 0; i < names.Length; i++)
        {
            int count = programs[i].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[i].Flow.Entry).Parameters.Length;
            source.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static Outcome outcome_{{i}}([[maybe_unused]] int a, [[maybe_unused]] int b, [[maybe_unused]] int d) {
                    using namespace {{Scope(i)}}; Instance heap; Context c(heap); Outcome o;
                    const auto value = {{Root(i)}}(c{{(count > 0 ? ", a" : "")}}{{(count > 1 ? ", b" : "")}}{{(count > 2 ? ", d" : "")}});
                    assert(c.top == nullptr && c.used == 0 && c.depth == 0);
                    o.status = c.pending; o.method = c.method; o.il = c.il; if (o.status == 0) o.value = value;
                    std::int32_t result = -123456789; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                    const std::int32_t parameters[] = {a, b, d};
                    assert({{artifacts[i].EntryPoint}}(&result, &method, &il, &image, parameters, {{count}}) == o.status);
                    assert(result == o.value && method == o.method && il == o.il && ((image == nullptr && c.image == nullptr)
                        || (image != nullptr && c.image != nullptr && std::strcmp(image, c.image) == 0)));
                    return o;
                }
                """);
        }
        source.Append("int main() { storage_and_literal_identity(); owned_arguments(); roots_and_budgets(); int which, a, b, d; while (std::cin >> which >> a >> b >> d) { Outcome o; switch (which) {\n");
        for (int i = 0; i < names.Length; i++) source.Append(CultureInfo.InvariantCulture, $"case {i}: o = outcome_{i}(a, b, d); break;\n");
        source.Append("default: return 2; } std::cout << o.status << ' ' << o.value << ' ' << o.method << ' ' << o.il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-strings"), cpp = Path.Combine(directory, "strings.cpp"), executable = Path.Combine(directory, "strings");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", /* Private forged-pointer probes require diagnostic membership checks. */ "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var measure = await NativeProcess.Run(compiler, ["-std=c++20", "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", optimized ? "-O2" : "-O0", "-fstack-usage",
            "-Werror", "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows); Assert.All(rows, r => { Assert.Equal("static", r[^1]); Assert.InRange(int.Parse(r[^2], CultureInfo.InvariantCulture), 0, 2048); });
        var inputText = new StringBuilder(); var expected = new List<string>();
        for (int i = 0; i < names.Length; i++) foreach (var args in Cases(names[i]))
        {
            int status = 0, value = -123456789, token = 0, offset = 0;
            try { value = (int)typeof(StringProbe).GetMethod(names[i])!.Invoke(null, args.Cast<object>().ToArray())!; }
            catch (TargetInvocationException error)
            {
                status = error.InnerException switch { DivideByZeroException => 1, OverflowException => 2, NullReferenceException => 4,
                    IndexOutOfRangeException => 10, ArrayTypeMismatchException => 11, InvalidCastException => 7, _ => throw error.InnerException! };
                var method = names[i] == "Boundaries" ? programs[i].Flow.Methods.Single(m => m.Source.Token == typeof(StringProbe).GetMethod("Read")!.MetadataToken)
                    : programs[i].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[i].Flow.Entry);
                token = method.Source.Token;
                var statements = method.Blocks.SelectMany(b => b.Statements).OrderBy(Offset);
                var fault = statements.First(s => status switch
                {
                    1 => s is IrStatement.Define { Value.Expression: IrExpression.Binary { Operation: Int32Operation.DivideSigned } },
                    7 => s is IrStatement.Define { Value.Expression: IrExpression.CastClass },
                    11 => s is IrStatement.StoreElement,
                    _ => s is IrStatement.Define { Value.Expression: IrExpression.StringLength or IrExpression.StringCodeUnit },
                });
                offset = Offset(fault);
            }
            int[] padded = [.. args, .. Enumerable.Repeat(0, 3 - args.Length)];
            inputText.Append(CultureInfo.InvariantCulture, $"{i} {padded[0]} {padded[1]} {padded[2]}\n");
            expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
        }
        var run = await NativeProcess.Run(executable, [], directory, inputText.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr); Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} .NET/native string outcomes at O{(optimized ? 2 : 0)}; maximum static frame {rows.Max(r => int.Parse(r[^2], CultureInfo.InvariantCulture))} bytes.");
    }

    private static IEnumerable<int[]> Cases(string name) => name switch
    {
        "Contents" or "Concatenation" => from left in Enumerable.Range(0, 6) from right in Enumerable.Range(0, 6) select new[] { left, right },
        "CodeUnit" or "NullCodeUnit" or "Boundaries" or "ConcatCodeUnit" => new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6, int.MaxValue }.Select(v => new[] { v }),
        "Cast" or "Joined" or "CheckedStore" or "Rooted" or "OrderedFault" => new[] { -1, 0, 1, 3, 7 }.Select(v => new[] { v }),
        _ => [Array.Empty<int>()],
    };
    private static int Offset(IrStatement statement) => statement switch
    {
        IrStatement.Define d => d.Value.ILOffset, IrStatement.StoreElement s => s.ILOffset,
        IrStatement.StoreField s => s.ILOffset, IrStatement.StoreLocal s => s.ILOffset, IrStatement.Invoke s => s.ILOffset,
        _ => throw new InvalidOperationException(),
    };
    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
}
