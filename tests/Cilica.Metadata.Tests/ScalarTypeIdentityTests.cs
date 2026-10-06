using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ScalarTypeIdentityTests
{
    private static readonly string[] Names = ["Primitives", "Enums", "LiteralOnly", "Generic", "Dictionary", "Null"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ScalarTypeIdentityProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Scalar_type_literals_match_boxed_identity_and_remain_nominal(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ScalarTypeIdentityProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData("target")] [InlineData("scalar-fact")] [InlineData("fault-edge")]
    public async Task Both_stages_require_a_valid_scalar_plan_and_fault_edge(string mutation)
    {
        var program = (await Programs.Value)[2]; var flow = program.Flow.Flow.Flow;
        var target = flow.Types.Types.Single(t => t.Source.Name == "Cilica.Fixtures.Execution.IdentityEnum");
        var id = ObjectTypeId.From(target.Source);
        System.Collections.Immutable.ImmutableArray<IrMethod> Mutate(System.Collections.Immutable.ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(statement =>
                statement is IrStatement.Define d && d.Value.Expression is IrExpression.RuntimeTypeLiteral literal && literal.Target == id
                    ? mutation == "target" ? d with { Value = d.Value with { Expression = literal with { Target = id with { Token = 0x02007fff } } } }
                        : mutation == "fault-edge" ? d with { Exceptional = null } : statement
                    : statement)] })] })];
        ValueTypeCatalog Values(ValueTypeCatalog values) => mutation == "scalar-fact"
            ? values with { Types = values.Types.Replace(target, target with { ScalarBox = null }) } : values;
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = Values(flow.Types), Methods = Mutate(flow.Methods) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Values = Values(boxes.Values), Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Mutate(objects.Flow.Methods) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData("UnsupportedValue")] [InlineData("UnsupportedFloat")] [InlineData("UnsupportedOpen")] [InlineData("UnsupportedNullable")]
    public async Task Unsupported_value_and_open_type_tokens_remain_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, library, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ScalarTypeIdentityProbe).Assembly.Location).Value!;
        var root = (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(ScalarTypeIdentityProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, library);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
