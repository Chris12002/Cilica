using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
public sealed class EmptyStringTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(EmptyStringProbe), "Identity"));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Canonical_identity_and_faults_match_dotnet(bool optimized) =>
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("EmptyString", (await Programs.Value)[0], EmptyStringProbe.Identity)]);

    [Fact]
    public async Task Exact_field_grant_is_required_and_writes_are_rejected()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.StringEmpty));
        Assert.Equal(0x04, (int)((uint)grant.Token >> 24));
        var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g != grant)] }] };
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var input = resolver.OpenInput(typeof(EmptyStringProbe).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(EmptyStringProbe).GetMethod("Identity")!.MetadataToken);
        var rejected = ValueTranslator.TranslateIntegerObjects(denied, input, root, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics,d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);

        var b = new ResolutionAssemblyBuilder(new("WriteEmpty",new Version(1,0,0,0)));
        var text = b.TypeReference("System","String",b.Reference(implementation.Identity));
        var field = b.Member(text,"Empty",6,0x0e);
        b.Type("Test","Root");
        var write = b.Method("Write",[0,0,8],emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Stsfld); il.Token(field); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var authored = resolver.OpenInput(files.Write("write.dll",b.Build())).Value!;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,bytes,built.ManifestSha256).Value!;
        var illegal = ValueTranslator.TranslateIntegerObjects(scope,authored,write,implementation);
        Assert.False(illegal.Success); Assert.Contains(illegal.Diagnostics,d => d.Message.Contains("readonly Empty read",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Field_selector_checks_token_kind_budget_and_ambiguity(bool ambiguous)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("EmptySelection",new Version(1,0,0,0)));
        var type = b.Type("System","String");
        var field = b.Field("Empty",6,0x0e);
        b.Field(ambiguous ? "Empty" : "Other",6,8);
        var image = resolver.OpenInput(files.Write("fields.dll",b.Build())).Value!;
        var owner = resolver.ResolveType(image,type).Value!;
        var result = resolver.DescribeLibraryFieldIntrinsic(owner,"Empty",LibraryIntrinsicCapability.StringEmpty,new(3));
        Assert.Equal(!ambiguous,result.Success);
        Assert.False(resolver.DescribeLibraryFieldIntrinsic(owner,"Empty",LibraryIntrinsicCapability.StringEmpty,new(2)).Success);
        Assert.False(resolver.DescribeLibraryIntrinsic(image,field,LibraryIntrinsicCapability.StringLength).Success);
        Assert.False(resolver.DescribeLibraryIntrinsic(image,type,LibraryIntrinsicCapability.StringEmpty).Success);
    }
}
