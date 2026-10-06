using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ReferenceResultDelegateTests
{
    private static readonly string[] Names = ["Fresh", "InstanceCycle", "Null", "Fault", "NullReceiver", "String", "Array", "Interface", "Object", "Generic", "Dictionary", "SignatureOnlyArray", "ReferenceArgument"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ReferenceResultDelegateProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Reference_results_match_dotnet_with_forced_collection(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ReferenceResultDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 4).Take(group == 2 ? 5 : 4).ToArray());
    }
    [Fact]
    public async Task Unsupported_result_signatures_and_target_covariance_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ReferenceResultDelegateProbe).Assembly.Location).Value!;
        foreach (string suffix in new[] { "Covariance", "Wide", "Value", "ByRef", "Recursive" })
        {
            var method = typeof(ReferenceResultDelegateProbe).GetMethod("Unsupported" + suffix)!;
            var result = ValueTranslator.TranslateIntegerObjects(scope, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.MetadataToken), implementation);
            Assert.False(result.Success, suffix);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(suffix == "Covariance" ? "expression, parameter or operand"
                : suffix == "Recursive" ? "Recursive delegate signatures" : suffix == "ByRef" ? "byref locals/returns" : "Delegate requires", StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData("wide")] [InlineData("unknown")] [InlineData("null")] [InlineData("location")]
    public async Task Detached_return_types_are_validated(string mutation)
    {
        var program = (await Programs.Value)[0]; var catalog = program.Flow.Flow.Flow.Types.References;
        var declaration = Assert.Single(catalog.Delegates);
        var result = mutation switch {
            "wide" => IrType.Int64, "unknown" => IrType.Reference(catalog.ObjectBase with { Token = 0x0200ffff }),
            "location" => IrType.Location(IrType.Int32), _ => IrType.NullReference };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with { Delegates = catalog.Delegates.Replace(declaration, declaration with { ReturnType = result }) }));
    }
    [Fact]
    public async Task Serialized_return_types_round_trip_and_dispatch_catalogs_must_agree()
    {
        var program = (await Programs.Value)[7]; var dispatch = program.Flow.InterfaceDispatch!;
        var declarations = JsonSerializer.Deserialize<ImmutableArray<ObjectDelegateType>>(JsonSerializer.Serialize(dispatch.Types.Delegates));
        var detached = dispatch with { Types = dispatch.Types with { Delegates = declarations } };
        ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { InterfaceDispatch = detached } });
        var declaration = Assert.Single(declarations);
        Assert.Equal(IrTypeKind.Reference, declaration.ReturnType.Kind);
        detached = detached with { Types = detached.Types with { Delegates = declarations.Replace(declaration, declaration with { ReturnType = IrType.Int32 }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { InterfaceDispatch = detached } }));
    }
    [Fact]
    public async Task Detached_capture_and_call_cannot_change_return_identity()
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var declaration = Assert.Single(flow.Types.References.Delegates);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with {
            Types = flow.Types with { References = flow.Types.References with { Delegates = [declaration with { ReturnType = IrType.Reference(flow.Types.References.ObjectBase) }] } } } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var method = flow.Methods.First(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.CallDelegate)));
        var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.CallDelegate));
        var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.CallDelegate });
        var changed = statement with { Value = statement.Value with { Type = IrType.Int32 } };
        var badMethod = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement, changed) }) };
        invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, badMethod) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
}
