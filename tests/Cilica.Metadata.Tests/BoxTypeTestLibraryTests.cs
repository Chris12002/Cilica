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

public sealed partial class BoxTypeTestLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        (nameof(BoxTypeTestProbe.Int), BoxTypeTestProbe.Int),
        (nameof(BoxTypeTestProbe.UInt), BoxTypeTestProbe.UInt),
        (nameof(BoxTypeTestProbe.Long), BoxTypeTestProbe.Long),
        (nameof(BoxTypeTestProbe.ULong), BoxTypeTestProbe.ULong),
        (nameof(BoxTypeTestProbe.Byte), BoxTypeTestProbe.Byte),
        (nameof(BoxTypeTestProbe.SByte), BoxTypeTestProbe.SByte),
        (nameof(BoxTypeTestProbe.Short), BoxTypeTestProbe.Short),
        (nameof(BoxTypeTestProbe.UShort), BoxTypeTestProbe.UShort),
        (nameof(BoxTypeTestProbe.Char), BoxTypeTestProbe.Char),
        (nameof(BoxTypeTestProbe.Bool), BoxTypeTestProbe.Bool),
        (nameof(BoxTypeTestProbe.Enum), BoxTypeTestProbe.Enum),
        (nameof(BoxTypeTestProbe.EnumIdentity), BoxTypeTestProbe.EnumIdentity),
        (nameof(BoxTypeTestProbe.ReferencePayload), BoxTypeTestProbe.ReferencePayload),
        (nameof(BoxTypeTestProbe.ClosedValue), BoxTypeTestProbe.ClosedValue),
        (nameof(BoxTypeTestProbe.TestOnly), BoxTypeTestProbe.TestOnly),
        (nameof(BoxTypeTestProbe.NullOnly), BoxTypeTestProbe.NullOnly),
        (nameof(BoxTypeTestProbe.EmptyPayload), BoxTypeTestProbe.EmptyPayload),
        (nameof(BoxTypeTestProbe.NominalMismatch), BoxTypeTestProbe.NominalMismatch)];
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);
    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(BoxTypeTestProbe).Assembly.Location).Value!;
        return Probes.Select(p => p.Name).Append(nameof(BoxTypeTestProbe.UnsupportedFilter)).Append(nameof(BoxTypeTestProbe.GuidValue)).ToDictionary(n => n, n => ValueTranslator.TranslateIntegerObjects(scope, input,
            MetadataTokens.MethodDefinitionHandle(typeof(BoxTypeTestProbe).GetMethod(n)!.MetadataToken & 0xffffff), implementation));
    }
    [Fact]
    public async Task Boxed_type_tests_translate_including_descriptors_used_only_as_targets()
    {
        var results = await Results.Value;
        Assert.All(Probes, p => Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Guid_type_tests_match_dotnet(bool optimized)
    {
        var result = (await Results.Value)[nameof(BoxTypeTestProbe.GuidValue)];
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(nameof(BoxTypeTestProbe.GuidValue), result.Program!, BoxTypeTestProbe.GuidValue)]);
    }
    [Fact]
    public async Task General_filters_remain_diagnostic()
    {
        var result = (await Results.Value)[nameof(BoxTypeTestProbe.UnsupportedFilter)];
        Assert.False(result.Success);
        Assert.Contains("General exception filters require", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public async Task Boxed_type_tests_match_dotnet(bool optimized, int batch)
    {
        var results = await Results.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Skip(batch * 6).Take(6).Select(p => {
            Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics));
            return (p.Name, results[p.Name].Program!, p.Oracle);
        }).ToArray());
    }
}
