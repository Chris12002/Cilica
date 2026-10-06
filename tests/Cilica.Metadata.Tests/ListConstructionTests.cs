using System;
using System.Collections.Immutable;
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
public sealed class ListConstructionTests
{
    private static readonly string[] Names = ["Integers", "Lists", "Null", "Custom", "References", "Values", "EmptyValues", "Covariant"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ListConstructionProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Proven_collection_construction_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ListConstructionProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Fact]
    public async Task Construction_requires_grant_and_rejects_unproven_enumerables()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == LibraryIntrinsicCapability.ListCollectionConstruction));
        var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        var input = resolver.OpenInput(typeof(ListConstructionProbe).Assembly.Location).Value!;
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ListConstructionProbe).GetMethod(name)!.MetadataToken);
        using (var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!)
        {
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, Method("Integers"), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        foreach (string name in new[] { "UnsupportedAlias", "UnsupportedIterator", "UnsupportedUnsigned" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, Method(name), implementation);
            Assert.False(result.Success);
            if (name == "UnsupportedAlias") Assert.Contains(result.Diagnostics, d => d.Message.Contains("statically proven ICollection<T>", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("arity")] [InlineData("operand")] [InlineData("scalar")] [InlineData("fault")]
    public async Task Copy_bridge_calls_remain_typed_at_both_detached_stages(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var target = IrMethodId.From(Assert.Single(flow.Methods.Where(m => m.Source.Name.Contains("::CopyCollection", StringComparison.Ordinal))).Source);
        IrMethod Change(IrMethod method) => method with { Blocks = [.. method.Blocks.Select(block => block with { Statements = [.. block.Statements.Select(statement =>
            statement is IrStatement.Define { Value.Expression: IrExpression.Call call } define && call.Target == target
                ? define with { Value = define.Value with { Expression = call with { Arguments = mutation switch {
                    "arity" => [], "operand" => [-1], "scalar" => [block.Values.First(v => v.Type == IrType.Int32).Id], _ => call.Arguments } } },
                    Exceptional = mutation == "fault" ? null : define.Exceptional } : statement)] })] };
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.Call call && call.Target == target);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = [.. flow.Methods.Select(Change)] } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = [.. scalarFlow.Methods.Select(Change)] } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
}
