using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class WideStaticTests
{
    private static readonly string[] Names = ["Signed", "Unsigned", "Closed", "Faults", "Initialized"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(WideStaticProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Wide_statics_preserve_bits_and_initialization(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(WideStaticProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Fact]
    public async Task Detached_wide_static_packet_cannot_change_its_storage_width()
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var field = flow.Types.References.Statics.Single(f => f.Type == IrType.Int64);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with {
            Types = flow.Types with { References = flow.Types.References with {
                Statics = flow.Types.References.Statics.Replace(field, field with { Type = IrType.Int32 }) } } } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Wide_storage_persists_and_is_isolated_per_runtime(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(WideStaticProbe), "Persistent"))[0];
        var flow = program.Flow.Flow.Flow;
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var artifact = ObjectCppEmitter.EmitExceptions(scalar);
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance first; first.collect_before_allocation = true; Context a(first);
                Instance second; second.collect_before_allocation = true; Context b(second);
                LocationArgument args[2]; args[0].bits = 7; args[1].bits = 3;
                assert({{symbol}}(a, args) == 10 && a.pending == 0);
                assert({{symbol}}(a, args) == 13 && a.pending == 0);
                args[0].bits = 20; args[1].bits = 5;
                assert({{symbol}}(b, args) == 25 && b.pending == 0);
                Context again(first);
                assert({{symbol}}(again, args) == 18 && again.pending == 0);
                assert(collect(a).status == MarkStatus::complete && first.count == 1);
                assert(collect(b).status == MarkStatus::complete && second.count == 1);
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    assert(first.statics[i].reference == nullptr && second.statics[i].reference == nullptr);
                }
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("wide-statics"), cpp = Path.Combine(dir, "test.cpp"), exe = Path.Combine(dir, "test");
        File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
