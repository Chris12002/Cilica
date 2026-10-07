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
public sealed class ArraySearchTests
{
    private static readonly string[] Names=["Integers","Wide","Enums","Strings","Faults","Values","Allocating","Throwing"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ArraySearchProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Whole_vector_search_equality_faults_and_rooting_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(ArraySearchProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Fact]
    public async Task Unsupported_equality_and_range_overloads_remain_diagnostic()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(ArraySearchProbe).Assembly.Location).Value!;
        foreach(string suffix in new[]{"Nullable","Start","Range","Characters","Bytes"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySearchProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix);
            Assert.Contains(result.Diagnostics,d=>suffix=="Nullable" ? d.Message.Contains("Nullable array search",StringComparison.Ordinal)
                : suffix is "Characters" or "Bytes" ? d.Message.Contains("Narrow local addresses require width-preserving borrow storage",StringComparison.Ordinal)
                : d.Message.Contains("No implementation method matches",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(LibraryIntrinsicCapability.ClosedArraySearch)] [InlineData(LibraryIntrinsicCapability.ClosedKeyEqual)]
    public async Task Public_search_retains_private_search_and_equality_grants(LibraryIntrinsicCapability capability)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==capability));
        var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with{Artifacts=[artifact with{Grants=artifact.Grants.Remove(grant)}]});
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input=scope.OpenInput(typeof(ArraySearchProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArraySearchProbe).GetMethod("Integers")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
}
