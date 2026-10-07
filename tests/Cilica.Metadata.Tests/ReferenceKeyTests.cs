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
public sealed class ReferenceKeyTests
{
    private static readonly string[] Names = ["Identity", "Nulls", "Overrides", "Typed", "StaticBase", "Inherited", "Faults", "NullDictionary"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ReferenceKeyProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Reference_equality_hashing_and_null_order_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ReferenceKeyProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("Vectors")] [InlineData("ObjectAliases")]
    public async Task Special_runtime_receivers_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ReferenceKeyProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ReferenceKeyProbe).GetMethod(name)!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Object-slot virtual calls require a statically known ordinary class receiver", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Previously_rejected_ordinary_reference_searches_match_dotnet(bool optimized)
    {
        var cases = new[] { (typeof(ArraySearchProbe), "UnsupportedReference"), (typeof(ListSearchProbe), "UnsupportedReference"),
            (typeof(DictionaryProbe), "UnsupportedReferenceKey"), (typeof(DictionaryViewProbe), "UnsupportedValuesContains") };
        foreach (var (fixture, name) in cases)
        {
            var program = (await CoreLibraryBuildTests.TranslateFixtures(fixture, [name]))[0];
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(name, program, fixture.GetMethod(name)!.CreateDelegate<Func<int,int,int>>())]);
        }
    }
    [Fact]
    public async Task Receiver_type_proof_cannot_be_forged_before_or_after_lowering()
    {
        var program = (await Programs.Value)[1]; var flow = program.Flow.Flow.Flow;
        IrMethod Retag(IrMethod method, ObjectTypeId objectType)
        {
            var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.LoadLocation && v.Type.Kind == IrTypeKind.Reference));
            var statement = block.Statements.OfType<IrStatement.Define>().First(s => s.Value.Expression is IrExpression.LoadLocation && s.Value.Type.Kind == IrTypeKind.Reference);
            return method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement,
                statement with { Value = statement.Value with { Type = IrType.Reference(objectType) } }) }) };
        }
        var helper = flow.Methods.First(m => m.Source.Name.Contains("ReferenceKeyOperations", StringComparison.Ordinal) && m.Source.Name.EndsWith("::Equal", StringComparison.Ordinal));
        var invalid = program with { Flow = program.Flow with { Flow = new(flow with { Methods = flow.Methods.Replace(helper, Retag(helper, flow.Types.References.ObjectBase)) }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var objects = scalar.Flow.Flow.Flow.Flow;
        var scalarHelper = objects.Flow.Methods.Single(m => m.Source == helper.Source);
        var changed = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(scalarHelper, Retag(scalarHelper, objects.Flow.Types.ObjectBase)) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = new(changed) } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData(LibraryIntrinsicCapability.ClosedKeyEqual)] [InlineData(LibraryIntrinsicCapability.ClosedKeyHash)]
    public async Task Reference_keys_retain_exact_intrinsic_grants(LibraryIntrinsicCapability capability)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts); var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == capability));
        var bytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;
        var input = scope.OpenInput(typeof(ReferenceKeyProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ReferenceKeyProbe).GetMethod("Typed")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
    }
}
