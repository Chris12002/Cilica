using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.LibraryBuild;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CoreLibraryBuildTests
{
    internal static string CorePath(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) root = root.Parent;
        Assert.NotNull(root); return Path.Combine(root.FullName, "library", "Cilica.Core", name);
    }

    internal static async Task<(LibraryBuildResult Built, LibraryBuildRequest Request, AssemblyResolver Resolver, ResolvedAssembly Implementation, LibraryBindingPlan Plan)> BuildAndMap(ResolverFiles files)
    {
        var requests = LibraryIntrinsicRequests.Read(CorePath("intrinsic-requests.json"));
        var request = LibraryBuildProducerTests.Request(files, CorePath("CoreTypes.cs"));
        string reference = request.References.Single();
        request = request with { AssemblyName = "Cilica.Core", References = [], CoreLibrary = true, Intrinsics = requests };
        var built = await LibraryBuilder.Build(request);
        var resolver = AssemblyResolver.Create([Path.GetDirectoryName(reference)!,
            Path.GetDirectoryName(typeof(Cilica.Library.IntegerFormatting).Assembly.Location)!]).Value!;
        var contract = resolver.OpenInput(reference).Value!; Assert.True(contract.IsReferenceAssembly);
        var implementation = resolver.OpenInput(built.AssemblyPath).Value!;
        var names = requests.Select(r => (r.Namespace, r.Type)).Append(("System", "Object")).Append(("System", "ValueType")).Append(("System", "IFormatProvider")).Concat(new[] { "Int32", "UInt32", "Int64", "UInt64" }.Select(n => ("System", n))).Distinct().ToArray();
        var pairs = names.Select(name => new LibraryTypeMapping(resolver.ResolveType(contract, name.Item1, name.Item2).Value!,
            resolver.ResolveType(implementation, name.Item1, name.Item2).Value!));
        var plan = resolver.CreateLibraryBindingPlan(pairs);
        Assert.True(plan.Success, string.Join("; ", plan.Diagnostics));
        return (built, request, resolver, implementation, plan.Value!);
    }

    internal static async Task<ManagedExceptionIrProgram> TranslateFixture(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await BuildAndMap(files);
        using var resolverLifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(CoreLibraryProbe).Assembly.Location).Value!;
        var translated = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod(name)!.MetadataToken), implementation);
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        return translated.Program!;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Built_core_maps_real_reference_declarations_and_executes_native_strings_and_exceptions(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, request, resolver, implementation, plan) = await BuildAndMap(files);
        using var resolverLifetime = resolver;
        var requests = request.Intrinsics;
        Assert.Empty(implementation.References); Assert.False(implementation.IsReferenceAssembly);
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions {
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() }
        })!;
        var artifact = Assert.Single(manifest.Artifacts);
        Assert.Equal(requests.Length, artifact.Grants.Length);
        Assert.DoesNotContain(artifact.Inputs, i => i.Role == "reference");
        Assert.Contains("-runtimemetadataversion:v4.0.30319", artifact.CompilerArguments);
        using var options = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(request.OutputDirectory, "build-options.json"), TestContext.Current.CancellationToken));
        Assert.True(options.RootElement.GetProperty("CoreLibrary").GetBoolean());
        Assert.Equal(requests.Length, options.RootElement.GetProperty("IntrinsicRequests").GetArrayLength());
        // Independent approval is simulated only for this reviewed local test build.
        // Neither the producer nor ordinary compilation auto-admits the digest.
        var admitted = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256);
        Assert.True(admitted.Success, string.Join("; ", admitted.Diagnostics));
        using var scope = admitted.Value!;
        Assert.False(resolver.CreateTrustedLibraryCompilationScope(plan, bytes, new string('0', 64)).Success);
        var input = resolver.OpenInput(typeof(CoreLibraryProbe).Assembly.Location).Value!;
        var unsupported = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod("UnsupportedHash")!.MetadataToken), implementation);
        Assert.False(unsupported.Success); Assert.Null(unsupported.Program); Assert.NotEmpty(unsupported.Diagnostics);
        var cultureMethod = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod("InvariantIdentity")!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.CultureInfoLayout, LibraryIntrinsicCapability.InvariantCulture })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var narrowedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var narrowedScope = resolver.CreateTrustedLibraryCompilationScope(plan, narrowedBytes, Convert.ToHexStringLower(SHA256.HashData(narrowedBytes))).Value!;
            var denied = ValueTranslator.TranslateIntegerObjects(narrowedScope, input, cultureMethod, implementation);
            Assert.False(denied.Success); Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        var otherCultureMember = ValueTranslator.TranslateIntegerObjects(scope, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod("UnsupportedCulture")!.MetadataToken), implementation);
        Assert.False(otherCultureMember.Success); Assert.NotEmpty(otherCultureMember.Diagnostics);
        var probes = new[] { ("Text", (Func<int, int, int>)CoreLibraryProbe.Text), ("Fault", CoreLibraryProbe.Fault), ("Formatting", CoreLibraryProbe.Formatting), ("InvariantIdentity", CoreLibraryProbe.InvariantIdentity), ("IntegerHashes", CoreLibraryProbe.IntegerHashes) };
        var translated = probes.Select(probe => {
            var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod(probe.Item1)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            using var unapproved = resolver.CreateLibraryCompilationScope(plan);
            var denied = ValueTranslator.TranslateIntegerObjects(unapproved, input, method, implementation);
            Assert.False(denied.Success); Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
            return (probe.Item1, result.Program!, probe.Item2);
        }).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, translated);
    }
}
