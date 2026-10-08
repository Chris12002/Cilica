using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class SortedDictionaryTests
{
    private static readonly string[] Names=["Ordering","Replacement","Duplicate","Copies","Interfaces","Empty","LiveValues","Faults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(SortedDictionaryProbe),Names));
    [Fact]
    public async Task Ordered_tree_and_cursor_programs_validate_at_both_stages()
    {foreach(var p in await Programs.Value){ManagedExceptionIrValidator.Validate(p);ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(p));}}
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Ordering_mutation_cursor_copies_and_faults_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        var pairs=(from a in Enumerable.Range(0,16).Concat(new[]{int.MinValue,int.MaxValue}) from b in new[]{0,3,-1} select(a,b)).ToArray();
        foreach(var i in Enumerable.Range(group*2,2))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(Names[i],programs[i],typeof(SortedDictionaryProbe).GetMethod(Names[i])!.CreateDelegate<Func<int,int,int>>())],pairs,expectedStaticRoots:Names[i]=="Empty"?3:1);
    }
    [Fact]
    public async Task Exact_ordinal_factory_grant_and_constructor_proof_are_required()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var bytes=await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken);
        var manifest=JsonSerializer.Deserialize<LibraryBuildManifest>(bytes,new JsonSerializerOptions{Converters={new JsonStringEnumConverter<LibraryIntrinsicCapability>()}})!;
        var artifact=Assert.Single(manifest.Artifacts);Assert.Single(artifact.Grants.Where(g=>g.Capability==LibraryIntrinsicCapability.SortedDictionaryOrdinalConstruction));
        var narrowed=manifest with {Artifacts=[artifact with {Grants=[..artifact.Grants.Where(g=>g.Capability!=LibraryIntrinsicCapability.SortedDictionaryOrdinalConstruction)]}]};
        var deniedBytes=AssemblyResolver.SerializeLibraryBuildManifest(narrowed);var input=resolver.OpenInput(typeof(SortedDictionaryProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name)=>(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(SortedDictionaryProbe).GetMethod(name)!.MetadataToken);
        using(var denied=resolver.CreateTrustedLibraryCompilationScope(plan,deniedBytes,Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!){var result=ValueTranslator.TranslateIntegerObjects(denied,input,Method("Ordering"),implementation);Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id==DiagnosticIds.LibraryIntrinsicDenied);}
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        foreach(var name in new[]{"UnsupportedCursorTypeof","UnsupportedInteger","UnsupportedDefault","UnsupportedNull","UnsupportedIgnoreCase","UnsupportedAlias","UnsupportedRemove","UnsupportedSet"})
        {var result=ValueTranslator.TranslateIntegerObjects(scope,input,Method(name),implementation);Assert.False(result.Success,name);Assert.NotEmpty(result.Diagnostics);}
    }
}
