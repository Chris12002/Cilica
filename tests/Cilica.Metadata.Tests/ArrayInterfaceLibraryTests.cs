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

public sealed class ArrayInterfaceLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Casts", ArrayInterfaceProbe.Casts), ("Integers", ArrayInterfaceProbe.Integers),
        ("Covariance", ArrayInterfaceProbe.Covariance), ("Faults", ArrayInterfaceProbe.Faults), ("Mixed", ArrayInterfaceProbe.Mixed), ("SignedReads", ArrayInterfaceProbe.SignedReads), ("Flags", ArrayInterfaceProbe.Flags)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayInterfaceProbe), Probes.Select(p => p.Name).ToArray()));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Array_interface_casts_calls_covariance_and_faults_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }

    [Fact]
    public async Task Each_helper_requires_its_exact_grant_and_unimplemented_operations_are_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ArrayInterfaceProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArrayInterfaceProbe).GetMethod(name)!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.ArrayInterfaceCount, LibraryIntrinsicCapability.ArrayInterfaceGet, LibraryIntrinsicCapability.ArrayInterfaceFalse, LibraryIntrinsicCapability.ArrayInterfaceTrue, LibraryIntrinsicCapability.ArrayInterfaceIdentity, LibraryIntrinsicCapability.ArrayInterfaceEnumerate })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method(capability == LibraryIntrinsicCapability.ArrayInterfaceEnumerate ? "UnsupportedEnumeration" : capability is LibraryIntrinsicCapability.ArrayInterfaceCount or LibraryIntrinsicCapability.ArrayInterfaceGet ? "Integers" : "Flags"), implementation);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var unsupported = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedEnumeration"), implementation);
        var enumArgument = ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedEnum"), implementation);
        Assert.False(enumArgument.Success);
        Assert.Contains(enumArgument.Diagnostics, d => d.Message.Contains("enum element equivalence", StringComparison.Ordinal));
        Assert.True(unsupported.Success, string.Join("; ", unsupported.Diagnostics));
    }

    [Theory]
    [InlineData("coverage")] [InlineData("receiver")] [InlineData("operation")] [InlineData("kind")] [InlineData("contract")]
    public async Task Detached_array_dispatch_assertions_are_checked(string mutation)
    {
        var program = (await Programs.Value)[1];
        var plan = program.Flow.InterfaceDispatch!;
        plan = JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(plan))!;
        ObjectInterfacePlanValidator.Validate(plan);
        var fact = plan.Methods.First(m => m.ArrayHelper);
        var map = plan.Dispatch.First(m => m.Targets.Any(t => t.Implementation == fact.Binding.Method));
        if (mutation == "coverage") plan = plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = [] }) };
        else if (mutation == "contract")
        {
            var contract = plan.Types.Interfaces.First(i => i.ArrayContract == ObjectArrayInterfaceKind.ReadOnlyList);
            var types = plan.Types with { Interfaces = plan.Types.Interfaces.Replace(contract, contract with { ArrayContract = (ObjectArrayInterfaceKind)99 }) };
            plan = plan with { Types = types };
        }
        else
        {
            var changed = mutation switch {
                "receiver" => fact with { Parameters = fact.Parameters.SetItem(0, IrType.Reference(plan.Types.ObjectBase)) },
                "operation" => fact with { Name = "GetEnumerator" },
                _ => fact with { ArrayHelper = false } };
            plan = plan with { Methods = plan.Methods.Replace(fact, changed) };
        }
        Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan));
    }
}
