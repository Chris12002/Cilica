using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DictionaryLibraryTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Fixture = new(BuildPrograms);

    private static async Task<ManagedExceptionIrProgram[]> BuildPrograms()
    {
        using var files = new ResolverFiles();
        string source = Path.GetFullPath(Path.Combine(CoreLibraryBuildTests.CorePath(".."), "..", "tests", "Fixtures", "Cilica.Fixtures.Execution", "DictionaryProbe.cs"));
        // The public System.Dictionary mapping is deliberately unavailable until
        // its interface contracts are implemented. Change only the dictionary
        // type identifier in this independent probe; the .NET oracle runs the
        // original source against the real framework implementation.
        string adapted = (await File.ReadAllTextAsync(source, TestContext.Current.CancellationToken))
            .Replace("Dictionary<", "Cilica.Library.TrialDictionary<", StringComparison.Ordinal);
        string nativeSource = files.Write("dictionary-probe.cs", System.Text.Encoding.UTF8.GetBytes(adapted));
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files, nativeSource);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var owner = scope.ResolveType(implementation, "Cilica.Fixtures.Execution", "DictionaryProbe").Value!;
        var programs = new System.Collections.Generic.List<ManagedExceptionIrProgram>();
        foreach (string name in new[] { "Integers", "WideKeys", "Guids", "Collisions", "CallbackFaults" })
        {
            var method = scope.ResolveClosedDeclaration(new(owner, SignatureList<TypeSignature>.Empty), name, 2, new()).Value!;
            var translated = ValueTranslator.TranslateIntegerObjects(scope, implementation, method.Handle, implementation);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            programs.Add(translated.Program!);
        }
        var unsupported = scope.ResolveClosedDeclaration(new(owner, SignatureList<TypeSignature>.Empty), "UnsupportedReferenceKey", 2, new()).Value!;
        var rejected = ValueTranslator.TranslateIntegerObjects(scope, implementation, unsupported.Handle, implementation);
        Assert.False(rejected.Success); Assert.Null(rejected.Program);
        Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("reference fallbacks", StringComparison.Ordinal));
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() }
        })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var integers = scope.ResolveClosedDeclaration(new(owner, SignatureList<TypeSignature>.Empty), "Integers", 2, new()).Value!;
        foreach (var capability in new[] { LibraryIntrinsicCapability.ClosedKeyEqual, LibraryIntrinsicCapability.ClosedKeyHash, LibraryIntrinsicCapability.ClosedKeyIsNull })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var narrowedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var deniedScope = resolver.CreateTrustedLibraryCompilationScope(plan, narrowedBytes, Convert.ToHexStringLower(SHA256.HashData(narrowedBytes))).Value!;
            var denied = ValueTranslator.TranslateIntegerObjects(deniedScope, implementation, integers.Handle, implementation);
            Assert.False(denied.Success);
            Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        var hostile = new ResolutionAssemblyBuilder(new("KeyIntrinsicCaller", new Version(1, 0, 0, 0)));
        var helper = hostile.TypeReference("Cilica.Runtime", "KeyOperations`1", hostile.Reference(implementation.Identity));
        var specialization = hostile.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(helper) << 2) | 1)), 1, 8);
        var call = hostile.Member(specialization, "IsNull", 0, 1, 2, 0x13, 0);
        hostile.Type("Test", "Caller");
        var root = hostile.Method("Run", [0, 0, 8], emit: il => {
            il.LoadConstantI4(0); il.Call(call); il.OpCode(ILOpCode.Ret);
        });
        var caller = scope.OpenInput(files.Write("key-caller.dll", hostile.Build())).Value!;
        var deniedCaller = ValueTranslator.TranslateIntegerObjects(scope, caller, root, implementation);
        Assert.False(deniedCaller.Success);
        Assert.Contains(deniedCaller.Diagnostics, d => d.Message.Contains("private to the selected core", StringComparison.Ordinal));
        return programs.ToArray();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Dictionary_growth_collisions_copies_and_mutating_key_callbacks_match_dotnet(bool optimized)
    {
        var programs = await Fixture.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [
            ("Integers", programs[0], DictionaryProbe.Integers), ("WideKeys", programs[1], DictionaryProbe.WideKeys),
            ("Guids", programs[2], DictionaryProbe.Guids), ("Collisions", programs[3], DictionaryProbe.Collisions),
            ("CallbackFaults", programs[4], DictionaryProbe.CallbackFaults)]);
    }
}
