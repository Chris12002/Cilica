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
public sealed class DictionaryCopyTests
{
    private static readonly string[] Names = ["Keys", "Values", "Pairs", "KeyFaults", "ValueFaults", "PairFaults", "CovariantValues", "CovariantKeys", "InlineValues", "ReferenceIdentity", "Callbacks", "Construction", "Cleared"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryCopyProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)] [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Dictionary_copies_preserve_order_faults_and_identity(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(DictionaryCopyProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Exact_copy_capacity_boundaries_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("KeyFaults",programs[3],DictionaryCopyProbe.KeyFaults),("ValueFaults",programs[4],DictionaryCopyProbe.ValueFaults),("PairFaults",programs[5],DictionaryCopyProbe.PairFaults)],
            (from index in new[] { -1,0,1,2,3,int.MaxValue } from mode in new[] { -1,0,1 } select (index,mode)).ToArray());
    }
    [Fact]
    public async Task Nongeneric_copy_remains_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(DictionaryCopyProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DictionaryCopyProbe).GetMethod("UnsupportedLegacy")!.MetadataToken);
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation).Success);
    }
}
