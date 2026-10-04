using System;
using System.Linq;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ScalarBoxedIrTests
{
    [Fact]
    public void Hidden_empty_payload_owner_checks_cannot_bypass_expanded_root_limits()
    {
        var source = BoxedValueIrTests.Model(empty: true); var method = source.Flow.Flow.Methods[0]; var block = method.Blocks[0];
        var statements = block.Statements.AddRange(Enumerable.Range(7, 1024).Select(id => new IrStatement.Define(
            new(id, IrType.Value(source.Flow.Flow.Types.Roots[0]), 0, new IrExpression.UnboxValue(source.Flow.Flow.Types.Roots[0], 1)), IrExceptionTarget.MethodExit)));
        source = source with { Flow = new(source.Flow.Flow with { Methods = [method with { Blocks = [block with { Statements = statements }] }] }) };
        Assert.Equal(3, Assert.Single(ValueRootAssignment.Assign(source)).Slots.Length);
        Assert.Contains("root-slot limit", Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerBoxes(source)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lowering_retains_exact_checks_and_owner_roots_for_empty_payloads(bool empty)
    {
        var source = BoxedValueIrTests.Model(nested: true, empty: empty);
        var lowered = ValueScalarLowering.LowerBoxes(source);
        var method = lowered.Flow.Flow.Flow.Methods[0];
        var checks = method.Blocks[0].Values.Where(v => v.Expression is IrExpression.CheckBox).ToArray();
        Assert.Equal(2, checks.Length);
        var roots = Assert.Single(ObjectRootAssignment.AssignBoxes(lowered));
        Assert.All(checks, check => Assert.Contains(roots.Slots, root => root.Kind == ObjectRootKind.Value && root.Index == check.Id));
        Assert.Equal(2, roots.Safepoints.Length);
        Assert.All(roots.Safepoints, p => Assert.Equal(ObjectSafepointKind.Allocation, p.Kind));
        if (empty)
        {
            Assert.DoesNotContain(method.Blocks[0].Values, v => v.Expression is IrExpression.AddressBoxSlot);
            Assert.All(method.Blocks[0].Values.Where(v => v.Expression is IrExpression.BoxScalars),
                v => Assert.Empty(((IrExpression.BoxScalars)v.Expression).Values));
        }
        Assert.Throws<ArgumentException>(() => ScalarLocationIrValidator.Validate(lowered.Flow));
    }

    [Theory]
    [InlineData("packet_length")]
    [InlineData("packet_type")]
    [InlineData("packet_forward")]
    [InlineData("packet_default")]
    [InlineData("packet_fault")]
    [InlineData("check_fault")]
    [InlineData("check_type")]
    [InlineData("slot_index")]
    [InlineData("slot_type")]
    [InlineData("slot_receiver")]
    [InlineData("slot_fault")]
    public void Scalar_contract_rejects_forged_packets_checks_and_payload_slots(string kind)
    {
        var p = ValueScalarLowering.LowerBoxes(BoxedValueIrTests.Model());
        var method = p.Flow.Flow.Flow.Methods[0]; var block = method.Blocks[0];
        var selected = block.Statements.OfType<IrStatement.Define>().First(d => kind.StartsWith("packet", StringComparison.Ordinal)
            ? d.Value.Expression is IrExpression.BoxScalars : kind.StartsWith("check", StringComparison.Ordinal)
                ? d.Value.Expression is IrExpression.CheckBox : d.Value.Expression is IrExpression.AddressBoxSlot);
        IrExpression changed = (kind, selected.Value.Expression) switch
        {
            ("packet_length", IrExpression.BoxScalars b) => b with { Values = [] },
            ("packet_type", IrExpression.BoxScalars b) => b with { Values = [b.Values[1], b.Values[0]] },
            ("packet_forward", IrExpression.BoxScalars b) => b with { Values = [selected.Value.Id, b.Values[1]] },
            ("packet_default", IrExpression.BoxScalars b) => b with { Values = default },
            ("check_type", IrExpression.CheckBox b) => b with { Type = p.Values.References.ObjectBase },
            ("slot_index", IrExpression.AddressBoxSlot b) => b with { Slot = int.MaxValue },
            ("slot_type", IrExpression.AddressBoxSlot b) => b with { Slot = 1 },
            ("slot_receiver", IrExpression.AddressBoxSlot b) => b with { Receiver = 0 },
            _ => selected.Value.Expression,
        };
        var replacement = selected with { Value = selected.Value with { Expression = changed },
            Exceptional = kind.EndsWith("fault", StringComparison.Ordinal) ? null : selected.Exceptional };
        p = p with { Flow = new(p.Flow.Flow with { Flow = p.Flow.Flow.Flow with { Methods = [method with
            { Blocks = [block with { Statements = block.Statements.Replace(selected, replacement) }] }] } }) };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitBoxes(p));
    }
}
