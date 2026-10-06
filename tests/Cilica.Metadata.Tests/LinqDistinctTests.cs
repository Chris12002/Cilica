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
public sealed class LinqDistinctTests
{
    private static readonly string[] Names = ["Integers", "Identity", "Unstarted", "EmptyAndNull", "Text", "MutableValues", "AnySkipsHash", "Fallback", "Faults", "CollectionTerminals", "ListAny", "ProjectedAny", "FilteredAny", "Projection", "Resume", "ValuePipeline", "EqualityFault", "ReferenceCurrent"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqDistinctProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    [InlineData(false, 5)] [InlineData(true, 5)]
    public async Task Distinct_iteration_and_terminal_callbacks_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqDistinctProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedComparer")] [InlineData("UnsupportedList")]
    public async Task Unimplemented_distinct_shapes_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(LinqDistinctProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(LinqDistinctProbe).GetMethod(name)!.MetadataToken);
        var translated = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(translated.Success);
    }

}
