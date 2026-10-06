using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class PairFormattingTests
{
    private static readonly string[] Names = ["Different", "Copies", "Callbacks", "ReturnValue", "Faults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(PairFormattingProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Two_boxed_arguments_preserve_each_payload_callback_and_fault_order(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(PairFormattingProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Fact]
    public async Task Both_arguments_require_direct_box_flow_and_ordinary_rendering()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(PairFormattingProbe).Assembly.Location).Value!;
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var pairGrant = artifact.Grants.Single(g => g.Capability == LibraryIntrinsicCapability.BoxedStringFormat
            && resolver.ReadMethodDeclarationInput(implementation, (MethodDefinitionHandle)MetadataTokens.EntityHandle(g.Token), new()).Value!.Signature.Parameters.Count == 3);
        foreach (bool denyRender in new[] { false, true })
        {
            var changed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => denyRender
                ? g.Capability != LibraryIntrinsicCapability.BoxedTextRender : g != pairGrant)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(changed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(PairFormattingProbe).GetMethod("Different")!.MetadataToken);
            var rejected = ValueTranslator.TranslateIntegerObjects(denied, input, method, implementation);
            Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
            if (!denyRender)
            {
                var single = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BoxedFormattingProbe).GetMethod("Text")!.MetadataToken);
                var allowed = ValueTranslator.TranslateIntegerObjects(denied, input, single, implementation);
                Assert.True(allowed.Success, string.Join("; ", allowed.Diagnostics));
            }
        }
        foreach (string name in new[] { "UnsupportedFirstAlias", "UnsupportedLastAlias", "UnsupportedJoin", "UnsupportedFirstCustom", "UnsupportedLastCustom" })
        {
            var method = (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(PairFormattingProbe).GetMethod(name)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(name.Contains("Custom", StringComparison.Ordinal) ? "IFormattable" : "box", StringComparison.Ordinal));
        }
    }
}
