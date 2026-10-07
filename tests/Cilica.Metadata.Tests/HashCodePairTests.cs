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
public sealed class HashCodePairTests
{
    private static readonly string[] Names=["Stable","NullAndText","Wide","CallbackCopies","ReferenceCallbacks","Dictionary"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(HashCodePairProbe),Names));
    [Fact]
    public async Task Pair_combine_probes_translate() => Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Pair_hash_contracts_match_dotnet(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(HashCodePairProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Integer_hash_matches_pinned_algorithm_with_Cilica_seed(bool optimized)
    {
        var programs=await CoreLibraryBuildTests.TranslateFixtures(typeof(HashCodePairProbe),["Raw"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Raw",programs[0],FixedSeed)]);
    }
    // Independent local model of pinned xxHash32, with the documented Cilica seed.
    // The runtime-randomized .NET numeric hash is deliberately not the oracle here.
    private static int FixedSeed(int a,int b)
    {
        static uint Queue(uint h,uint v) => System.Numerics.BitOperations.RotateLeft(unchecked(h+v*3266489917u),17)*668265263u;
        uint hash=Queue(Queue(374761393u+8u,(uint)a),(uint)b);
        hash=(hash^(hash>>15))*2246822519u;
        hash=(hash^(hash>>13))*3266489917u;
        return unchecked((int)(hash^(hash>>16)));
    }
    [Fact]
    public async Task Other_arities_nullable_and_builder_remain_diagnostic()
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(HashCodePairProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"UnsupportedArity","UnsupportedNullable","UnsupportedBuilder","UnsupportedVector"})
        {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(HashCodePairProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success,name); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
            if(name=="UnsupportedVector") Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Default key equality/hashing for array identity receivers",StringComparison.Ordinal));
        }
    }
}
