using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed partial class BoxTypeTestLibraryTests
{
    private static ImmutableArray<IrMethod> ChangeTest(ImmutableArray<IrMethod> methods, Func<IrValue, IrValue> change) =>
        [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with {
            Statements = [.. b.Statements.Select(s => s is IrStatement.Define { Value.Expression: IrExpression.IsInstance { BoxedTarget: true } } d
                ? d with { Value = change(d.Value) } : s)] })] })];

    [Fact]
    public async Task Detached_box_tests_require_payload_catalog_and_object_result_at_both_stages()
    {
        var result = (await Results.Value)[nameof(BoxTypeTestProbe.TestOnly)];
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow.Flow.Flow;
        var objectType = flow.Types.References.ObjectBase;
        Func<IrValue, IrValue>[] mutations = [
            v => v with { Type = IrType.Int32 },
            v => v with { Expression = ((IrExpression.IsInstance)v.Expression) with { BoxedTarget = false } },
            v => v with { Expression = ((IrExpression.IsInstance)v.Expression) with { Target = objectType } },
            v => v with { Expression = ((IrExpression.IsInstance)v.Expression) with { Operand = v.Id } },
            v => v with { Expression = new IrExpression.CastClass(objectType, ((IrExpression.IsInstance)v.Expression).Operand) { BoxedTarget = true } }];
        foreach (var change in mutations)
        {
            var invalid = program with { Flow = program.Flow with { Flow = new(flow with { Methods = ChangeTest(flow.Methods, change) }) } };
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        }
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        foreach (var change in mutations)
        {
            var invalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with {
                Flow = new(scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = ChangeTest(scalarFlow.Methods, change) } }) } } };
            Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
        }
    }
}
