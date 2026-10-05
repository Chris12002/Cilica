using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class GuidLibraryTests
{
    private static readonly string[] Names = ["Identity", "Compare", "ProviderAndDefault", "Text", "CompareFields", "BaseExceptionMessages", "DefaultExceptionMessages"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Fixture = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(GuidProbe), Names));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Guid_values_comparison_and_every_formatted_code_unit_match_dotnet(bool optimized)
    {
        var programs = await Fixture.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [
            (Names[0], programs[0], GuidProbe.Identity), (Names[1], programs[1], GuidProbe.Compare),
            (Names[2], programs[2], GuidProbe.ProviderAndDefault), (Names[5], programs[5], GuidProbe.BaseExceptionMessages),
            (Names[6], programs[6], GuidProbe.DefaultExceptionMessages)]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(Names[3], programs[3], GuidProbe.Text)],
            (from seed in Enumerable.Range(0, 3) from format in Enumerable.Range(0, 14)
             from position in Enumerable.Range(-1, GuidProbe.Text(seed * 16 + format, -1) + 2)
             select (seed * 16 + format, position)).ToArray());
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(Names[4], programs[4], GuidProbe.CompareFields)],
            (from field in Enumerable.Range(0, 11) from value in new[] { -1, 0, 1, 127, 128, 255, 32767, 32768, 65535, int.MinValue, int.MaxValue }
             select (field, value)).ToArray());
    }

    [Fact]
    public async Task Framework_exception_resources_reject_forged_names_and_ancestry()
    {
        var program = (await Fixture.Value)[3];
        var flow = program.Flow.Flow.Flow;
        var format = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.Format);
        var layout = flow.Types.References.Types.Single(t => ObjectTypeId.From(t.Source) == format.Type);
        foreach (var changed in new[] { layout with { BaseType = program.Exceptions.Base }, layout with { Source = layout.Source with { Name = "Other.FormatException" } } })
        {
            var types = flow.Types with { References = flow.Types.References with { Types = flow.Types.References.Types.Replace(layout, changed) } };
            var corrupted = program with { Flow = program.Flow with { Flow = new(flow with { Types = types }) } };
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(corrupted));
        }
    }

    [Fact]
    public async Task Format_exception_requires_each_exact_layout_and_constructor_grant()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var resolverLifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() }
        })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(GuidProbe).Assembly.Location).Value!;
        foreach (var grant in artifact.Grants.Where(g => g.Owner == "System.FormatException"))
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] };
            var narrowedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, narrowedBytes, Convert.ToHexStringLower(SHA256.HashData(narrowedBytes))).Value!;
            var results = new[] { "DefaultExceptionMessages", "BaseExceptionMessages" }.Select(name =>
                ValueTranslator.TranslateIntegerObjects(scope, input,
                    (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(GuidProbe).GetMethod(name)!.MetadataToken), implementation)).ToArray();
            var denied = Assert.Single(results.Where(result => !result.Success).Take(1));
            Assert.False(denied.Success);
            Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
    }

    [Fact]
    public async Task Unimplemented_Guid_apis_stay_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var resolverLifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(GuidProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "Parse", "Boxed" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(GuidProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        }
    }
}
