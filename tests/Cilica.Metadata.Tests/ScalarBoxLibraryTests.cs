using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ScalarBoxLibraryTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        (nameof(ScalarBoxProbe.Int), ScalarBoxProbe.Int),
        (nameof(ScalarBoxProbe.UInt), ScalarBoxProbe.UInt),
        (nameof(ScalarBoxProbe.Long), ScalarBoxProbe.Long),
        (nameof(ScalarBoxProbe.ULong), ScalarBoxProbe.ULong),
        (nameof(ScalarBoxProbe.Byte), ScalarBoxProbe.Byte),
        (nameof(ScalarBoxProbe.SByte), ScalarBoxProbe.SByte),
        (nameof(ScalarBoxProbe.Short), ScalarBoxProbe.Short),
        (nameof(ScalarBoxProbe.UShort), ScalarBoxProbe.UShort),
        (nameof(ScalarBoxProbe.Char), ScalarBoxProbe.Char),
        (nameof(ScalarBoxProbe.Bool), ScalarBoxProbe.Bool),
        (nameof(ScalarBoxProbe.Enum), ScalarBoxProbe.Enum),
        (nameof(ScalarBoxProbe.EnumCompatibility), ScalarBoxProbe.EnumCompatibility),
        (nameof(ScalarBoxProbe.ExactNullableEnum), ScalarBoxProbe.ExactNullableEnum),
        (nameof(ScalarBoxProbe.NominalIdentity), ScalarBoxProbe.NominalIdentity),
        (nameof(ScalarBoxProbe.WrongScalarTypes), ScalarBoxProbe.WrongScalarTypes),
        (nameof(ScalarBoxProbe.ArrayLifetime), ScalarBoxProbe.ArrayLifetime),
        (nameof(ScalarBoxProbe.WideBits), ScalarBoxProbe.WideBits),
        ("RawSByteBox", (a, b) => unchecked((sbyte)a))];
    private static readonly Lazy<Task<Dictionary<string, ManagedExceptionTranslationResult>>> Results = new(Translate);
    private static async Task<Dictionary<string, ManagedExceptionTranslationResult>> Translate()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = resolver.OpenInput(typeof(ScalarBoxProbe).Assembly.Location).Value!;
        var results = Probes.Select(p => p.Name).Where(n => n != "RawSByteBox")
            .Concat([nameof(ScalarBoxProbe.UnsupportedInterfaceTest), nameof(ScalarBoxProbe.UnsupportedInterfaceCast)])
            .ToDictionary(n => n, n => ValueTranslator.TranslateIntegerObjects(scope, input,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ScalarBoxProbe).GetMethod(n)!.MetadataToken), implementation));
        var builder = new ResolutionAssemblyBuilder(new("RawScalarBox", new Version(1, 0, 0, 0)));
        var scalar = builder.TypeReference("System", "SByte", builder.Reference(implementation.Identity));
        builder.Type("Test", "Raw");
        var root = builder.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(0); il.OpCode(ILOpCode.Box); il.Token(scalar); il.OpCode(ILOpCode.Unbox_any); il.Token(scalar); il.OpCode(ILOpCode.Ret);
        });
        var rawUnbox = builder.Method("Borrow", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Unbox); il.Token(scalar); il.OpCode(ILOpCode.Pop); il.LoadArgument(0); il.OpCode(ILOpCode.Ret);
        });
        var raw = scope.OpenInput(files.Write("raw-box.dll", builder.Build())).Value!;
        results.Add("RawSByteBox", ValueTranslator.TranslateIntegerObjects(scope, raw, root, implementation));
        results.Add("RawUnbox", ValueTranslator.TranslateIntegerObjects(scope, raw, rawUnbox, implementation));
        return results;
    }
    [Fact]
    public async Task Scalar_boxes_translate_with_nominal_descriptors()
    {
        var results = await Results.Value;
        Assert.All(Probes, p => Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Scalar_and_nullable_boxes_match_dotnet(bool optimized)
    {
        var results = await Results.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select(p => {
            Assert.True(results[p.Name].Success, p.Name + ": " + string.Join("; ", results[p.Name].Diagnostics));
            return (p.Name, results[p.Name].Program!, p.Oracle);
        }).ToArray());
    }
    [Fact]
    public async Task Incomplete_interfaces_and_raw_scalar_borrows_stay_diagnostic()
    {
        var results = await Results.Value;
        foreach (string name in new[] { nameof(ScalarBoxProbe.UnsupportedInterfaceTest), nameof(ScalarBoxProbe.UnsupportedInterfaceCast) })
        {
            Assert.False(results[name].Success);
            Assert.Contains("Scalar box interface casts and dispatch require", Assert.Single(results[name].Diagnostics).Message, StringComparison.Ordinal);
        }
        Assert.False(results["RawUnbox"].Success);
        Assert.Contains("Raw scalar unbox requires", Assert.Single(results["RawUnbox"].Diagnostics).Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Detached_scalar_descriptors_cannot_be_forged_into_managed_inline_layouts()
    {
        var result = (await Results.Value)[nameof(ScalarBoxProbe.Int)]; Assert.True(result.Success);
        var flow = result.Program!.Flow.Flow.Flow;
        var catalog = flow.Types;
        var scalar = catalog.Types.Single(t => t.ScalarBox == PrimitiveTypeCode.Int32);
        var id = ObjectTypeId.From(scalar.Source);
        Assert.Single(ValueLayoutPlanner.Build(catalog).Single(p => p.Type == id).Slots);
        foreach (var mutated in new[] {
            scalar with { ScalarBox = PrimitiveTypeCode.Single },
            scalar with { Fields = [new(new(id, 0x0400ffff), "forged", FieldAttributes.Public, new ObjectStorage.Int32())] } })
            Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog with { Types = catalog.Types.Replace(scalar, mutated) }));
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var changed = entry with { Parameters = entry.Parameters.SetItem(0, IrType.Value(id)) };
        var program = result.Program with { Flow = result.Program.Flow with { Flow = new(flow with { Methods = flow.Methods.Replace(entry, changed) }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
    }

}
