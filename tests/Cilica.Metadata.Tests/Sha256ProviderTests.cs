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
public sealed class Sha256ProviderTests
{
    private static readonly string[] Names = ["Byte", "Reuse", "Faults", "Recovery", "Interface", "NullReceiver", "Utf8"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(Sha256ProviderProbe), Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Whole_array_provider_matches_every_digest_byte(bool optimized)
    {
        var program = (await Programs.Value)[0];
        var pairs = (from length in new[] { -2, -1, 0, 1, 55, 56, 63, 64, 65, 119, 120, 127, 128, 129 }
                     from position in Enumerable.Range(-1, 33) select (length, position)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Byte", program, Sha256ProviderProbe.Byte)], pairs);
    }
    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Provider_instances_lifecycle_and_utf8_match_dotnet(bool optimized, int first)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(Sha256ProviderProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(first).Take(first == 4 ? 2 : 3).ToArray());
        if (first == 4) await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Utf8", programs[6], Sha256ProviderProbe.Utf8)], expectedStaticRoots: 1);
    }
    [Fact]
    public async Task Unimplemented_streaming_hash_state_and_custom_providers_fail_closed()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(Sha256ProviderProbe).Assembly.Location).Value!;
        foreach (var name in new[] { "UnsupportedOffset", "UnsupportedHashProperty", "UnsupportedTransform", "UnsupportedCustom", "UnsupportedShaSubclass" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(Sha256ProviderProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success, name); Assert.NotEmpty(result.Diagnostics);
        }
    }
}
