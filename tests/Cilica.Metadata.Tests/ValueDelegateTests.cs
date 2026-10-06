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
public sealed class ValueDelegateTests
{
    private static readonly string[] Names = ["Static", "Captured", "GenericPair", "GenericNested", "EmptyValue", "ReferenceResult", "Void", "Null", "Fault", "Predicate", "Query", "Closure", "Count", "DefaultQuery", "Nominal"];
    internal static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ValueDelegateProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Inline_arguments_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ValueDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), persistentRoots: group == 4);
    }
    [Theory]
    [InlineData("UnsupportedResult")] [InlineData("UnsupportedByRef")]
    public async Task Unsupported_value_delegate_shapes_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(ValueDelegateProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueDelegateProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Delegate requires", StringComparison.Ordinal));
    }
}
