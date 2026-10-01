using System;
using System.Collections.Immutable;
using System.Linq;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectNativePlanningTests
{
    [Theory]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    public void Exact_and_excess_phase_A_frame_bounds_are_checked_before_emission(int count, bool accepted)
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.Run));
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        int existing = entry.Blocks[0].Values.Count(v => v.Type.Kind is IrTypeKind.Reference or IrTypeKind.NullReference)
            + entry.Locals.Count(t => t.Kind == IrTypeKind.Reference);
        var statements = entry.Blocks[0].Statements.ToBuilder(); int next = entry.Blocks[0].Values.Length;
        for (int i = existing; i < count; i++) statements.Add(new IrStatement.Define(new(next++, IrType.NullReference, entry.Blocks[0].Terminator.ILOffset, new IrExpression.Null())));
        var expanded = entry with { Blocks = [entry.Blocks[0] with { Statements = statements.ToImmutable() }] };
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Replace(entry, expanded) } };
        ObjectIrValidator.Validate(program);
        if (accepted)
        {
            var roots = ObjectRootAssignment.Assign(program).Single(p => p.Method == program.Flow.Entry);
            Assert.Equal(count, roots.Slots.Length); Assert.Equal(count, roots.Slots.Select(s => s.Slot).Distinct().Count());
            Assert.NotEmpty(ObjectCppEmitter.EmitProgram(program).Text);
        }
        else Assert.Contains("root-slot limit", Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Structurally_valid_reference_control_flow_requires_later_native_lowering()
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.Run));
        var entry = program.Flow.Methods.Single(m => IrMethodId.From(m.Source) == program.Flow.Entry);
        var first = entry.Blocks[0]; var returned = Assert.IsType<IrReturn>(first.Terminator);
        var second = new IrBlock(1, returned.ILOffset + 1, [IrType.Int32],
            [new IrStatement.Define(new(0, IrType.Int32, returned.ILOffset + 1, new IrExpression.StackInput(0)))], new IrReturn(returned.ILOffset + 2, 0));
        var branched = entry with { Blocks = [first with { Terminator = new IrJump(returned.ILOffset, new(1, [returned.Value])) }, second] };
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Replace(entry, branched) } };
        ObjectIrValidator.Validate(program);
        Assert.Contains("straight-line", Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_emission_revalidates_forged_object_IR()
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.Run));
        var ctor = program.Bindings.First(b => b.Kind == ObjectMethodKind.Constructor);
        var forged = program with { Bindings = program.Bindings.Replace(ctor, ctor with { Kind = ObjectMethodKind.Static }) };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(forged));
        Assert.Throws<ArgumentNullException>(() => ObjectRootAssignment.Assign(null!));
    }
}
