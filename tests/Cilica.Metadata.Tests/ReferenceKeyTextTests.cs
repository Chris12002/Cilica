using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ReferenceKeyTextTests
{
    private static readonly string[] Names = ["DefaultName", "GenericName", "HiddenName", "Duplicate", "Missing", "NullOrder"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ReferenceKeyTextProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Sealed_key_names_callbacks_and_fault_order_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ReferenceKeyTextProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedPolymorphic", "exact sealed ordinary class")]
    [InlineData("UnsupportedFormattable", "IFormattable requires provider dispatch")]
    public async Task Broader_reference_formatting_stays_diagnostic(string name, string diagnostic)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ReferenceKeyTextProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ReferenceKeyTextProbe).GetMethod(name)!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
    [Fact]
    public async Task Application_cannot_call_private_type_name_helper()
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var hostile = new ResolutionAssemblyBuilder(new("HostileReferenceKeyName", new Version(1,0,0,0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "DefaultReferenceKeyText`1", hostile.Reference(implementation.Identity));
        var owner = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1, 0x0e);
        var call = hostile.Member(owner, "Name", 0, 0, 0x0e);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0,0,8], emit: il => { il.Call(call); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var input = scope.OpenInput(files.Write("hostile-name.dll", hostile.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(LibraryIntrinsicCapability.DictionaryKeyText)] [InlineData(LibraryIntrinsicCapability.ReferenceKeyTypeName)]
    public async Task Key_text_and_private_name_require_exact_grants(LibraryIntrinsicCapability capability)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts); var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == capability));
        var bytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input = scope.OpenInput(typeof(ReferenceKeyTextProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ReferenceKeyTextProbe).GetMethod("DefaultName")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
    }
}
