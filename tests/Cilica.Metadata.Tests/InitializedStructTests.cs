using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InitializedStructTests
{
    private static readonly string[] Names = ["Numeric", "Closed", "Reentrant", "Empty", "References", "Failure"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InitializedStructProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Struct_initializers_preserve_storage_recursion_roots_and_failures(bool optimized, int group)
    {
        var programs = await Programs.Value;
        int start = group < 2 ? group * 2 : group + 2;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(InitializedStructProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(start).Take(group < 2 ? 2 : 1).ToArray(),
            expectedStaticRoots: group == 2 ? 1 : 0, persistentRoots: group == 3);
    }
    [Fact]
    public async Task Static_fields_never_enter_inline_layouts_and_closed_initializers_remain_distinct()
    {
        var programs = await Programs.Value;
        var numeric = programs[0].Flow.Flow.Flow;
        var point = Assert.Single(numeric.Types.Types.Where(t => t.Source.Name.EndsWith("InitializedPoint", StringComparison.Ordinal)));
        Assert.Equal(2, point.Fields.Length);
        Assert.DoesNotContain(point.Fields, f => (f.Attributes & System.Reflection.FieldAttributes.Static) != 0);
        Assert.Single(numeric.Types.References.Statics);
        var closed = programs[1];
        var initializers = closed.Flow.Bindings.Where(b => b.TypeInitializerName is not null).ToArray();
        Assert.Equal(2, initializers.Length); Assert.NotEqual(initializers[0].Owner, initializers[1].Owner);
        var empty = Assert.Single(programs[3].Flow.Flow.Flow.Types.Types.Where(t => t.Source.Name.EndsWith("InitializedEmpty", StringComparison.Ordinal)));
        Assert.Empty(empty.Fields);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Initialized_struct_reference_storage_is_isolated_and_released(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(InitializedStructProbe), "Persistent"))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                LocationArgument args[2]; args[0].bits = 10; args[1].bits = 2;
                assert({{symbol}}(c, args) == 49 && c.pending == 0);
                assert({{symbol}}(c, args) == 51 && c.pending == 0);
                Context again(heap);
                assert({{symbol}}(again, args) == 53 && again.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                Instance other; other.collect_before_allocation = true; Context d(other);
                args[0].bits = 20; args[1].bits = 3;
                assert({{symbol}}(d, args) == 60 && d.pending == 0);
                assert(collect(d).status == MarkStatus::complete && other.count == 2);
                std::size_t references = 0;
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    if (heap.statics[i].reference != nullptr) {
                        ++references;
                        assert(heap.statics[i].reference != other.statics[i].reference);
                    }
                    heap.statics[i].reference = nullptr;
                }
                assert(references == 1);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(collect(d).status == MarkStatus::complete && other.count == 2);
                assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr && c.depth == 0);
                assert(d.used == 0 && d.top == nullptr && d.borrows == nullptr && d.depth == 0);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("initialized-struct-runtime");
        string cpp = Path.Combine(directory, "test.cpp"), exe = Path.Combine(directory, "test");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData("UnsupportedStrict", "strict type")]
    [InlineData("UnsupportedAddress", "ldsflda")]
    public void Strict_initialization_and_static_addresses_stay_diagnostic(string name, string diagnostic)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(InitializedStructProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
}
