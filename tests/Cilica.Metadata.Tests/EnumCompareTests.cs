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
public sealed class EnumCompareTests
{
    private static readonly string[] Names=["Values","Null","Field","Evaluation","NullField","ArgumentFaults"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Proven_enum_comparisons_preserve_signed_order_null_and_evaluation(bool optimized,int group)
    {
        var names=Names.Skip(group*2).Take(2).ToArray();var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(EnumCompareProbe),names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,names.Select((name,i)=>(name,programs[i],typeof(EnumCompareProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
    [Fact]
    public async Task Unproven_targets_boxed_receivers_and_other_widths_remain_diagnostic()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(EnumCompareProbe).Assembly.Location).Value!;
        foreach(string suffix in new[]{"Different","Integer","Alias","Wide","Boxed","Constraint"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumCompareProbe).GetMethod("Unsupported"+suffix)!.MetadataToken),implementation);
            Assert.False(result.Success,suffix);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(suffix=="Wide"?"Int32 enum":suffix=="Constraint"?"constraint":"Enum.CompareTo",StringComparison.Ordinal));
        }
    }
    [Fact]
    public async Task CompareTo_requires_exact_library_grant()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var grant=Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.EnumCompareTo));
        var bytes=AssemblyResolver.SerializeLibraryBuildManifest(manifest with {Artifacts=[artifact with {Grants=artifact.Grants.Remove(grant)}]});
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input=scope.OpenInput(typeof(EnumCompareProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumCompareProbe).GetMethod("Values")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);
    }
}
