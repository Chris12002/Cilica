using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArraySegmentTests(ITestOutputHelper output)
{
    private static readonly string[] Names=["Whole","Bounds","NullOrder","Defaults","Copies","References","Values","Widths","Lifetime","LifetimeValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySegmentProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Constructors_views_and_copies_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} from b in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Names.Take(8).Select((name,i)=>(name,programs[i],typeof(ArraySegmentProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Existing_boxed_interface_count_path_preserves_copied_segment(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySegmentProbe),["InterfaceCounts"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("InterfaceCounts",programs[0],ArraySegmentProbe.InterfaceCounts)]);
    }
    [Fact]
    public async Task Unimplemented_segment_members_remain_diagnosed()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(ArraySegmentProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"IndexerGap","EqualityGap","InterfaceGap"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySegmentProbe).GetMethod(name)!.MetadataToken),implementation);
            output.WriteLine(name+": "+string.Join("; ",result.Diagnostics));Assert.False(result.Success);Assert.NotEmpty(result.Diagnostics);
        }
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Empty_segments_retain_whole_backing_arrays_until_last_copy_is_cleared(bool optimized, bool valueElements)
    {
        var program = (await Programs.Value)[valueElements ? 9 : 8];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ArraySegmentProbe::" + name + (valueElements ? "Values" : ""));
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* arrays[2] = {nullptr, nullptr}; Frame frame; RootScope roots(c, frame, arrays, 2);
                arrays[0] = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && arrays[0] != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[1]; args[0].reference = arrays[0];
                arrays[1] = {{Symbol("CopyTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before + 1);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before + 1);
                args[0].reference = arrays[1];
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 3);
                arrays[0] = nullptr; arrays[1] = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.emergency_exception != nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("array-gc"), cpp = Path.Combine(directory, "array.cpp"), executable = Path.Combine(directory, "array");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        NativeEvidence.Record(cpp, optimized);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        NativeEvidence.Record(cpp, optimized, executable);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
