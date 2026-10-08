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
public sealed class TupleThreeTests
{
    private static readonly string[] Names = ["Fields", "Copies", "Returns", "Heap", "Arrays", "Boxing", "Nullable", "Equality", "TextEquality", "ComponentEffects", "ConditionalConstruction"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(TupleThreeProbe), Names));
    [Fact]
    public async Task Three_component_probes_translate() => Assert.Equal(Names.Length, (await Programs.Value).Length);
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Three_component_storage_copies_and_typed_equality_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(TupleThreeProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 5).Take(5).ToArray());
    }
    [Fact]
    public async Task Unimplemented_tuple_contracts_remain_explicit_diagnostics()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(TupleThreeProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "UnsupportedHash", "UnsupportedComparison", "UnsupportedText", "UnsupportedObjectEquals" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TupleThreeProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success, name); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("no implemented managed body", StringComparison.Ordinal));
        }
    }
}
