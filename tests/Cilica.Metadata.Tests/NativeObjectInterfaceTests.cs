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

public sealed class NativeObjectInterfaceTests(ITestOutputHelper output)
{
    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_interface_calls_match_dotnet_with_roots_ownership_fault_order_and_resource_cleanup(bool optimized)
    {
        string[] names = [nameof(InterfaceCallProbe.Run), nameof(InterfaceCallProbe.OtherContract), nameof(InterfaceCallProbe.Diamond),
            nameof(InterfaceCallProbe.ReferenceFlow), nameof(InterfaceCallProbe.Recursive), nameof(InterfaceCallProbe.NullCall),
            nameof(InterfaceCallProbe.MetadataOnlySlot), nameof(InterfaceCallProbe.MixedClassCall), nameof(InterfaceCallProbe.ContractBoundary),
            nameof(InterfaceCallProbe.BoxBoundary), nameof(InterfaceCallProbe.MutuallyRecursive)];
        Func<int, int, int>[] oracles = [InterfaceCallProbe.Run, (_, v) => InterfaceCallProbe.OtherContract(v), (_, v) => InterfaceCallProbe.Diamond(v),
            (_, v) => InterfaceCallProbe.ReferenceFlow(v), (_, v) => InterfaceCallProbe.Recursive(v), (_, v) => InterfaceCallProbe.NullCall(v),
            (_, v) => InterfaceCallProbe.MetadataOnlySlot(v), (_, v) => InterfaceCallProbe.MixedClassCall(v), (_, v) => InterfaceCallProbe.ContractBoundary(v),
            (_, v) => InterfaceCallProbe.BoxBoundary(v), (_, v) => InterfaceCallProbe.MutuallyRecursive(v)];
        var programs = names.Select(ObjectInterfaceTranslationTests.Translate).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Layout(int i, System.Type type) => Type(ObjectTypeId.From(programs[i].Flow.Types.Types.Single(t =>
            t.Source.ContentHash == programs[i].Flow.Entry.ContentHash && t.Source.Token == type.MetadataToken).Source));
        string Symbol(int i, System.Type type, string method) => Method(IrMethodId.From(programs[i].Flow.Methods.Single(m => m.Source.Token == type.GetMethod(method)!.MetadataToken).Source));
        string contractDispatch = Method(IrMethodId.From(programs[8].InterfaceDispatch!.Declarations.Single().Source)) + "_dispatch";
        int voidCallOffset = programs[9].Flow.Methods.Single(m => m.Source.Token == typeof(InterfaceCallProbe).GetMethod(nameof(InterfaceCallProbe.ThroughBox))!.MetadataToken)
            .Blocks.SelectMany(b => b.Statements).OfType<IrStatement.Invoke>().First().ILOffset;
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        var cross = ObjectInterfaceTranslationTests.CrossImage(); var crossArtifact = ObjectCppEmitter.EmitProgram(cross);
        source.Append(crossArtifact.Text).Append(NativeObjectAdapters.Emit(cross, crossArtifact));
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <cstring>
            #include <iostream>
            #include <pthread.h>
            namespace {{Scope(3)}} {
            static Object* returned = nullptr;
            static unsigned observations = 0;
            static void observe(Context& c) noexcept {
                assert(valid_roots(c) && c.top != nullptr);
                Frame* entry = c.top; while (entry->previous != nullptr) entry = entry->previous;
                if (c.instance.count == 3) returned = c.instance.head;
                if (c.instance.count >= 3) {
                    assert(returned != nullptr && c.instance.owns(returned));
                    bool rooted = false; for (std::size_t i = 0; i < entry->count; ++i) rooted |= entry->slots[i] == returned;
                    assert(rooted); ++observations;
                    Object* first = c.instance.head; while (first->next != nullptr) first = first->next;
                    assert(first->type->reference_count == 1);
                    assert(*static_cast<Object**>(first->type->field_address(first, first->type->references[0])) == returned);
                }
            }
            }
            [[gnu::noinline]] static void roots_and_limits() {
                using namespace {{Scope(3)}};
                Instance heap; Context c(heap); c.observe = observe;
                assert({{Method(programs[3].Flow.Entry)}}(c, 19) == {{InterfaceCallProbe.ReferenceFlow(19)}} && c.pending == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0 && heap.count == 5 && observations > 0);
                assert(heap.head->type == &{{Layout(3, typeof(SecondFrameBox))}}_descriptor);
                Instance exact_heap; Context exact(exact_heap, c.peak);
                assert({{Method(programs[3].Flow.Entry)}}(exact, 19) == {{InterfaceCallProbe.ReferenceFlow(19)}} && exact.pending == 0);
                Instance short_heap; Context short_frame(short_heap, c.peak - 1);
                (void){{Method(programs[3].Flow.Entry)}}(short_frame, 19);
                assert(short_frame.pending == 9 && short_frame.used == 0 && short_frame.top == nullptr && short_frame.depth == 0);
                assert(short_frame.method != 0 && short_frame.image != nullptr);
                for (std::size_t limit = 0; limit < 5; ++limit) {
                    Instance limited; limited.object_limit = limit; Context fault(limited);
                    (void){{Method(programs[3].Flow.Entry)}}(fault, 19);
                    assert(fault.pending == 5 && fault.used == 0 && fault.top == nullptr && fault.depth == 0 && limited.count == limit);
                    assert(fault.method != 0 && fault.image != nullptr);
                    limited.object_limit = 4096; fault.pending = 0; fault.method = 0; fault.il = 0; fault.image = nullptr;
                    assert({{Method(programs[3].Flow.Entry)}}(fault, 7) == {{InterfaceCallProbe.ReferenceFlow(7)}} && fault.pending == 0);
                    assert(fault.used == 0 && fault.top == nullptr && fault.depth == 0);
                }
            }
            [[gnu::noinline]] static void ownership_and_contracts() {
                using namespace {{Scope(8)}};
                Instance heap; Context c(heap);
                Object* value = allocate<{{Layout(8, typeof(ExactInterfaceDiamond))}}>(c, {{Layout(8, typeof(ExactInterfaceDiamond))}}_descriptor);
                assert({{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(c, value, 7) == 107 && c.pending == 0);
                Instance foreign; Context wrong(foreign);
                (void){{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(wrong, value, 7);
                assert(wrong.pending == 6 && wrong.used == 0 && wrong.top == nullptr && wrong.depth == 0);
                assert(wrong.method == {{typeof(InterfaceCallProbe).GetMethod(nameof(InterfaceCallProbe.ThroughContract))!.MetadataToken}}u && wrong.il == 0);
                Context invalid(heap);
                (void){{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}), 7);
                assert(invalid.pending == 6 && invalid.used == 0 && invalid.top == nullptr);
                Object* unrelated = allocate<{{Type(programs[8].Flow.Types.ObjectBase)}}>(c, {{Type(programs[8].Flow.Types.ObjectBase)}}_descriptor);
                Context bad_type(heap);
                (void){{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(bad_type, unrelated, 7);
                assert(bad_type.pending == 6 && bad_type.used == 0 && bad_type.top == nullptr);
                Context null_receiver(heap);
                (void){{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(null_receiver, static_cast<Object*>(nullptr), 7);
                assert(null_receiver.pending == 4 && null_receiver.used == 0 && null_receiver.depth == 0);
                static const Descriptor forged = {{Layout(8, typeof(ExactInterfaceDiamond))}}_descriptor;
                Object* missing = allocate<{{Layout(8, typeof(ExactInterfaceDiamond))}}>(c, forged);
                Context bad_descriptor(heap);
                (void){{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(bad_descriptor, missing, 7);
                assert(bad_descriptor.pending == 6 && bad_descriptor.used == 0 && bad_descriptor.top == nullptr);
                assert({{contractDispatch}}(&forged) == nullptr);
                const Descriptor* original = value->type;
                value->type = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                Context unreadable_descriptor(heap);
                (void){{Symbol(8, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughContract))}}(unreadable_descriptor, value, 7);
                assert(unreadable_descriptor.pending == 6 && unreadable_descriptor.used == 0 && unreadable_descriptor.top == nullptr);
                value->type = original;
            }
            [[gnu::noinline]] static void void_null_call() {
                using namespace {{Scope(9)}};
                Instance heap; Context c(heap);
                (void){{Symbol(9, typeof(InterfaceCallProbe), nameof(InterfaceCallProbe.ThroughBox))}}(c, static_cast<Object*>(nullptr), 7);
                assert(c.pending == 4 && c.used == 0 && c.top == nullptr && c.depth == 0);
                assert(c.method == {{typeof(InterfaceCallProbe).GetMethod(nameof(InterfaceCallProbe.ThroughBox))!.MetadataToken}}u);
                assert(c.il == {{voidCallOffset}}u);
            }
            static void* recursion(void*) noexcept {
                #if defined(__APPLE__)
                const auto size = pthread_get_stacksize_np(pthread_self());
                assert(size >= 256u * 1024u && size <= 320u * 1024u);
                #endif
                using namespace {{Scope(4)}};
                Instance heap; Context c(heap);
                assert({{Method(programs[4].Flow.Entry)}}(c, 62) == 62 && c.pending == 0 && c.used == 0);
                (void){{Method(programs[4].Flow.Entry)}}(c, 1000);
                assert(c.pending == 8 && c.method == {{typeof(FrameRecursion).GetMethod(nameof(FrameRecursion.Count))!.MetadataToken}}u && c.il == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                c.pending = 0; c.method = 0; c.il = 0; c.image = nullptr;
                assert({{Method(programs[4].Flow.Entry)}}(c, 7) == 7 && c.pending == 0 && c.used == 0);
                return nullptr;
            }
            [[gnu::noinline]] static void mutual_limits() {
                using namespace {{Scope(10)}};
                Instance heap; Context c(heap);
                assert({{Method(programs[10].Flow.Entry)}}(c, 19) == 19 && c.pending == 0 && c.used == 0);
                (void){{Method(programs[10].Flow.Entry)}}(c, 1000);
                assert(c.pending == 8 && c.used == 0 && c.top == nullptr && c.depth == 0);
                c.pending = 0; c.method = 0; c.il = 0; c.image = nullptr;
                assert({{Method(programs[10].Flow.Entry)}}(c, 7) == 7 && c.pending == 0 && c.used == 0);
            }
            [[gnu::noinline]] static void cross_image() {
                using namespace {{crossArtifact.EntryPoint[..^4] + "_closure"}};
                Instance heap; Context c(heap);
                assert({{Method(cross.Flow.Entry)}}(c) == 48 && c.pending == 0);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                std::int32_t result = 0; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                assert({{crossArtifact.EntryPoint}}(&result, &method, &il, &image, nullptr, 0) == 0 && result == 48);
            }
            [[gnu::noinline]] static void membership() {
            """);
        // Independent authored reflection is a test oracle only. Compare every included class/contract pair.
        foreach (int i in Enumerable.Range(0, programs.Length))
            foreach (var contract in programs[i].Flow.Types.Interfaces)
                foreach (var layout in programs[i].Flow.Types.Types)
                {
                    var receiver = ObjectTypeId.From(layout.Source);
                    var oracleClass = receiver == programs[i].Flow.Types.ObjectBase ? typeof(object)
                        : typeof(InterfaceCallProbe).Assembly.GetTypes().Single(t => t.MetadataToken == receiver.Token);
                    var oracleContract = typeof(InterfaceCallProbe).Assembly.GetTypes().Single(t => t.MetadataToken == contract.Source.Token);
                    source.Append("assert(").Append(Scope(i)).Append("::").Append(Type(ObjectTypeId.From(contract.Source))).Append("_implements(&")
                        .Append(Scope(i)).Append("::").Append(Type(receiver)).Append("_descriptor) == ")
                        .Append(oracleContract.IsAssignableFrom(oracleClass) ? "true" : "false").Append(");\n");
                }
        source.Append("}\nstruct Outcome { std::int32_t value = -123456789; std::uint32_t status = 0, method = 0, il = 0; };\n");
        for (int i = 0; i < programs.Length; i++)
        {
            string args = i == 0 ? "kind, input" : "input";
            string packed = i == 0 ? "kind, input" : "input";
            source.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static Outcome outcome_{{i}}([[maybe_unused]] int kind, int input) {
                    Outcome outcome;
                    using namespace {{Scope(i)}}; Instance heap; Context c(heap);
                    const auto returned = {{Method(programs[i].Flow.Entry)}}(c, {{args}});
                    assert(c.used == 0 && c.top == nullptr && c.depth == 0);
                    outcome.status = c.pending; outcome.method = c.method; outcome.il = c.il; if (outcome.status == 0) outcome.value = returned;
                    const std::int32_t parameters[] = { {{packed}} }; std::int32_t boundary = -123456789;
                    std::uint32_t bm = 0, bi = 0; const char* bh = nullptr;
                    assert({{artifacts[i].EntryPoint}}(&boundary, &bm, &bi, &bh, parameters, {{(i == 0 ? 2 : 1)}}) == outcome.status);
                    assert(boundary == outcome.value && bm == outcome.method && bi == outcome.il && ((bh == nullptr && c.image == nullptr)
                        || (bh != nullptr && c.image != nullptr && std::strcmp(bh, c.image) == 0)));
                    return outcome; }
                """);
        }
        source.Append("int main() { roots_and_limits(); ownership_and_contracts(); void_null_call(); mutual_limits(); membership(); cross_image();\n")
            .Append("pthread_attr_t attributes; assert(pthread_attr_init(&attributes) == 0); assert(pthread_attr_setstacksize(&attributes, 256u * 1024u) == 0);\n")
            .Append("pthread_t worker; assert(pthread_create(&worker, &attributes, recursion, nullptr) == 0);\n")
            .Append("assert(pthread_join(worker, nullptr) == 0 && pthread_attr_destroy(&attributes) == 0);\n")
            .Append("int which = 0, kind = 0, input = 0; while (std::cin >> which >> kind >> input) { Outcome outcome; switch (which) {\n");
        for (int i = 0; i < programs.Length; i++) source.Append(CultureInfo.InvariantCulture, $"case {i}: outcome = outcome_{i}(kind, input); break;\n");
        source.Append("default: return 2; } std::cout << outcome.status << ' ' << outcome.value << ' ' << outcome.method << ' ' << outcome.il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("interface-native"), cpp = Path.Combine(directory, "interface.cpp"), executable = Path.Combine(directory, "interface");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler,
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-pthread",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var measure = await NativeProcess.Run(compiler,
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-pthread", optimized ? "-O2" : "-O0", "-fstack-usage", "-Werror",
                "-Wframe-larger-than=2048", "-c", cpp, "-o", Path.Combine(directory, "measured.o")], directory);
        Assert.True(measure.ExitCode == 0, measure.Stderr);
        var rows = File.ReadAllLines(Path.Combine(directory, "measured.su")).Select(line => line.Split('\t')).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => { Assert.Equal("static", row[^1]); Assert.InRange(int.Parse(row[^2], CultureInfo.InvariantCulture), 0, 2048); });
        output.WriteLine($"O{(optimized ? 2 : 0)}: {rows.Length} functions measured; maximum {rows.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} stack bytes.");
        var inputs = new StringBuilder(); var expected = new List<string>();
        int[] edges = [int.MinValue, -65536, -1, 0, 1, 19, 65536, int.MaxValue];
        for (int which = 0; which < programs.Length; which++)
            foreach (int kind in which == 0 ? Enumerable.Range(0, 12) : new[] { 0 })
                foreach (int input in which is 4 or 10 ? new[] { 0, 1, 7, 19, 31 } : edges)
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
        output.WriteLine($"{expected.Count} .NET/native interface outcomes with exact origins, roots, budgets, ownership and recursion at O{(optimized ? 2 : 0)}.");
    }
}
