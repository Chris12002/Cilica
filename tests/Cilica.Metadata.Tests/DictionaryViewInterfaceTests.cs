using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DictionaryViewInterfaceTests
{
    private static readonly string[] Names = ["Order", "Current", "Mutations", "Defaults", "Copies", "References", "Values", "Nullable", "ClearCurrent", "EmptyIdentity", "PairOrder", "PairStates", "PairReferences", "PairEmpty"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DictionaryViewInterfaceProbe), Names));
    [Fact]
    public async Task View_interface_probes_translate() => Assert.Equal(Names.Length, (await Programs.Value).Length);
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)] [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Boxed_view_cursors_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name, i) =>
            (name, programs[i], typeof(DictionaryViewInterfaceProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3);
        foreach (var batch in probes.GroupBy(p => p.name == "PairEmpty" ? 2 : p.name == "EmptyIdentity" ? 1 : 0))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, batch.ToArray(), expectedStaticRoots: batch.Key);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task All_mutation_modes_match_dotnet(bool optimized)
    {
        var program = (await Programs.Value)[2];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Mutations", program, DictionaryViewInterfaceProbe.Mutations)],
            pairs: (from input in new[] { -3, int.MaxValue } from mode in Enumerable.Range(0, 8) select (input, mode)).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedDictionaryEntry")] [InlineData("UnsupportedEntryView")]
    public async Task Legacy_dictionary_entry_apis_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(DictionaryViewInterfaceProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DictionaryViewInterfaceProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("no implemented managed body", StringComparison.Ordinal));
    }

}
