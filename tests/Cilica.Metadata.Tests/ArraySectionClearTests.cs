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
public sealed class ArraySectionClearTests
{
    private static readonly string[] Names=["Ints","Widths","References","Values","Null","Order","Field","Empty","Lifetime","LifetimeValues"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySectionClearProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Sections_faults_and_argument_order_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} from b in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Names.Take(8).Select((name,i)=>(name,programs[i],typeof(ArraySectionClearProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Fact]
    public async Task Clear_requires_public_and_bridge_grants_and_rejects_unproven_array_views()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var input=resolver.OpenInput(typeof(ArraySectionClearProbe).Assembly.Location).Value!;
        var root=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySectionClearProbe).GetMethod("Ints")!.MetadataToken);
        foreach(var capability in new[]{LibraryIntrinsicCapability.ArraySectionClear,LibraryIntrinsicCapability.ArraySectionClearBridge})
        {
            var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==capability));
            var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=artifact.Grants.Remove(grant)}]});
            using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,root,implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(string name in new[]{"GeneralAlias","JoinedArguments"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySectionClearProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("typed vector producer",StringComparison.Ordinal));
        }
        foreach(string name in new[]{"NarrowByte","NarrowChar","NarrowBoolean"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySectionClearProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Narrow memory operands",StringComparison.Ordinal));
        }
        var b=new ResolutionAssemblyBuilder(new AssemblyIdentity("ExternalManagedClear",new Version(1,0,0,0)));
        var owner=b.TypeReference("Cilica.Runtime","ArraySections`1",b.Reference(implementation.Identity));
        byte code=checked((byte)((MetadataTokens.GetRowNumber(owner)<<2)|1));var spec=b.Specification(0x15,0x12,code,1,8);
        var call=b.Member(spec,"ClearSection",0,3,1,0x1d,0x13,0,8,8);
        b.Type("Example","ExternalClear");var method=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldnull);il.LoadConstantI4(0);il.LoadConstantI4(0);il.Call(call);il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);});
        var external=scope.OpenInput(files.Write("external-clear.dll",b.Build())).Value!;
        var direct=ValueTranslator.TranslateIntegerObjects(scope,external,method,implementation);
        Assert.True(direct.Success,string.Join("; ",direct.Diagnostics));
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Clear_releases_references_while_the_array_stays_rooted(bool optimized, bool valueElements)
    {
        var program = (await Programs.Value)[valueElements ? 9 : 8];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name)
        {
            var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.ArraySectionClearProbe::" + name + (valueElements ? "Values" : ""));
            return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}";
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* array = nullptr; Frame frame; RootScope roots(c, frame, &array, 1);
                array = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && array != nullptr && collect(c).status == MarkStatus::complete);
                auto before = heap.count;
                LocationArgument args[1]; args[0].reference = array;
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 1);
                {{Symbol("ClearTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 1);
                {{Symbol("ClearAllTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == before - 3);
                array = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1 && heap.emergency_exception != nullptr);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("array-gc"), cpp = Path.Combine(directory, "array.cpp"), executable = Path.Combine(directory, "array");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
