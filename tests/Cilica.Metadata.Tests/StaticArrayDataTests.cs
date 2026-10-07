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
public sealed class StaticArrayDataTests
{
    private static readonly string[] Names = ["Integers", "Wide", "Bytes", "Chars", "Fresh", "CaughtBounds", "Multiple"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Constant_vectors_match_managed_bits_mutations_and_exceptions(bool optimized, int group)
    {
        var names = Names.Skip(group * 2).Take(2).ToArray();
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(StaticArrayDataProbe), names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name,i) =>
            (name, programs[i], typeof(StaticArrayDataProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Static_initialization_preserves_retained_array(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(StaticArrayDataProbe), "Static"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Static", program, StaticArrayDataProbe.Static)], expectedStaticRoots: 1);
    }
    [Fact]
    public async Task Runtime_field_handles_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(StaticArrayDataProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StaticArrayDataProbe).GetMethod("UnsupportedHandle")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
