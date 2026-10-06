using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed partial class ScalarBoxConfinementTests
{
    private static IrMethod EscapeTestAlias(IrMethod method, ObjectTypeId objectType)
    {
        var block = Assert.Single(method.Blocks);
        var statement = Assert.Single(block.Statements.OfType<IrStatement.Define>(), d => d.Value.Expression is IrExpression.IsInstance or IrExpression.CastClass);
        return method with { Locals = method.Locals.Add(IrType.Reference(objectType)), Blocks = [block with {
            Statements = block.Statements.Insert(block.Statements.IndexOf(statement) + 1,
                new IrStatement.StoreLocal(statement.Value.ILOffset, method.Locals.Length, statement.Value.Id)) }] };
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Detached_validation_follows_exact_test_aliases_before_and_after_lowering(bool cast)
    {
        var result = (await Results.Value)[nameof(ScalarBoxConfinementProbe.MixedNominal)];
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow.Flow.Flow;
        var helper = flow.Methods.Single(m => m.Source.Name.EndsWith("::IsInteger", StringComparison.Ordinal));
        if (cast)
        {
            helper = helper with { Blocks = [.. helper.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s =>
                s is IrStatement.Define { Value.Expression: IrExpression.IsInstance test } d
                    ? d with { Value = d.Value with { Expression = new IrExpression.CastClass(test.Target, test.Operand) { BoxedTarget = true } } } : s)] })] };
            flow = flow with { Methods = [.. flow.Methods.Select(m => m.Source == helper.Source ? helper : m)] };
            program = program with { Flow = program.Flow with { Flow = new(flow) } };
        }
        // The checked-alias model itself is valid; the added escape must be the rejection.
        ManagedExceptionIrValidator.Validate(program);
        var invalid = program with { Flow = program.Flow with { Flow = new(flow with {
            Methods = flow.Methods.Replace(helper, EscapeTestAlias(helper, flow.Types.References.ObjectBase)) }) } };
        Assert.Contains("proven confined box uses", Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid)).Message, StringComparison.Ordinal);

        var scalar = ValueScalarLowering.LowerExceptions(program);
        var objects = scalar.Flow.Flow.Flow.Flow;
        var scalarHelper = objects.Flow.Methods.Single(m => m.Source == helper.Source);
        var changed = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(scalarHelper,
            EscapeTestAlias(scalarHelper, objects.Flow.Types.ObjectBase)) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = new(changed) } } };
        Assert.Contains("proven confined box uses", Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar)).Message, StringComparison.Ordinal);
    }
    private static IrMethod ForgeInterfaceParameter(IrMethod method, ObjectTypeId contract)
    {
        var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.Parameter { Index: 0 }));
        var statement = block.Statements.OfType<IrStatement.Define>().First(d => d.Value.Expression is IrExpression.Parameter { Index: 0 });
        return method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement,
            statement with { Value = statement.Value with { Type = IrType.Reference(contract) } }) }) };
    }
    [Fact]
    public async Task Interface_receiver_proof_cannot_be_forged_by_retagging_an_object_parameter()
    {
        var program = (await Results.Value)[nameof(ScalarBoxConfinementProbe.NominalTest)].Program!;
        var flow = program.Flow.Flow.Flow;
        var contract = ObjectTypeId.From(flow.Types.References.Interfaces.Single(i => i.Source.Name.EndsWith(".IBoxCall", StringComparison.Ordinal)).Source);
        var helper = flow.Methods.Single(m => m.Source.Name.EndsWith("::IsInteger", StringComparison.Ordinal));
        var invalid = program with { Flow = program.Flow with { Flow = new(flow with {
            Methods = flow.Methods.Replace(helper, ForgeInterfaceParameter(helper, contract)) }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var objects = scalar.Flow.Flow.Flow.Flow;
        var scalarHelper = objects.Flow.Methods.Single(m => m.Source == helper.Source);
        var changed = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(scalarHelper, ForgeInterfaceParameter(scalarHelper, contract)) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = new(changed) } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }

}
