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
public sealed class LinqFirstTests
{
    private static readonly string[] Names = ["Arrays", "Required", "EmptyAndNull", "Mutations", "References", "Covariant", "Fallback", "Faults", "IgnoreCount", "Where", "MatchedDefaults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqFirstProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Predicate_first_operations_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqFirstProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedNoPredicate")]
    public async Task Unimplemented_first_shapes_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(LinqFirstProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqFirstProbe).GetMethod(name)!.MetadataToken);
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation).Success);
    }
}
