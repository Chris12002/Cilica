using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArraySectionCopyTests
{
    private static readonly string[] Names=["Whole","Sections","Bounds","Overlap","LengthOrder","Widths","ReferenceFaults","ReferenceShape","Values","Order","Lifetime","LifetimeValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySectionCopyProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)]
    public async Task Sections_faults_and_argument_order_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} from b in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Names.Take(10).Select((name,i)=>(name,programs[i],typeof(ArraySectionCopyProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Generic_constructor_field_copy_matches_dotnet(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySectionCopyProbe),["Fields"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Fields",programs[0],ArraySectionCopyProbe.Fields)]);
    }
    [Fact]
    public async Task Copy_requires_exact_grants_and_rejects_unproven_or_cross_element_views()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var input=resolver.OpenInput(typeof(ArraySectionCopyProbe).Assembly.Location).Value!;
        var root=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySectionCopyProbe).GetMethod("Whole")!.MetadataToken);
        foreach(var grant in artifact.Grants.Where(g=>g.Capability is LibraryIntrinsicCapability.ArraySectionCopy or LibraryIntrinsicCapability.ArraySectionCopyBridge or LibraryIntrinsicCapability.ArrayCopyStore or LibraryIntrinsicCapability.ArrayCopyCompatible))
        {
            var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=artifact.Grants.Remove(grant)}]});
            using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,root,implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(string name in new[]{"GeneralAlias","Joined","Widening","Boxing"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySectionCopyProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(name is "Widening" or "Boxing" ? "same declared vector element type" : "typed vector producers",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Copy_transfers_roots_and_releases_overwritten_references(bool optimized, bool valueElements)
    {
        var program = (await Programs.Value)[valueElements ? 11 : 10];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ArraySectionCopyProbe::" + name + (valueElements ? "Values" : ""));
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
                arrays[1] = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && arrays[0] != nullptr && arrays[1] != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[2]; args[0].reference = arrays[0]; args[1].reference = arrays[1];
                {{Symbol("CopyTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 1);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 3);
                args[0].reference = arrays[1];
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 6);
                arrays[0] = nullptr; arrays[1] = nullptr; args[0].reference = nullptr; args[1].reference = nullptr;
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
