using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class TypeInitializationTranslationTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Fields", TypeInitializationProbe.Fields), ("Failure", TypeInitializationProbe.Failure), ("Closed", TypeInitializationProbe.Closed), ("GenericFailure", TypeInitializationProbe.GenericFailure)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(TypeInitializationProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Field_initializers_reentry_failures_and_closed_identity_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray(), persistentRoots: true);
    }
    [Fact]
    public async Task Closed_initializers_keep_distinct_identity_and_definition_display_names()
    {
        var program = (await Programs.Value)[2];
        var initializers = program.Flow.Bindings.Where(b => b.TypeInitializerName is not null).ToArray();
        Assert.Equal(2, initializers.Length);
        Assert.Equal(2, initializers.Select(b => b.Owner).Distinct().Count());
        Assert.Single(initializers.Select(b => b.TypeInitializerName).Distinct());
        Assert.All(initializers, b => Assert.NotEmpty(b.Owner.Specialization));
    }
    [Fact]
    public async Task Initialization_requires_the_exact_wrapper_grant_and_strict_types_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.TypeInitializationExceptionConstructor)] }] });
        var input = resolver.OpenInput(typeof(TypeInitializationProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TypeInitializationProbe).GetMethod(name)!.MetadataToken);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var failed = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Fields"), implementation);
        Assert.False(failed.Success); Assert.Contains(failed.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var strict = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedStrict"), implementation);
        Assert.False(strict.Success); Assert.Contains(strict.Diagnostics, d => d.Message.Contains("strict type initialization", StringComparison.Ordinal));
    }

}
