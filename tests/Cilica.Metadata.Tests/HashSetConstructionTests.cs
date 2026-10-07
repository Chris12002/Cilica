using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HashSetConstructionTests
{
    private static readonly string[] Names = ["Arrays", "WideValues", "Strings", "EmptyNull", "Collection", "Fallback", "Yield", "Copy", "CopyCallbacks", "TrimKeys"];
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Enumerable_set_construction_matches_dotnet(bool optimized, int group)
    {
        string[] names = Names.Skip(group * 2).Take(2).ToArray();
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetConstructionProbe), names);
        var pairs = (from a in new[] { int.MinValue, -1, 0, 1, 3, 7, 15, 31, int.MaxValue }
                     from b in new[] { -1, 0, 1, 2, 3, 4, 5, 6, 7, 15, 31 } select (a,b)).ToArray();
        var probes = names.Select((name, i) =>
            (name, programs[i], typeof(HashSetConstructionProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray();
        if (group == 1)
        {
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [probes[0]], pairs);
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [probes[1]], pairs, expectedStaticRoots: 1);
        }
        else await IntegerObjectTranslationTests.VerifyTranslated(optimized, probes, pairs);
    }
    [Fact]
    public async Task Int64_enumerable_still_requires_wide_vector_support()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(HashSetConstructionProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(HashSetConstructionProbe).GetMethod("Wide")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CIL2001" && d.Message.Contains("other integer and enum arrays require later lowering", StringComparison.Ordinal));
    }
}
