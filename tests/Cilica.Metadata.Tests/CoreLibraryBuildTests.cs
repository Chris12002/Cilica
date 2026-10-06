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

    internal static async Task<(LibraryBuildResult Built, LibraryBuildRequest Request, AssemblyResolver Resolver, ResolvedAssembly Implementation, LibraryBindingPlan Plan)> BuildAndMap(ResolverFiles files, params string[] additionalSources)
    {
        var requests = LibraryIntrinsicRequests.Read(CorePath("intrinsic-requests.json"));
        var request = LibraryBuildProducerTests.Request(files, CorePath("CoreTypes.cs"));
        string reference = request.References.Single();
        request = request with { Sources = [CorePath("CoreTypes.cs"), CorePath("RuntimeTypes.cs"), CorePath("Environment.cs"), CorePath("Nullable.cs"), CorePath("IntegerText.cs"), CorePath("CollectionContracts.cs"), CorePath("DelegateContracts.cs"), CorePath("Guid.cs"), CorePath("KeyOperations.cs"), CorePath("Dictionary.cs"), CorePath("HashSet.cs"), CorePath("DictionaryContracts.cs"), CorePath("ArrayInterface.cs"), CorePath("ArrayEnumerator.cs"), CorePath("Enumerable.cs"), CorePath("EnumerableContains.cs"), CorePath("WhereIterator.cs"), CorePath("SelectIterator.cs"), CorePath("DistinctIterator.cs"), CorePath("ConcatIterator.cs"), CorePath("OrderedIterator.cs"), CorePath("MapSort.cs"), CorePath("OfTypeIterator.cs"), CorePath("StringBuilder.cs"), CorePath("StringConcatenation.cs"), CorePath("List.cs"), CorePath("ReadOnlyCollection.cs"), CorePath("ValueTuple.cs"), CorePath("StringKeyOperations.cs"), CorePath("PairHash.cs"), CorePath("BoxedFormatting.cs"), CorePath("StringComparison.cs"), CorePath("StringComparer.cs"), CorePath("Comparer.cs"), CorePath("StringWhitespace.cs"), CorePath("Math.cs"), Path.GetFullPath(Path.Combine(CorePath(".."), "Cilica.Library", "IntegerFormatting.cs")), .. additionalSources], AssemblyName = "Cilica.Core", References = [], CoreLibrary = true, Intrinsics = requests };
        var built = await LibraryBuilder.Build(request);
        var resolver = AssemblyResolver.Create([Path.GetDirectoryName(reference)!, Path.GetDirectoryName(built.AssemblyPath)!,
            Path.GetDirectoryName(typeof(Cilica.Library.IntegerFormatting).Assembly.Location)!]).Value!;
        var contract = resolver.OpenInput(reference).Value!; Assert.True(contract.IsReferenceAssembly);
        var implementation = resolver.OpenInput(built.AssemblyPath).Value!;
        var names = requests.Where(r => r.Namespace != "Cilica.Runtime").Select(r => (r.Namespace, r.Type)).Append(("System", "Object")).Append(("System", "ValueType")).Append(("System", "IFormatProvider")).Concat(new[] { "Boolean", "Char", "SByte", "Byte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64" }.Select(n => ("System", n))).Distinct().ToArray();
        names = names.Concat(new[] { ("System.Linq", "Enumerable"), ("System.Linq", "IOrderedEnumerable`1"), ("System.Reflection", "MemberInfo"), ("System", "StringComparison"), ("System", "StringComparer"), ("System.Collections.Generic", "IComparer`1"), ("System.Collections.Generic", "Comparer`1"), ("System.Collections.Generic", "IEqualityComparer`1"), ("System", "ValueTuple`2"), ("System.Collections", "IStructuralEquatable"), ("System.Collections", "IStructuralComparable"), ("System.Collections", "IEqualityComparer"), ("System.Collections", "IComparer"), ("System.Runtime.CompilerServices", "ITuple") }).Concat(DictionaryContractNames).Concat(CollectionContractNames).Concat(new[] { "Nullable`1", "Enum", "Delegate", "MulticastDelegate", "Action`1", "Action`2", "Func`2", "Func`3", "Predicate`1", "Comparison`1", "Guid", "Math", "Environment", "IEquatable`1", "IComparable", "IComparable`1", "IFormattable" }.Select(n => ("System", n))).ToArray();
        var pairs = names.Select(name => new LibraryTypeMapping(resolver.ResolveType(contract, name.Item1, name.Item2).Value!,
            resolver.ResolveType(implementation, name.Item1, name.Item2).Value!));
        pairs = pairs.Append(new LibraryTypeMapping(
            resolver.ResolveType(contract, "System.Collections.Generic", "List`1", "Enumerator").Value!,
            resolver.ResolveType(implementation, "System.Collections.Generic", "List`1", "Enumerator").Value!));
        foreach (string nested in new[] { "KeyCollection", "ValueCollection", "Enumerator" })
            pairs = pairs.Append(new LibraryTypeMapping(
                resolver.ResolveType(contract, "System.Collections.Generic", "Dictionary`2", nested).Value!,
                resolver.ResolveType(implementation, "System.Collections.Generic", "Dictionary`2", nested).Value!));
        foreach (string view in new[] { "KeyCollection", "ValueCollection" })
            pairs = pairs.Append(new LibraryTypeMapping(
                resolver.ResolveType(contract, "System.Collections.Generic", "Dictionary`2", view, "Enumerator").Value!,
                resolver.ResolveType(implementation, "System.Collections.Generic", "Dictionary`2", view, "Enumerator").Value!));
        var plan = resolver.CreateLibraryBindingPlan(pairs);
        Assert.True(plan.Success, string.Join("; ", plan.Diagnostics));
        return (built, request, resolver, implementation, plan.Value!);
    }

    internal static readonly (string Namespace, string Name)[] DictionaryContractNames = [
        ("System.Text", "StringBuilder"), ("System.Collections.Generic", "List`1"), ("System.Collections.ObjectModel", "ReadOnlyCollection`1"), ("System", "Array"), ("System.Collections", "ICollection"), ("System.Collections", "IList"), ("System.Collections", "IDictionary"),
        ("System.Collections", "IDictionaryEnumerator"), ("System.Collections", "DictionaryEntry"),
        ("System.Collections.Generic", "ICollection`1"), ("System.Collections.Generic", "ISet`1"), ("System.Collections.Generic", "HashSet`1"), ("System.Collections.Generic", "IList`1"),
        ("System.Collections.Generic", "IDictionary`2"), ("System.Collections.Generic", "IReadOnlyDictionary`2"),
        ("System.Collections.Generic", "KeyValuePair`2"), ("System.Collections.Generic", "Dictionary`2"),
        ("System.Runtime.Serialization", "ISerializable"), ("System.Runtime.Serialization", "IDeserializationCallback"),
        ("System.Runtime.Serialization", "SerializationInfo"), ("System.Runtime.Serialization", "StreamingContext")];

    internal static readonly (string Namespace, string Name)[] CollectionContractNames = [
        ("System", "IDisposable"), ("System.Collections", "IEnumerable"), ("System.Collections", "IEnumerator"),
        ("System.Collections.Generic", "IEnumerable`1"), ("System.Collections.Generic", "IEnumerator`1"),
        ("System.Collections.Generic", "IReadOnlyCollection`1"), ("System.Collections.Generic", "IReadOnlyList`1")];

    internal static async Task<ManagedExceptionIrProgram> TranslateFixture(string name) => (await TranslateFixtures(name))[0];

    internal static async Task<ManagedExceptionIrProgram[]> TranslateFixtures(params string[] names)
        => await TranslateFixtures(typeof(CoreLibraryProbe), names);

    internal static async Task<ManagedExceptionIrProgram[]> TranslateFixtures(Type fixture, params string[] names)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await BuildAndMap(files);
        using var resolverLifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(fixture.Assembly.Location).Value!;
        return names.Select(name => {
            var translated = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(fixture.GetMethod(name)!.MetadataToken), implementation);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            return translated.Program!;
        }).ToArray();
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
        var numericMethod = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod("IntegerText")!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.InvariantIntegerFormatting, LibraryIntrinsicCapability.IntegerTextCacheFind, LibraryIntrinsicCapability.IntegerTextCachePublish })
        {
            var narrowed = manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != capability)] }] };
            var narrowedBytes = AssemblyResolver.SerializeLibraryBuildManifest(narrowed);
            using var narrowedScope = resolver.CreateTrustedLibraryCompilationScope(plan, narrowedBytes, Convert.ToHexStringLower(SHA256.HashData(narrowedBytes))).Value!;
            var denied = ValueTranslator.TranslateIntegerObjects(narrowedScope, input, numericMethod, implementation);
            Assert.False(denied.Success); Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        foreach (string name in new[] { "UnsupportedProvider", "AliasedProvider" })
        {
            var unsupportedProvider = ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(unsupportedProvider.Success);
            Assert.Contains(unsupportedProvider.Diagnostics, d => d.Message.Contains("directly proven CultureInfo.InvariantCulture", StringComparison.Ordinal));
        }
        var hostile = new ResolutionAssemblyBuilder(new AssemblyIdentity("ExternalCacheCaller", new Version(1, 0, 0, 0)));
        var cacheOwner = hostile.TypeReference("Cilica.Runtime", "IntegerTextCache", hostile.Reference(implementation.Identity));
        var findCache = hostile.Member(cacheOwner, "Find", 0, 1, 0x0e, 0x09);
        hostile.Type("Example", "ExternalCacheCaller");
        var hostileRoot = hostile.Method("Run", [0, 0, 8], emit: il => {
            il.LoadConstantI4(1); il.Call(findCache); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var hostileInput = scope.OpenInput(files.Write("cache-caller.dll", hostile.Build())).Value!;
        var privateCache = ValueTranslator.TranslateIntegerObjects(scope, hostileInput, hostileRoot, implementation);
        Assert.False(privateCache.Success);
        Assert.Contains(privateCache.Diagnostics, d => d.Id == DiagnosticIds.ExecutionUnsupported
            && d.Message.Contains("Explicit type use requires lexical declaring scope", StringComparison.Ordinal));
        var probes = new[] { ("Text", (Func<int, int, int>)CoreLibraryProbe.Text), ("Fault", CoreLibraryProbe.Fault), ("Formatting", CoreLibraryProbe.Formatting), ("InvariantIdentity", CoreLibraryProbe.InvariantIdentity), ("IntegerHashes", CoreLibraryProbe.IntegerHashes), ("IntegerText", CoreLibraryProbe.IntegerText), ("IntegerHeapText", CoreLibraryProbe.IntegerHeapText), ("IntegerTextIdentity", CoreLibraryProbe.IntegerTextIdentity) };
        var translated = probes.Select(probe => {
            var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CoreLibraryProbe).GetMethod(probe.Item1)!.MetadataToken);
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            using var unapproved = resolver.CreateLibraryCompilationScope(plan);
            var denied = ValueTranslator.TranslateIntegerObjects(unapproved, input, method, implementation);
            Assert.False(denied.Success); Assert.Contains(denied.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
            return (probe.Item1, result.Program!, probe.Item2);
        }).ToArray();
        foreach (var batch in translated.Chunk(3))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, batch);
    }
}
