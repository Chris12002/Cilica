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
public sealed class BoxedFormattingTests
{
    private static readonly (string Name, Func<int,int,int> Oracle)[] Probes = [
        ("Text", BoxedFormattingProbe.Text), ("Faults", BoxedFormattingProbe.Faults), ("Callback", BoxedFormattingProbe.Callback), ("Generic", BoxedFormattingProbe.Generic), ("UnrelatedDispatch", BoxedFormattingProbe.UnrelatedDispatch)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(BoxedFormattingProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Formatting_mutates_the_retained_box_and_preserves_utf16_and_faults(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p,i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }
    [Fact]
    public async Task Exact_box_proof_private_caller_and_both_grants_are_required()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(BoxedFormattingProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BoxedFormattingProbe).GetMethod(name)!.MetadataToken);
        foreach (var (name, message) in new[] { ("UnsupportedAlias", "directly boxed"), ("UnsupportedCustom", "IFormattable"), ("UnsupportedJoin", "directly boxed") })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
        var joined = new ResolutionAssemblyBuilder(new("JoinedFormatProof", new Version(1,0,0,0)));
        var selectedString = joined.TypeReference("System", "String", joined.Reference(implementation.Identity));
        var format = joined.Member(selectedString, "Format", 0, 2, 0x0e, 0x0e, 0x1c);
        var payload = joined.TypeReference("Cilica.Fixtures.Execution", "FormattingValue", joined.Reference(input.Identity));
        var literal = joined.String("{0}");
        joined.Type("Test", "Caller");
        var joinedRoot = joined.Method("Run", [0,2,8,8,8], locals: [7,1,0x11, checked((byte)((MetadataTokens.GetRowNumber(payload) << 2) | 1))], emit: il => {
            var makeBox = il.DefineLabel(); var callFormat = il.DefineLabel();
            il.LoadString(literal); il.LoadArgument(0); il.Branch(ILOpCode.Brfalse, makeBox);
            il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br, callFormat);
            il.MarkLabel(makeBox); il.LoadLocal(0); il.OpCode(ILOpCode.Box); il.Token(payload);
            il.MarkLabel(callFormat); il.Call(format); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var joinedImage = scope.OpenInput(files.Write("joined.dll", joined.Build())).Value!;
        var joinedResult = ValueTranslator.TranslateIntegerObjects(scope, joinedImage, joinedRoot, implementation);
        Assert.False(joinedResult.Success);
        Assert.Contains(joinedResult.Diagnostics, d => d.Message.Contains("same control-flow block", StringComparison.Ordinal));
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        foreach (var capability in new[] { LibraryIntrinsicCapability.BoxedStringFormat, LibraryIntrinsicCapability.BoxedTextRender })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Text"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        var hostile = new ResolutionAssemblyBuilder(new("HostileBoxedRender", new Version(1,0,0,0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "BoxedText`1", hostile.Reference(implementation.Identity));
        var value = hostile.TypeReference("Cilica.Fixtures.Execution", "FormattingValue", hostile.Reference(input.Identity));
        var owner = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1,
            0x11, checked((byte)((MetadataTokens.GetRowNumber(value) << 2) | 1)));
        var call = hostile.Member(owner, "Render", 0, 1, 0x0e, 0x1c);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0,0,8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.Call(call); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var caller = scope.OpenInput(files.Write("hostile.dll", hostile.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
}
