using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

// The ledger is deliberately an expected-gap baseline, not a compatibility claim.
// Advance one entry with its implementation and native evidence; never accept any error.
public sealed class CompatibilityGapTests
{
    private static readonly JsonElement Ledger = ReadLedger();
    private static readonly JsonElement[] PublicProbes = Ledger.GetProperty("probes").EnumerateArray()
        .Where(p => p.GetProperty("publicFixture").ValueKind == JsonValueKind.String).ToArray();
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);

    private static JsonElement ReadLedger()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "compatibility-gap-ledger.json")));
        void UniqueKeys(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    Assert.True(names.Add(property.Name), "Duplicate compatibility ledger key: " + property.Name);
                    UniqueKeys(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) UniqueKeys(item);
        }
        UniqueKeys(document.RootElement);
        return document.RootElement.Clone();
    }

    public static TheoryData<string, string, string> KnownGaps
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var probe in PublicProbes.Where(p => p.GetProperty("expectedDiagnostic").ValueKind != JsonValueKind.Null))
            {
                var diagnostic = probe.GetProperty("expectedDiagnostic");
                data.Add(probe.GetProperty("publicFixture").GetString()!, diagnostic.GetProperty("id").GetString()!,
                    diagnostic.GetProperty("contains").GetString()!);
            }
            return data;
        }
    }

    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        string reference = LibraryBuildProducerTests.Request(files, CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single();
        Assert.Equal(Ledger.GetProperty("audit").GetProperty("referenceSha256").GetString(),
            Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(reference))));
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var manifest = await File.ReadAllBytesAsync(built.ManifestPath);
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, manifest, built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(CompatibilityGapProbe).Assembly.Location).Value!;
        var results = new Dictionary<string, ManagedExceptionTranslationResult>(StringComparer.Ordinal);
        foreach (var probe in PublicProbes)
        {
            string name = probe.GetProperty("publicFixture").GetString()!;
            var method = typeof(CompatibilityGapProbe).GetMethod(name)!;
            results.Add(name, ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken), implementation));
        }
        return results;
    }

    [Fact]
    public void Every_authored_probe_has_an_explicit_ledger_expectation()
    {
        var methods = typeof(CompatibilityGapProbe).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();
        var entries = PublicProbes.Select(p => p.GetProperty("publicFixture").GetString()!).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(methods, entries);
        foreach (var probe in PublicProbes)
        {
            Assert.True(Ledger.GetProperty("slices").TryGetProperty(probe.GetProperty("slice").GetString()!, out _));
            Assert.Equal(probe.GetProperty("expectedDiagnostic").ValueKind == JsonValueKind.Null,
                probe.GetProperty("status").GetString()!.StartsWith("native-validated", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(KnownGaps))]
    public async Task Known_gap_preserves_its_specific_diagnostic(string name, string id, string message)
    {
        var result = (await Results.Value)[name];
        Assert.False(result.Success, name + " advanced: add native evidence and deliberately update its ledger entry.");
        Assert.Null(result.Program);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.True(id == diagnostic.Id, name + ": " + diagnostic);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Location));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Positive_controls_match_dotnet_with_forced_collection(bool optimized)
    {
        var results = await Results.Value;
        var controls = PublicProbes.Where(p => p.GetProperty("expectedDiagnostic").ValueKind == JsonValueKind.Null).Select(p =>
        {
            string name = p.GetProperty("publicFixture").GetString()!;
            var result = results[name];
            Assert.True(result.Success, name + ": " + string.Join("; ", result.Diagnostics));
            var oracle = typeof(CompatibilityGapProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>();
            return (name, result.Program!, oracle);
        }).ToArray();
        // Authored probes run in the test oracle; metadata translation never invokes them.
        // This selected bounded core uses fixed invariant numeric text (AC7b.2).
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            // Roslyn caches the noncapturing Where predicate in traced static fields.
            // Keep the stricter no-extra-roots check for all other controls.
            // Keep each native compilation within the existing 30-second subprocess budget.
            foreach (var batch in controls.Where(p => p.name != "Probe_LinqWhere" && p.name != "Probe_Utf8").Chunk(6))
                await IntegerObjectTranslationTests.VerifyTranslated(optimized, batch);
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, controls.Where(p => p.name == "Probe_LinqWhere").ToArray(), persistentRoots: true);
            // The owned UTF-8 encoder contributes exactly one traced static singleton.
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, controls.Where(p => p.name == "Probe_Utf8").ToArray(), expectedStaticRoots: 1);
        }
        finally { CultureInfo.CurrentCulture = savedCulture; }
    }
}
