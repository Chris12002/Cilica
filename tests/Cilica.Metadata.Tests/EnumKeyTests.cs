using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EnumKeyTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram>> Hash = new(BuildHash);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Enum_key_hash_preserves_exact_underlying_bits(bool optimized)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Hash", await Hash.Value, (int a, int b) => ((EnumKey)a).GetHashCode())]);
    }
    private static async Task<ManagedExceptionIrProgram> BuildHash()
    {
        using var files = new ResolverFiles();
        string extra = files.Write("EnumKeyHash.cs", Encoding.UTF8.GetBytes("""
            namespace Cilica.Runtime {
                public enum HashKey { Zero = 0 }
                public static class EnumKeyHash {
                    public static int Run(int a, int b) {
                        HashKey key = (HashKey)a;
                        return KeyOperations<HashKey>.Hash(ref key);
                    }
                }
            }
            """));
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files, extra);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var owner = scope.ResolveType(implementation, "Cilica.Runtime", "EnumKeyHash").Value!;
        var method = scope.ResolveClosedDeclaration(new(owner, SignatureList<TypeSignature>.Empty), "Run", 2, new MemberResolutionBudget(1000)).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, implementation, method.Handle, implementation);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != LibraryIntrinsicCapability.ClosedKeyHash)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var deniedScope = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var denied = ValueTranslator.TranslateIntegerObjects(deniedScope, implementation, method.Handle, implementation);
        Assert.False(denied.Success); Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        return result.Program!;
    }
    [Theory]
    [InlineData("Dictionary", "enum element equivalence")]
    [InlineData("Aliases", "enum element equivalence")]
    [InlineData("Set", "enum element equivalence")]
    [InlineData("Views", "enum element equivalence")]
    [InlineData("UnsupportedAliasFormatting", "duplicate-value aliases")]
    [InlineData("UnsupportedWide", "Int32 enum")] [InlineData("UnsupportedHidden", "externally visible")]
    public async Task Collection_dependencies_other_widths_and_hidden_arguments_remain_diagnostic(string name, string diagnostic)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(EnumKeyProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EnumKeyProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
}
