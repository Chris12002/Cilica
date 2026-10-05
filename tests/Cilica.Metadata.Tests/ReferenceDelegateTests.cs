using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ReferenceDelegateTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Capture", ReferenceDelegateProbe.Capture), ("Function", ReferenceDelegateProbe.Function),
        ("Interface", ReferenceDelegateProbe.Interface), ("Arrays", ReferenceDelegateProbe.Arrays), ("Null", ReferenceDelegateProbe.Null)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(ReferenceDelegateProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Exact_reference_parameters_are_rooted_and_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray());
    }
    [Fact]
    public async Task Target_parameter_variance_remains_explicitly_unsupported()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ReferenceDelegateProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ReferenceDelegateProbe).GetMethod("UnsupportedVariance")!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("expression, parameter or operand", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("count")] [InlineData("wide")] [InlineData("unknown")]
    public async Task Detached_delegate_parameter_shapes_are_validated(string mutation)
    {
        var program = (await Programs.Value)[1]; var catalog = program.Flow.Flow.Flow.Types.References;
        var declaration = Assert.Single(catalog.Delegates);
        var parameters = mutation switch {
            "count" => declaration.Parameters.RemoveAt(0),
            "wide" => declaration.Parameters.SetItem(0, IrType.Int64),
            _ => declaration.Parameters.SetItem(0, IrType.Reference(catalog.ObjectBase with { Token = 0x0200ffff })) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with { Delegates = catalog.Delegates.Replace(declaration, declaration with { Parameters = parameters }) }));
    }

    [Fact]
    public async Task Detached_dispatch_catalogs_preserve_parameter_shapes_and_calls_require_assignable_arguments()
    {
        var program = (await Programs.Value)[2];
        var dispatch = program.Flow.InterfaceDispatch!;
        var declarations = JsonSerializer.Deserialize<System.Collections.Immutable.ImmutableArray<ObjectDelegateType>>(JsonSerializer.Serialize(dispatch.Types.Delegates));
        var detached = dispatch with { Types = dispatch.Types with { Delegates = declarations } };
        ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { InterfaceDispatch = detached } });
        program = (await Programs.Value)[1]; var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.CallDelegate));
        var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.CallDelegate });
        var call = (IrExpression.CallDelegate)statement.Value.Expression;
        var changed = statement with { Value = statement.Value with { Expression = call with { Arguments = call.Arguments.SetItem(0, call.Arguments[1]) } } };
        var badMethod = method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement, changed) }) };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, badMethod) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
}
