using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ConstrainedEnumTextTests
{
    private static readonly string[] Names=["Direct","Empty","Identity","GenericCall","Storage","Faults","CacheSeparation","Lowercase"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ConstrainedEnumTextProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    public async Task Constrained_text_and_cached_identity_match_dotnet(bool optimized,int index)
    {
        var programs=await Programs.Value;
        int[] values=[0,1,2,3,4,7,8,9,17,127,255,256,299,300,301,-1,-2,int.MinValue,int.MaxValue];
        var pairs=(from a in values from b in new[]{0,1,-1} select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[index],programs[index],typeof(ConstrainedEnumTextProbe).GetMethod(Names[index])!.CreateDelegate<Func<int,int,int>>())],pairs,
            expectedStaticRoots:index==1?1:index==6?14:10);
    }
    [Fact]
    public async Task Grants_and_supported_enum_shapes_are_required()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions {Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var input=resolver.OpenInput(typeof(ConstrainedEnumTextProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ConstrainedEnumTextProbe).GetMethod(name)!.MetadataToken);
        foreach(var grant in artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.EnumFormatting && g.Member!="Bits"))
        {
            var narrowed=manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]};
            var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Direct"),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==Cilica.Metadata.DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(string name in new[]{"UnsupportedFlags","UnsupportedAliases","UnsupportedWide","UnsupportedBoxed","UnsupportedArrayAddress"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method(name),implementation);
            Assert.False(result.Success,name);Assert.NotEmpty(result.Diagnostics);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Partial_name_cache_allocation_recovers_without_retaining_partial_arrays(bool optimized)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string symbol="cilica_m_"+flow.Entry.ContentHash+"_"+flow.Entry.Token.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
        int expected=ConstrainedEnumTextProbe.Direct(0,17);
        var source=artifact.Text+$$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            int main() {
                std::size_t failed=0, succeeded=0;
                for (std::size_t quota=0; quota<=4096; quota+=32) {
                    Instance heap; heap.collect_before_allocation=true; Context first(heap);
                    LocationArgument args[2]; args[0].bits=0; args[1].bits=17;
                    heap.limit=quota;
                    auto result={{symbol}}(first,args);
                    if (first.pending != 0) ++failed;
                    else { ++succeeded; assert(result=={{expected}}); }
                    assert(first.used==0 && first.top==nullptr && first.borrows==nullptr && first.depth==0);
                    heap.limit=1048576;
                    Context retry(heap);
                    assert({{symbol}}(retry,args)=={{expected}} && retry.pending==0 && retry.exception==nullptr);
                    assert(retry.used==0 && retry.top==nullptr && retry.borrows==nullptr && retry.depth==0);
                    assert(collect(retry).status==MarkStatus::complete && heap.count==11);
                }
                assert(failed!=0 && succeeded!=0);
            }
            """;
        using var files=new ResolverFiles();string directory=files.DirectoryPath("enum-cache"),cpp=Path.Combine(directory,"cache.cpp"),exe=Path.Combine(directory,"cache");
        File.WriteAllText(cpp,source);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",
            ["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);var run=await NativeProcess.Run(exe,[],directory);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Hidden_enum_generic_text_preserves_lexical_access(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(ConstrainedEnumTextProbe),["Hidden"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Hidden",programs[0],ConstrainedEnumTextProbe.Hidden)],expectedStaticRoots:3);
    }

}
