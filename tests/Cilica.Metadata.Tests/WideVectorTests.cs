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
public sealed class WideVectorTests
{
    private static readonly string[] Names = ["Storage", "Generic", "Heap", "Empty", "Types", "Cast", "Faults", "Interfaces", "InterfaceFaults", "List", "Set", "SetArray"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    public async Task Int64_vectors_preserve_all_bits_and_composed_contracts(bool optimized, int group)
    {
        var names = Names.Skip(group * 2).Take(2).ToArray();
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(WideVectorProbe), names);
        var pairs = (from a in new[] { int.MinValue, -65537, -1, 0, 1, 65537, int.MaxValue }
                     from b in new[] { int.MinValue, -1, 0, 1, 2, 3, 65537, int.MaxValue } select (a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name,i) =>
            (name, programs[i], typeof(WideVectorProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray(), pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Signed_unsigned_scalar_array_views_preserve_exact_bits(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(WideVectorProbe), ["UnsignedView"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("UnsignedView", program, WideVectorProbe.UnsignedView)]);
    }
    [Fact]
    public async Task Unimplemented_wide_borrows_and_unsigned_vectors_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(WideVectorProbe).Assembly.Location).Value!;
        foreach (var name in new[] { "UnsupportedBorrow", "UnsupportedUnsigned", "UnsupportedUnsignedEnumeration", "UnsupportedUnsignedCopy" })
        {
            var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(WideVectorProbe).GetMethod(name)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
        }
    }
}
