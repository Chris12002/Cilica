using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NativeBoxedInterfaceTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Receiver_packets_check_identity_bounds_and_preserve_suffixes(bool optimized, bool empty)
    {
        var result = IntegerObjectTranslationTests.Translate(empty ? "Empty" : "PayloadReturn", fixture: typeof(BoxedInterfaces));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var scalar = ValueScalarLowering.LowerExceptions(result.Program!);
        var artifact = ObjectCppEmitter.EmitExceptions(scalar);
        var value = scalar.Flow.Flow.Values.Types.Single(t => t.Source.Name.EndsWith(empty ? ".EmptyBoxCall" : ".BoxState", StringComparison.Ordinal));
        var type = ObjectTypeId.From(value.Source);
        string box = "box_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture);
        int width = ValueLayoutPlanner.Build(scalar.Flow.Flow.Values).Single(p => p.Type == type).Slots.Length;
        var source = new StringBuilder(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void run() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Object* root = nullptr; Frame frame; RootScope scope(c, frame, &root, 1);
                std::array<Argument, {{width}}> initial{};
                root = box_value(c, &{{box}}_descriptor, initial.data(), initial.size()).value; assert(root);
                Storage<std::array<LocationArgument, {{width + 3}}>> storage(c); assert(storage.value);
                auto* args = storage.value->data(); const auto capacity = storage.value->size();
                args[0].reference = root; args[1].wide_bits = 0xfedcba9876543210ull; args[2].bits = 197u;
                const auto used = c.used; const auto count = heap.count;
                assert(adapt_boxed_call(c, args, 3, capacity, nullptr, 0) == 0);
                assert(args[0].reference == root && args[1].wide_bits == 0xfedcba9876543210ull);
                assert(adapt_boxed_call(c, nullptr, 3, capacity, &{{box}}_descriptor, {{width}}) == 6);
                assert(adapt_boxed_call(c, args, 0, capacity, &{{box}}_descriptor, {{width}}) == 6);
                assert(adapt_boxed_call(c, args, capacity + 1, capacity, &{{box}}_descriptor, {{width}}) == 6);
                assert(adapt_boxed_call(c, args, 3, capacity, &{{box}}_descriptor, capacity) == 6);
                assert(adapt_boxed_call(c, args, 1, capacity, &{{box}}_descriptor, {{width + 1}}) == 6);
                assert(args[0].reference == root && args[1].wide_bits == 0xfedcba9876543210ull && args[2].bits == 197u);
                const Descriptor copied = {{box}}_descriptor;
                assert(adapt_boxed_call(c, args, 3, capacity, &copied, {{width}}) == 6);
                const auto* forgedType = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                assert(adapt_boxed_call(c, args, 3, capacity, forgedType, {{width}}) == 6);
                args[0].reference = nullptr;
                assert(adapt_boxed_call(c, args, 3, capacity, &{{box}}_descriptor, {{width}}) == 4);
                args[0].reference = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(adapt_boxed_call(c, args, 3, capacity, &{{box}}_descriptor, {{width}}) == 6);
                args[0].reference = root;
                assert(adapt_boxed_call(c, args, 3, capacity, &{{box}}_descriptor, {{width}}) == 0);
                assert(args[{{width}}].wide_bits == 0xfedcba9876543210ull && args[{{width + 1}}].bits == 197u);
                for (std::size_t i = 0; i < {{width}}u; ++i) {
                    assert(args[i].location.owner == root && args[i].location.slot == i);
                    assert(!args[i].location.read_only);
                }
                assert(c.used == used && heap.count == count && c.pending == 0 && c.borrows == nullptr);
            }
            int main() { run(); }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("boxed-interface-packets");
        string cpp = Path.Combine(directory, "probe.cpp"), executable = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
