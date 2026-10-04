using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeCollectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Collection_stress_preserves_dotnet_results_fault_origins_and_roots(bool optimized)
    {
        (Type Type, string Method, int[] Args)[] cases = [
            (typeof(TracingProbe), "Run", [21]),
            (typeof(ArrayProbe), "Rooted", [100, 19]),
            (typeof(ArrayProbe), "WidenAndNest", [3]),
            (typeof(ArrayProbe), "CheckedStore", [2, 0]),
            (typeof(ArrayProbe), "FaultOrder", [1, 0]),
            (typeof(StringProbe), "Rooted", [20]),
            (typeof(StringProbe), "FreshIdentity", []),
            (typeof(StringProbe), "ConcatCodeUnit", [2]),
            (typeof(StringProbe), "OrderedFault", [0]),
            (typeof(MixedDispatchProbe), "Allocating", [1, 7])
        ];
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(TracingProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var source = new StringBuilder();
        for (int i = 0; i < cases.Length; i++)
        {
            var test = cases[i]; var method = test.Type.GetMethod(test.Method)!;
            var translated = ObjectTranslator.Translate(resolver, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken), library);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var program = translated.Program!; var artifact = ObjectCppEmitter.EmitProgram(program);
            source.Append(artifact.Text).Append(NativeObjectAdapters.Emit(program, artifact));
            var (status, expected) = Oracle(method, test.Args);
            string root = "cilica_m_" + program.Flow.Entry.ContentHash + "_" + program.Flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            string args = string.Concat(test.Args.Select(a => ", " + a.ToString(CultureInfo.InvariantCulture)));
            source.Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                #include <cstring>
                [[gnu::noinline]] static void stress_{{i}}() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance baseline; Context ordinary(baseline);
                    const auto expected = {{root}}(ordinary{{args}});
                    assert(ordinary.pending == {{status}}u);
                    if (ordinary.pending == 0) assert(expected == {{expected}});
                    Instance heap; heap.collect_before_allocation = true; Context c(heap);
                    for (unsigned run = 0; run < 20; ++run) {
                        c.pending = 0; c.method = 0; c.il = 0; c.image = nullptr;
                        const auto actual = {{root}}(c{{args}});
                        assert(c.pending == ordinary.pending && c.method == ordinary.method && c.il == ordinary.il);
                        assert((c.image == nullptr && ordinary.image == nullptr) ||
                            (c.image != nullptr && ordinary.image != nullptr && std::strcmp(c.image, ordinary.image) == 0));
                        if (c.pending == 0) assert(actual == expected);
                        assert(c.top == nullptr && c.used == 0 && c.depth == 0);
                        assert(collect(c).status == MarkStatus::complete);
                        const auto live = mark_reachable(c);
                        assert(live.status == MarkStatus::complete && live.unreachable == 0 && live.reachable == heap.count);
                        std::size_t bytes = 0; for (Object* p = heap.head; p != nullptr; p = p->next) bytes += p->allocation_bytes;
                        assert(bytes == heap.bytes && !heap.marking);
                    }
                }
                """);
        }
        source.Append("int main() {\n");
        for (int i = 0; i < cases.Length; i++) source.Append(CultureInfo.InvariantCulture, $"stress_{i}();\n");
        source.Append("}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-collection");
        string cpp = Path.Combine(directory, "collection.cpp"), executable = Path.Combine(directory, "collection");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var runResult = await NativeProcess.Run(executable, [], directory);
        Assert.True(runResult.ExitCode == 0, runResult.Stdout + runResult.Stderr); Assert.Empty(runResult.Stderr);
    }

    private static (uint Status, int Value) Oracle(MethodInfo method, int[] args)
    {
        try { return (0, (int)method.Invoke(null, args.Cast<object>().ToArray())!); }
        catch (TargetInvocationException ex) when (ex.InnerException is DivideByZeroException) { return (1, 0); }
        catch (TargetInvocationException ex) when (ex.InnerException is ArrayTypeMismatchException) { return (11, 0); }
    }
}
