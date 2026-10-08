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
public sealed class DefaultListSortTests
{
    [Theory]
    [InlineData(false,"Signed32")] [InlineData(true,"Signed32")]
    [InlineData(false,"Signed64")] [InlineData(true,"Signed64")]
    [InlineData(false,"ShortLists")] [InlineData(true,"ShortLists")]
    [InlineData(false,"Null")] [InlineData(true,"Null")]
    [InlineData(false,"Repeat")] [InlineData(true,"Repeat")]
    public async Task Numeric_order_version_and_cleanup_match_dotnet(bool optimized,string name)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(DefaultListSortProbe),[name]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,program,typeof(DefaultListSortProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Former_default_sort_gap_matches_dotnet(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(ListSortProbe),"UnsupportedDefault"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("FormerDefault",program,ListSortProbe.UnsupportedDefault)]);
    }
    [Theory]
    [InlineData("Unsigned32")] [InlineData("Unsigned64")]
    [InlineData("UnsupportedText")] [InlineData("UnsupportedEnum")] [InlineData("UnsupportedNullable")] [InlineData("UnsupportedSection")]
    public async Task Other_default_comparers_and_section_overload_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(DefaultListSortProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DefaultListSortProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Null(result.Program);Assert.NotEmpty(result.Diagnostics);
        if(name=="Unsigned32")Assert.Contains(result.Diagnostics,d=>d.Message.Contains("unsigned/narrow parameters",StringComparison.Ordinal));
        if(name=="Unsigned64")Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Composed vector elements",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Integer_compare_grant_is_required_without_a_comparer_factory(bool removeCompare)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);var capability=removeCompare?LibraryIntrinsicCapability.IntegerComparerCompare:LibraryIntrinsicCapability.IntegerComparerCreate;
        var narrowed=manifest with{Artifacts=[artifact with{Grants=[..artifact.Grants.Where(g=>g.Capability!=capability)]}]};
        var changed=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,changed,Convert.ToHexStringLower(SHA256.HashData(changed))).Value!;
        var input=scope.OpenInput(typeof(DefaultListSortProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DefaultListSortProbe).GetMethod("Signed64")!.MetadataToken),implementation);
        if(removeCompare){Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);}
        else Assert.True(result.Success,string.Join("\n",result.Diagnostics));
    }
}
