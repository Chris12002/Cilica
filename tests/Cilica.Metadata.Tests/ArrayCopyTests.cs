using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ArrayCopyTests
{
    private static readonly string[] Names = ["Integers", "Lists", "Values", "EmptyValues", "CovariantSources", "Downcast", "Interfaces", "Self"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayCopyProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Generic_array_and_list_copies_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ArrayCopyProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Empty_list_backing_type_allows_a_related_covariant_destination(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(ArrayCopyProbe), ["EmptyList"]);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("EmptyList", programs[0], ArrayCopyProbe.EmptyList)]);
    }

    [Fact]
    public async Task Array_copy_requires_each_exact_grant_and_rejects_unsigned_aliases()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ArrayCopyProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArrayCopyProbe).GetMethod("Integers")!.MetadataToken);
        foreach (var capability in new[] { LibraryIntrinsicCapability.ArrayInterfaceCopyTo, LibraryIntrinsicCapability.ArrayCopyCompatible, LibraryIntrinsicCapability.ArrayCopyStore })
        {
            var grant = Assert.Single(artifact.Grants.Where(g => g.Capability == capability));
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] });
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            var result = ValueTranslator.TranslateIntegerObjects(denied, input, method, implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArrayCopyProbe).GetMethod("UnsupportedUnsigned")!.MetadataToken), implementation).Success);
        var b = new ResolutionAssemblyBuilder(new AssemblyIdentity("ExternalCopyCompatibility", new Version(1, 0, 0, 0)));
        var core = b.Reference(implementation.Identity); var owner = b.TypeReference("Cilica.Runtime", "ArrayInterface`1", core);
        byte code = checked((byte)((MetadataTokens.GetRowNumber(owner) << 2) | 1));
        var spec = b.Specification(0x15, 0x12, code, 1, 8);
        var call = b.Member(spec, "CopyCompatible", 0, 2, 2, 0x1d, 0x13, 0, 0x1d, 0x13, 0);
        b.Type("Example", "ExternalCopyCompatibility", baseType: b.TypeReference("System", "Object", core));
        var root = b.Method("Run", [0, 0, 8], emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldnull); il.Call(call); il.OpCode(ILOpCode.Ret); });
        var hostile = resolver.OpenInput(files.Write("external-compatible.dll", b.Build())).Value!;
        var failure = ValueTranslator.TranslateIntegerObjects(scope, hostile, root, implementation);
        Assert.False(failure.Success); Assert.Contains(failure.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Copy_dispatch_recipe_requires_exact_array_index_and_result_packets()
    {
        foreach (var program in (await Programs.Value).Take(3))
        foreach (var plan in new[] { program.Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch! })
        {
            var helpers = plan.Methods.Where(m => m.ArrayHelper && m.Name == "CopyTo").ToArray(); Assert.NotEmpty(helpers);
            foreach (var method in helpers)
            foreach (var changed in new[] {
                method with { Parameters = method.Parameters.SetItem(1, IrType.Reference(plan.Types.ObjectBase)) },
                method with { Parameters = method.Parameters.SetItem(2, IrType.Int64) },
                method with { Parameters = method.Parameters.RemoveAt(2) },
                method with { ReturnType = IrType.Int32 }, method with { Name = "Set" } })
                Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(plan with { Methods = plan.Methods.Replace(method, changed) }));
        }
    }
    [Theory]
    [InlineData("source")] [InlineData("destination")] [InlineData("array")] [InlineData("fault")]
    public async Task Compatibility_operations_validate_both_stages(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        IrMethod Change(IrMethod method) => method with { Blocks = [.. method.Blocks.Select(block => block with { Statements = [.. block.Statements.Select(statement =>
            statement is IrStatement.Define { Value.Expression: IrExpression.ArrayCopyCompatible copy } define
                ? define with { Value = define.Value with { Expression = mutation switch {
                    "source" => copy with { Source = -1 }, "destination" => copy with { Destination = int.MaxValue },
                    "array" => copy with { ArrayType = flow.Types.References.ObjectBase }, _ => copy } },
                    Exceptional = mutation == "fault" ? null : define.Exceptional } : statement)] })] };
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.ArrayCopyCompatible);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = [.. flow.Methods.Select(Change)] } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = [.. scalarFlow.Methods.Select(Change)] } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }

}
