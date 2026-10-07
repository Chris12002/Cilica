using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class Utf8Tests
{
    private static readonly string[] Names=["Units","ByteAt","PairByte","Empty","Singleton","Independent","Repeated","Long","Fault","CastFault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(Utf8Probe),Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Default_utf8_preserves_code_units_fallbacks_buffers_and_fault_order(bool optimized)
    {
        var programs=await Programs.Value;
        int[] units=[0,1,0x7f,0x80,0x7ff,0x800,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xfeff,0xfffe,0xffff,-1];
        var pairs=(from a in units from b in units select (a,b)).ToArray();
        foreach(var batch in Names.Select((name,i)=>(name,programs[i],typeof(Utf8Probe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Chunk(3))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,batch,pairs:pairs,persistentRoots:true);
    }
    [Fact]
    public async Task Unsupported_encoding_contracts_remain_actionable_diagnostics()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(Utf8Probe).Assembly.Location).Value!;
        foreach(string name in new[]{"UnsupportedDecoder","UnsupportedOtherEncoding","UnsupportedSection","UnsupportedConfigurable"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(Utf8Probe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("No implementation method matches",StringComparison.Ordinal)||d.Message.Contains("has no library mapping",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Utf8_singleton_is_instance_owned_and_byte_allocation_failure_recovers(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(Utf8Probe), ["Budget"]))[0]; var flow = program.Flow.Flow.Flow;
        Assert.Single(flow.Types.References.Statics);
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol = $"cilica_m_{flow.Entry.ContentHash}_{flow.Entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Instance other; other.collect_before_allocation = true; Context d(other);
                assert(prepare_exceptions(c) == 0 && prepare_exceptions(d) == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.head == heap.emergency_exception);
                assert(collect(d).status == MarkStatus::complete && other.count == 1 && other.head == other.emergency_exception);
                LocationArgument args[2]; args[0].bits = 65; args[1].bits = 66;
                for (int i = 0; i < 3; i++) {
                    assert({{symbol}}(c, args) == 4 && c.pending == 0);
                    assert({{symbol}}(d, args) == 4 && d.pending == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                    assert(collect(d).status == MarkStatus::complete && other.count == 3);
                    assert(heap.statics[0].reference != nullptr && other.statics[0].reference != nullptr);
                    assert(heap.statics[0].reference != other.statics[0].reference);
                    assert(heap.owns(heap.statics[0].reference) && !other.owns(heap.statics[0].reference));
                    assert(other.owns(other.statics[0].reference) && !heap.owns(other.statics[0].reference));
                    assert(c.used == 0 && d.used == 0 && c.borrows == nullptr && d.borrows == nullptr);
                }
                heap.object_limit = heap.count;
                (void){{symbol}}(c, args);
                assert(c.pending == 5 && c.exception == heap.emergency_exception);
                assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr && c.depth == 0);
                c.pending = 0; c.exception = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                heap.object_limit++;
                assert({{symbol}}(c, args) == 4 && c.pending == 0);
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                heap.statics[0].reference = nullptr; other.statics[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                assert(collect(d).status == MarkStatus::complete && other.count == 2);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("utf8-budget"), cpp = Path.Combine(directory, "utf8.cpp"), executable = Path.Combine(directory, "utf8");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
