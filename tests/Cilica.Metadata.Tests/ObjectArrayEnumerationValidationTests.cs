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
public sealed class ObjectArrayEnumerationValidationTests
{
    [Theory]
    [InlineData(LibraryIntrinsicCapability.ArrayInterfaceEnumerateObjects)]
    [InlineData(LibraryIntrinsicCapability.ClosedTypeIsValueType)]
    public async Task New_core_capabilities_require_their_exact_grants(LibraryIntrinsicCapability capability)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts); var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == capability));
        var method = capability == LibraryIntrinsicCapability.ArrayInterfaceEnumerateObjects
            ? typeof(ObjectArrayEnumerationProbe).GetMethod("Integers")! : typeof(LinqOfTypeProbe).GetMethod("ValueProjection")!;
        var input = resolver.OpenInput(method.DeclaringType!.Assembly.Location).Value!;
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken);
        using var allowed = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var control = ValueTranslator.TranslateIntegerObjects(allowed, input, handle, implementation);
        Assert.True(control.Success, string.Join("; ", control.Diagnostics));
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(denied, input, handle, implementation);
        Assert.False(rejected.Success); Assert.Contains(rejected.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
    }
    [Fact]
    public async Task External_callers_cannot_invoke_private_type_classification()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new("ExternalTypeKind", new Version(1,0,0,0)));
        var owner = b.TypeReference("Cilica.Runtime", "KeyOperations`1", b.Reference(implementation.Identity));
        var spec = b.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(owner) << 2) | 1)), 1, 8);
        var call = b.Member(spec, "IsValueType", 0, 0, 2);
        b.Type("Example", "Caller"); var root = b.Method("Run", [0,0,8], emit: il => { il.Call(call); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("external-type-kind.dll", b.Build())).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("Explicit type use requires lexical", StringComparison.Ordinal) && d.Message.Contains("KeyOperations", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Nongeneric_array_packets_retain_exact_result_and_receiver_at_both_stages()
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(ObjectArrayEnumerationProbe), ["Integers"]))[0];
        foreach (var original in new[] { program.Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch! })
        {
            ObjectInterfacePlanValidator.Validate(original);
            foreach (var method in original.Methods.Where(m => m.ArrayHelper && m.Name == "EnumerateObjects"))
                foreach (var changed in new[] { method with { ReturnType = IrType.Reference(original.Types.ObjectBase) },
                    method with { Parameters = method.Parameters.SetItem(0, IrType.Reference(original.Types.ObjectBase)) },
                    method with { Parameters = method.Parameters.Add(IrType.Int32) }, method with { Name = "Enumerate" }, method with { ArrayHelper = false } })
                    Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(original with { Methods = original.Methods.Replace(method, changed) }));
            Assert.Contains(original.Methods, m => m.ArrayHelper && m.Name == "EnumerateObjects");
        }
    }
}
