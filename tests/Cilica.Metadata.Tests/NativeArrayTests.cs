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

public sealed class NativeArrayTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Arrays_match_dotnet_with_ordered_faults_roots_budgets_and_owned_storage(bool optimized, bool verify)
    {
        string[] names = ["Integers", "Zeroes", "NullLength", "NullLoad", "NullStore", "References", "Dispatch", "Virtual", "Covariance",
            "CheckedStore", "FaultOrder", "Rooted", "InterfaceElements", "WidenAndNest", "EmptyIdentity", "Boundaries", "ArrayTypeTest", "ArrayCast"];
        var programs = names.Select(ObjectArrayTranslationTests.Authored).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Root(int i) => Method(programs[i].Flow.Entry);
        string Layout(int i, Type type) => TypeName(ObjectTypeId.From(programs[i].Flow.Types.Types.Single(t => t.Source.Name == type.FullName).Source));
        string Vector(int i, string element) => TypeName(programs[i].Flow.Types.Arrays.Single(a => element == "int" ? a.ArrayElement!.Kind == ObjectArrayElementKind.Int32
            : a.ArrayElement!.Type == ObjectTypeId.From(programs[i].Flow.Types.Types.Single(t => t.Source.Name == element).Source)));
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        source.Append("#include <cassert>\n#include <cstring>\n#include <iostream>\n");
        source.Append(CultureInfo.InvariantCulture, $$"""
            [[gnu::noinline]] static void allocation_budgets() {
                using namespace {{Scope(0)}};
                for (std::size_t length : {std::size_t{0}, std::size_t{1}, std::size_t{7} }) {
                    Instance heap; heap.limit = sizeof(ArrayObject) + length * sizeof(std::uint32_t); Context c(heap);
                    Object* value = allocate_array(c, {{Vector(0, "int")}}_descriptor, static_cast<std::uint32_t>(length));
                    assert(value != nullptr && c.pending == 0 && heap.bytes == heap.limit && heap.count == 1);
                    assert(array_length(value) == length);
                    for (std::size_t i = 0; i < length; ++i) assert(load_integer_element(value, static_cast<std::uint32_t>(i)) == 0);
                    Instance short_heap; short_heap.limit = heap.limit - 1; Context short_c(short_heap);
                    assert(allocate_array(short_c, {{Vector(0, "int")}}_descriptor, static_cast<std::uint32_t>(length)) == nullptr);
                    assert(short_heap.count == 0 && short_heap.bytes == 0 && short_heap.head == nullptr);
                }
                Instance heap; Context c(heap); heap.limit = 0;
                assert(allocate_array(c, {{Vector(0, "int")}}_descriptor, 0xffffffffu) == nullptr && c.pending == 2);
                c.pending = 0; heap.limit = 1048576; heap.object_limit = 0;
                assert(allocate_array(c, {{Vector(0, "int")}}_descriptor, 0) == nullptr && heap.count == 0);
                heap.object_limit = 4096;
                assert(allocate_array(c, {{Vector(0, "int")}}_descriptor, 0x7fffffffu) == nullptr && heap.count == 0);
                assert(allocate_array(c, {{Vector(0, "int")}}_descriptor, 0) != nullptr && heap.count == 1);
            }
            """);
        int boundary = Array.IndexOf(names, "Boundaries");
        string write = Method(IrMethodId.From(programs[boundary].Flow.Methods.Single(m => m.Source.Token == typeof(ArrayProbe).GetMethod("WriteArray")!.MetadataToken).Source));
        string read = Method(IrMethodId.From(programs[boundary].Flow.Methods.Single(m => m.Source.Token == typeof(ArrayProbe).GetMethod("ReadArray")!.MetadataToken).Source));
        source.Append(CultureInfo.InvariantCulture, $$"""
            [[gnu::noinline]] static void owned_stores_and_descriptors() {
                using namespace {{Scope(boundary)}}; Instance heap; Context c(heap);
                Object* array = allocate_array(c, {{Vector(boundary, "Cilica.Fixtures.Execution.ArrayEcho")}}_descriptor, 2);
                assert(heap.bytes == sizeof(ArrayObject) + 2 * sizeof(Object*));
                { Frame retained_frame; RootScope retained(c, retained_frame, &array, 1);
                const auto bytes = heap.bytes;
                heap.limit = bytes + sizeof(ArrayObject) + sizeof(Object*) - 1;
                assert(allocate_array(c, {{Vector(boundary, "Cilica.Fixtures.Execution.ArrayEcho")}}_descriptor, 1) == nullptr);
                assert(heap.bytes == bytes && heap.count == 1);
                ++heap.limit;
                Object* extra = allocate_array(c, {{Vector(boundary, "Cilica.Fixtures.Execution.ArrayEcho")}}_descriptor, 1);
                assert(extra != nullptr && heap.bytes == heap.limit && heap.count == 2);
                assert(extra->type->dynamic_references(extra)[0] == nullptr); }
                heap.limit = 1048576;
                Object* valid = allocate<{{Layout(boundary, typeof(ArrayEcho))}}>(c, {{Layout(boundary, typeof(ArrayEcho))}}_descriptor);
                Object* wrong = allocate<{{Layout(boundary, typeof(object))}}>(c, {{Layout(boundary, typeof(object))}}_descriptor);
                assert({{write}}(c, array, 0, valid) == 17 && c.pending == 0);
                assert(array->type->dynamic_reference_count(array) == 2);
                Object** slots = array->type->dynamic_references(array);
                assert(slots[0] == valid && slots[1] == nullptr);
                (void){{write}}(c, array, 0, wrong); assert(c.pending == 11 && slots[0] == valid);
                c.pending = 0; (void){{write}}(c, array, -1, wrong); assert(c.pending == 10 && slots[0] == valid);
                c.pending = 0; (void){{write}}(c, nullptr, -1, wrong); assert(c.pending == 4);
                c.pending = 0; (void){{write}}(c, array, 0, nullptr); assert(c.pending == 0 && slots[0] == nullptr);
                // Private forged-pointer injection requires the diagnostic build.
                #if CILICA_VERIFY_INTERNAL_REFERENCES
                Instance foreign; Context other(foreign);
                Object* foreign_value = allocate<{{Layout(boundary, typeof(ArrayEcho))}}>(other, {{Layout(boundary, typeof(ArrayEcho))}}_descriptor);
                (void){{write}}(c, array, 0, foreign_value); assert(c.pending == 6 && slots[0] == nullptr);
                c.pending = 0; (void){{write}}(c, array, 0, reinterpret_cast<Object*>(std::uintptr_t{1})); assert(c.pending == 6);
                // Native argument validation precedes executing the managed store.
                c.pending = 0; (void){{write}}(c, array, 2, reinterpret_cast<Object*>(std::uintptr_t{1})); assert(c.pending == 6);
                c.pending = 0; (void){{read}}(c, array, 0); assert(c.pending == 6);
                Object* integers = allocate_array(other, {{Vector(boundary, "int")}}_descriptor, 1);
                c.pending = 0; (void){{read}}(c, integers, 0); assert(c.pending == 6);
                c.pending = 0; (void){{read}}(c, reinterpret_cast<Object*>(std::uintptr_t{1}), 0); assert(c.pending == 6);
                #endif
                const Descriptor* original = array->type; const Descriptor copied = *original;
                array->type = &copied; c.pending = 0; (void){{write}}(c, array, 0, nullptr); assert(c.pending == 6);
                array->type = reinterpret_cast<const Descriptor*>(std::uintptr_t{1}); c.pending = 0;
                (void){{write}}(c, array, 0, nullptr); assert(c.pending == 6); array->type = original;
                const Descriptor* element_type = valid->type;
                valid->type = reinterpret_cast<const Descriptor*>(std::uintptr_t{1}); c.pending = 0;
                (void){{write}}(c, array, 0, valid); assert(c.pending == 6 && slots[0] == nullptr); valid->type = element_type;
                assert(c.top == nullptr && c.used == 0 && c.depth == 0);
            }
            """);
        int rooted = Array.IndexOf(names, "Rooted");
        var entry = programs[rooted].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[rooted].Flow.Entry);
        var loaded = entry.Blocks.SelectMany(b => b.Values.Select(v => (Block: b.Id, Value: v))).First(p => p.Value.Expression is IrExpression.LoadElement);
        var roots = ObjectRootAssignment.Assign(programs[rooted]).Single(p => p.Method == programs[rooted].Flow.Entry);
        int slot = roots.Slots.Single(s => s.Kind == ObjectRootKind.Value && s.Block == loaded.Block && s.Index == loaded.Value.Id).Slot;
        source.Append(CultureInfo.InvariantCulture, $$"""
            namespace {{Scope(rooted)}} {
            static int observations = 0;
            static void observe_arrays(Context& c) noexcept {
                assert(valid_roots(c) && c.top != nullptr);
                if (c.instance.count >= 2) {
                    Frame* root = c.top; while (root->previous != nullptr) root = root->previous;
                    Object* array = c.instance.head; while (array->next != nullptr) array = array->next;
                    assert(root->slots[{{slot}}] == array->type->dynamic_references(array)[0]);
                    assert(root->slots[{{slot}}] != nullptr); ++observations;
                }
            }
            }
            [[gnu::noinline]] static void roots_and_frame_budgets() {
                using namespace {{Scope(rooted)}}; Instance heap; Context c(heap); c.observe = observe_arrays;
                assert({{Root(rooted)}}(c, 3, 19) == {{ArrayProbe.Rooted(3, 19)}} && c.pending == 0 && observations == 6);
                assert(c.top == nullptr && c.used == 0 && c.depth == 0);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Root(rooted)}}(exact, 3, 19) == {{ArrayProbe.Rooted(3, 19)}} && exact.pending == 0);
                Instance short_heap; Context short_c(short_heap, c.peak - 1); (void){{Root(rooted)}}(short_c, 3, 19);
                assert(short_c.pending == 9 && short_c.used == 0 && short_c.top == nullptr && short_c.depth == 0);
                for (std::size_t count = 0; count < heap.count; ++count) {
                    Instance limited; limited.object_limit = count; Context failed(limited); const auto result = {{Root(rooted)}}(failed, 3, 19);
                    // Five slots suffice once dead loop temporaries can be reclaimed.
                    assert(count < 5 ? failed.pending == 5 : failed.pending == 0 && result == {{ArrayProbe.Rooted(3, 19)}});
                    assert(limited.count <= count && failed.used == 0 && failed.top == nullptr && failed.depth == 0);
                    failed.pending = 0; limited.object_limit = 4096;
                    assert({{Root(rooted)}}(failed, 2, 7) == {{ArrayProbe.Rooted(2, 7)}} && failed.pending == 0);
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
        source.Append("int main() { allocation_budgets(); owned_stores_and_descriptors(); roots_and_frame_budgets(); int which, a, b, d; while (std::cin >> which >> a >> b >> d) { Outcome o; switch (which) {\n");
        for (int i = 0; i < names.Length; i++) source.Append(CultureInfo.InvariantCulture, $"case {i}: o = outcome_{i}(a, b, d); break;\n");
        source.Append("default: return 2; } std::cout << o.status << ' ' << o.value << ' ' << o.method << ' ' << o.il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-arrays"), cpp = Path.Combine(directory, "arrays.cpp"), executable = Path.Combine(directory, "arrays");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-DCILICA_VERIFY_INTERNAL_REFERENCES=" + (verify ? "1" : "0"), "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var measure = await NativeProcess.Run(compiler, ["-std=c++20", "-DCILICA_VERIFY_INTERNAL_REFERENCES=" + (verify ? "1" : "0"), "-fno-exceptions", "-fno-rtti", optimized ? "-O2" : "-O0", "-fstack-usage",
            "-Werror", "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows); Assert.All(rows, r => { Assert.Equal("static", r[^1]); Assert.InRange(int.Parse(r[^2], CultureInfo.InvariantCulture), 0, 2048); });
        var inputText = new StringBuilder(); var expected = new List<string>();
        for (int i = 0; i < names.Length; i++) foreach (var args in Cases(names[i]))
        {
            int status = 0, value = -123456789, token = 0, offset = 0;
            try { value = (int)typeof(ArrayProbe).GetMethod(names[i])!.Invoke(null, args.Cast<object>().ToArray())!; }
            catch (TargetInvocationException error)
            {
                status = error.InnerException switch { DivideByZeroException => 1, OverflowException => 2, NullReferenceException => 4,
                    IndexOutOfRangeException => 10, ArrayTypeMismatchException => 11, InvalidCastException => 7, _ => throw error.InnerException! };
                var method = names[i] == "Boundaries" ? programs[i].Flow.Methods.Single(m => m.Source.Token == typeof(ArrayProbe).GetMethod("ReadArray")!.MetadataToken)
                    : programs[i].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[i].Flow.Entry);
                token = method.Source.Token;
                var statements = method.Blocks.SelectMany(b => b.Statements).OrderBy(Offset);
                var fault = statements.First(s => status switch
                {
                    1 => s is IrStatement.Define { Value.Expression: IrExpression.Binary { Operation: Int32Operation.DivideSigned } },
                    7 => s is IrStatement.Define { Value.Expression: IrExpression.CastClass },
                    2 => s is IrStatement.Define { Value.Expression: IrExpression.NewArray }
                        && s == statements.Last(t => t is IrStatement.Define { Value.Expression: IrExpression.NewArray }),
                    4 when names[i] == "Rooted" => s is IrStatement.Define { Value.Expression: IrExpression.LoadField } && s == statements.Last(t => t is IrStatement.Define { Value.Expression: IrExpression.LoadField }),
                    _ => s is IrStatement.StoreElement or IrStatement.Define { Value.Expression: IrExpression.LoadElement or IrExpression.ArrayLength },
                });
                offset = Offset(fault);
            }
            int[] padded = [.. args, .. Enumerable.Repeat(0, 3 - args.Length)];
            inputText.Append(CultureInfo.InvariantCulture, $"{i} {padded[0]} {padded[1]} {padded[2]}\n");
            expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
        }
        var run = await NativeProcess.Run(executable, [], directory, inputText.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr); Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} .NET/native array outcomes at O{(optimized ? 2 : 0)}; maximum static frame {rows.Max(r => int.Parse(r[^2], CultureInfo.InvariantCulture))} bytes.");
    }

    private static IEnumerable<int[]> Cases(string name) => name switch
    {
        "Integers" => from length in new[] { -1, 0, 1, 3 } from index in new[] { -1, 0, 2, 3, int.MaxValue } from value in new[] { int.MinValue, 0, int.MaxValue } select new[] { length, index, value },
        "CheckedStore" => from kind in new[] { 0, 1, 2, 3 } from index in new[] { -1, 0, 1, 2, int.MaxValue } select new[] { kind, index },
        "FaultOrder" => from kind in new[] { 0, 1 } from divisor in new[] { -1, 0, 1, 7 } select new[] { kind, divisor },
        "Rooted" => from count in new[] { 0, 1, 3 } from value in new[] { int.MinValue, 7, int.MaxValue } select new[] { count, value },
        "NullLength" or "References" or "EmptyIdentity" => [Array.Empty<int>()],
        _ => new[] { -1, 0, 1, 2, 7 }.Select(v => new[] { v }),
    };
    private static int Offset(IrStatement statement) => statement switch
    {
        IrStatement.Define d => d.Value.ILOffset, IrStatement.StoreElement s => s.ILOffset,
        IrStatement.StoreField s => s.ILOffset, IrStatement.StoreLocal s => s.ILOffset, IrStatement.Invoke s => s.ILOffset,
        _ => throw new InvalidOperationException(),
    };
    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string TypeName(ObjectTypeId type) => "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture)
        + (type.ArrayElement is null ? "" : type.ArrayElement.Kind == ObjectArrayElementKind.Int32 ? "_vector_i4" : "_vector_ref_" + TypeName(type.ArrayElement.Type!));
}
