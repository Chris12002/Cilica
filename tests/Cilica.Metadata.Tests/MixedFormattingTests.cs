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
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class MixedFormattingTests
{
    private static readonly string[] Names=["One","Two","Three","Strings","Positions","Callbacks","Faults","ArgumentOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(MixedFormattingProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Typed_text_mixed_values_and_triples_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in new[]{int.MinValue,-1,0,1,2,3,int.MaxValue} from b in Enumerable.Range(0,16) select(a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(MixedFormattingProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(),pairs);
    }
    [Fact]
    public async Task Object_aliases_joins_scalars_custom_formats_and_flags_remain_diagnostic()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(MixedFormattingProbe).Assembly.Location).Value!;
        foreach(string suffix in new[]{"Alias","StringAlias","Join","Scalar","Custom","Flags"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(MixedFormattingProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.ExecutionUnsupported);
        }
    }
    [Fact]
    public async Task Triple_format_and_string_renderer_require_exact_grants()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var triple=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.BoxedStringFormat && resolver.ReadMethodDeclarationInput(implementation,(MethodDefinitionHandle)MetadataTokens.EntityHandle(g.Token),new()).Value!.Signature.Parameters.Count==4));
        foreach(bool renderer in new[]{false,true}) {
            var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=[.. artifact.Grants.Where(g=>renderer?g.Capability!=LibraryIntrinsicCapability.BoxedTextRender:g!=triple)]}]});
            using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
            var input=scope.OpenInput(typeof(MixedFormattingProbe).Assembly.Location).Value!;
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(MixedFormattingProbe).GetMethod("Strings")!.MetadataToken),implementation);
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
        }
    }
}
