using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed partial class ScalarBoxConfinementTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        (nameof(ScalarBoxConfinementProbe.Direct), ScalarBoxConfinementProbe.Direct),
        (nameof(ScalarBoxConfinementProbe.Forwarded), ScalarBoxConfinementProbe.Forwarded),
        (nameof(ScalarBoxConfinementProbe.Narrow), ScalarBoxConfinementProbe.Narrow),
        (nameof(ScalarBoxConfinementProbe.Wide), ScalarBoxConfinementProbe.Wide),
        (nameof(ScalarBoxConfinementProbe.EnumCopy), ScalarBoxConfinementProbe.EnumCopy),
        (nameof(ScalarBoxConfinementProbe.NominalTest), ScalarBoxConfinementProbe.NominalTest),
        (nameof(ScalarBoxConfinementProbe.NullableTest), ScalarBoxConfinementProbe.NullableTest),
        (nameof(ScalarBoxConfinementProbe.NullableCopy), ScalarBoxConfinementProbe.NullableCopy),
        (nameof(ScalarBoxConfinementProbe.UnrelatedCast), ScalarBoxConfinementProbe.UnrelatedCast),
        (nameof(ScalarBoxConfinementProbe.UnrelatedTest), ScalarBoxConfinementProbe.UnrelatedTest),
        (nameof(ScalarBoxConfinementProbe.TargetOnly), ScalarBoxConfinementProbe.TargetOnly),
        (nameof(ScalarBoxConfinementProbe.MixedNominal), ScalarBoxConfinementProbe.MixedNominal),
        (nameof(ScalarBoxConfinementProbe.Fault), ScalarBoxConfinementProbe.Fault),
        (nameof(ScalarBoxConfinementProbe.Returned), ScalarBoxConfinementProbe.Returned),
        (nameof(ScalarBoxConfinementProbe.NullableBox), ScalarBoxConfinementProbe.NullableBox),
        (nameof(ScalarBoxConfinementProbe.Local), ScalarBoxConfinementProbe.Local),
        (nameof(ScalarBoxConfinementProbe.Array), ScalarBoxConfinementProbe.Array),
        (nameof(ScalarBoxConfinementProbe.Field), ScalarBoxConfinementProbe.Field),
        (nameof(ScalarBoxConfinementProbe.Recursive), ScalarBoxConfinementProbe.Recursive),
        (nameof(ScalarBoxConfinementProbe.ParameterAddress), ScalarBoxConfinementProbe.ParameterAddress)];
    private static readonly string[] Unsupported = [
        nameof(ScalarBoxConfinementProbe.UnsupportedInterfaceTest),
        nameof(ScalarBoxConfinementProbe.UnsupportedInterfaceCast)];
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);
    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(ScalarBoxConfinementProbe).Assembly.Location).Value!;
        return Probes.Select(p => p.Name).Concat(Unsupported).Append(nameof(ScalarBoxConfinementProbe.AllocationLifetime)).ToDictionary(n => n, n => ValueTranslator.TranslateIntegerObjects(scope, input,
            MetadataTokens.MethodDefinitionHandle(typeof(ScalarBoxConfinementProbe).GetMethod(n)!.MetadataToken & 0xffffff), implementation));
    }
    [Fact]
    public async Task Confined_scalar_boxes_can_coexist_with_independent_interface_operations()
    {
        var results = await Results.Value;
        Assert.All(Probes, p => Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Confined_box_stays_rooted_during_helper_allocation(bool optimized)
    {
        var result = (await Results.Value)[nameof(ScalarBoxConfinementProbe.AllocationLifetime)];
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(nameof(ScalarBoxConfinementProbe.AllocationLifetime), result.Program!, ScalarBoxConfinementProbe.AllocationLifetime)]);
    }
    [Fact]
    public async Task Unknown_escapes_and_interface_uses_remain_diagnostic()
    {
        var results = await Results.Value;
        Assert.All(Unsupported, name => {
            Assert.False(results[name].Success, name);
            Assert.Contains("Scalar box interface casts and dispatch require", Assert.Single(results[name].Diagnostics).Message, StringComparison.Ordinal);
        });
    }
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)]
    [InlineData(true, 0)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Confined_boxes_match_dotnet_with_interfaces(bool optimized, int batch)
    {
        var results = await Results.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Skip(batch * 6).Take(6).Select(p => {
            Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics));
            return (p.Name, results[p.Name].Program!, p.Oracle);
        }).ToArray());
    }
}
