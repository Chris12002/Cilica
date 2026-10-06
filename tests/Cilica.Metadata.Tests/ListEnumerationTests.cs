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
public sealed class ListEnumerationTests
{
    private static readonly string[] Names = ["Integers", "Empty", "Copy", "Mutation", "NoMutation", "References", "Values", "Default", "EndMutation", "BreakAndFinally", "AllocatingValues", "NestedOwner"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListEnumerationProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Concrete_enumeration_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(4).ToArray());
    }
    [Fact]
    public async Task Legacy_current_remains_a_specific_build_error()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(ListEnumerationProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListEnumerationProbe).GetMethod("UnsupportedLegacyCurrent")!.MetadataToken), implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.ExecutionUnsupported && d.Message.Contains("Boxed interface method dispatch requires payload receiver lowering", StringComparison.Ordinal));
    }
}
