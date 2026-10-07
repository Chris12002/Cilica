using System;
using System.Globalization;
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
public sealed class TupleTextTests
{
    private static readonly string[] Names = ["Signed", "Unsigned", "WideStrings", "Dictionary", "MutableDirect", "MutableBoxed", "Nested", "Custom", "Order", "HeapMutation"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(TupleTextProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)]
    public async Task Pair_text_preserves_fields_nulls_callbacks_and_faults(bool optimized, int group)
    {
        var programs = await Programs.Value; var saved = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var pairs = group == 4 ? (from a in new[] { int.MinValue, -1, 0, 1, 2, 3, int.MaxValue }
                from b in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 8, 9, 11, int.MaxValue } select (a,b)).ToArray() : null;
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
                (name, programs[i], typeof(TupleTextProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray(), pairs: pairs);
        } finally { CultureInfo.CurrentCulture = saved; }
    }
    [Theory]
    [InlineData("Nullable", "Nullable and enum tuple component text")]
    [InlineData("Enum", "Nullable and enum tuple component text")]
    [InlineData("Reference", "Tuple component text requires")]
    [InlineData("Character", "Narrow field addresses require")]
    [InlineData("One", "no implemented managed body")]
    [InlineData("Five", "no implemented managed body")]
    public async Task Other_components_and_arities_stay_diagnostic(string suffix, string diagnostic)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(TupleTextProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TupleTextProbe).GetMethod("Unsupported" + suffix)!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }
    [Fact]
    public async Task Tuple_element_text_requires_exact_grant_and_private_scope()
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts); var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.TupleElementText));
        var bytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input = scope.OpenInput(typeof(TupleTextProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(TupleTextProbe).GetMethod("Signed")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        using var full = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var hostile = new ResolutionAssemblyBuilder(new("HostileTupleText", new Version(1,0,0,0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "TupleElementText`1", hostile.Reference(implementation.Identity));
        var owner = hostile.Specification(0x15,0x12,checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)),1,8);
        var call = hostile.Member(owner,"Render",0,1,0x0e,0x10,0x13,0);
        hostile.Type("Test","Caller"); var root = hostile.Method("Run",[0,1,8,8],emit: il => { il.LoadArgumentAddress(0); il.Call(call); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var caller = full.OpenInput(files.Write("hostile-tuple.dll",hostile.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(full,caller,root,implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics,d => d.Message.Contains("lexical declaring scope",StringComparison.Ordinal));
    }
}
