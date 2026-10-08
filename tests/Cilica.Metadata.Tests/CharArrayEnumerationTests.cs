using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CharArrayEnumerationTests
{
    private static readonly string[] Names=["States","FreshAndNull","Queries","NullAndFaults","BoxedCurrent","Empty","AllUnits"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CharArrayEnumerationProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Cursor_and_query_semantics_match_dotnet(bool optimized,int group)
    {
        var p=await Programs.Value;
        int[] inputs=[0,1,2,3,4,-1,65,255,256,32768,0xd800,0xdfff,0xffff,0x10000,int.MinValue,int.MaxValue];
        var pairs=(from a in inputs from b in new[]{0,1,65,-1,0xffff} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Take(5).Select((name,i)=>
            (name,p[i],typeof(CharArrayEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Mutable_empty_cache_has_exact_closed_identity(bool optimized)
    {
        var p=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CharEmpty",p[5],CharArrayEnumerationProbe.Empty)],expectedStaticRoots:2);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Every_UTF16_unit_is_read_through_exact_generic_Current(bool optimized)
    {
        var p=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("CharUnits",p[6],CharArrayEnumerationProbe.AllUnits)],Enumerable.Range(0,1024).Select(i=>(i*64,0)).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Former_generic_enumeration_blocker_executes(bool optimized)
    {
        var p=await CoreLibraryBuildTests.TranslateFixtures(typeof(NarrowArrayMetadataProbe),"UnsupportedEnumeration");
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("FormerCharEnumeration",p[0],NarrowArrayMetadataProbe.UnsupportedEnumeration)]);
    }
    [Theory]
    [InlineData("UnsupportedByte")] [InlineData("UnsupportedBool")] [InlineData("UnsupportedSet")] [InlineData("UnsupportedCharCopy")]
    public async Task Other_narrow_operations_remain_rejected(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharArrayEnumerationProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharArrayEnumerationProbe).GetMethod(name)!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Narrow vector interfaces",StringComparison.Ordinal));
    }
    [Fact]
    public async Task Full_owned_decoder_shape_records_the_next_missing_API()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CharArrayEnumerationProbe).Assembly.Location).Value!;
        var method=(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharArrayEnumerationProbe).GetMethod("DecoderShape")!.MetadataToken);
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL3010" && d.Message.Contains("ToUpperInvariant",StringComparison.Ordinal));
    }
    [Fact]
    public async Task Detached_Char_enumeration_packets_require_exact_receivers_and_results()
    {
        var program=(await Programs.Value)[5];
        foreach(var plan in new[]{program.Flow.InterfaceDispatch!,ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch!})
        {
            var helper=plan.Methods.Single(m=>m.ArrayHelper && m.Name=="Enumerate" && m.Parameters[0].Class!.ArrayElement!.Kind==ObjectArrayElementKind.Char);
            var integer=plan.Methods.Single(m=>m.ArrayHelper && m.Name=="Enumerate" && m.Parameters[0].Class!.ArrayElement!.Kind==ObjectArrayElementKind.Int32);
            foreach(var changed in new[]{helper with {ReturnType=integer.ReturnType},helper with {Parameters=integer.Parameters},
                helper with {Name="Get",Parameters=helper.Parameters.Add(IrType.Int32),ReturnType=IrType.Int32},
                helper with {ArrayHelper=false},helper with {ReturnType=IrType.Reference(plan.Types.ObjectBase)}})
                Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(plan with {Methods=plan.Methods.Replace(helper,changed)}));
        }
    }
    [Fact]
    public async Task Exact_grant_remains_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(CharArrayEnumerationProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CharArrayEnumerationProbe).GetMethod(name)!.MetadataToken);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.ArrayInterfaceEnumerate)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("States"), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);

    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Enumerators_retain_nonempty_arrays_but_empty_cache_does_not(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CharArrayEnumerationProbe), ["Tracked"]))[0];
        var flow = program.Flow.Flow.Flow;
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(string name) { var method = flow.Methods.Single(m => m.Source.Name == "Cilica.Fixtures.Execution.CharArrayEnumerationProbe::" + name); return $"cilica_m_{method.Source.ContentHash}_{method.Source.Token:x8}"; }
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* enumerator = nullptr; Frame frame; RootScope roots(c, frame, &enumerator, 1);
                enumerator = {{Symbol("CreateTracked")}}(c, nullptr);
                assert(c.pending == 0 && enumerator != nullptr);
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                LocationArgument args[1]; args[0].reference = enumerator;
                {{Symbol("EndTracked")}}(c, args);
                assert(c.pending == 0 && collect(c).status == MarkStatus::complete && heap.count == 3);
                enumerator = nullptr; args[0].reference = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            """;
        await RunNative(source, optimized);
        program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(CharArrayEnumerationProbe), ["Cache"]))[0]; flow = program.Flow.Flow.Flow;
        artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                Instance other; other.collect_before_allocation = true; Context d(other);
                LocationArgument args[2]; args[0].bits = 0; args[1].bits = 0;
                assert(cilica_m_{{flow.Entry.ContentHash}}_{{flow.Entry.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)}}(c, args) == 11 && c.pending == 0);
                assert(cilica_m_{{flow.Entry.ContentHash}}_{{flow.Entry.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)}}(d, args) == 11 && d.pending == 0);
                assert(static_field_count == 2);
                // Two closed empty enumerators and the emergency exception; no caller arrays survive.
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                for (std::size_t i = 0; i < static_field_count; ++i) {
                    assert(heap.statics[i].reference != nullptr && other.statics[i].reference != nullptr);
                    assert(heap.statics[i].reference != other.statics[i].reference);
                    heap.statics[i].reference = nullptr;
                }
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
                assert(collect(d).status == MarkStatus::complete && other.count == 3);
                assert(c.used == 0 && c.top == nullptr && c.borrows == nullptr);
            }
            """;
        await RunNative(source, optimized);
    }
    private static async Task RunNative(string source, bool optimized)
    {
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("array-enumerator-gc"), cpp = Path.Combine(directory, "enumerator.cpp"), executable = Path.Combine(directory, "enumerator");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        NativeEvidence.Record(cpp, optimized);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        NativeEvidence.Record(cpp, optimized, executable);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
