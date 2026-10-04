using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ScalarLocationIrTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_location_planning_enforces_exact_and_excess_owner_and_storage_budgets(bool owners)
    {
        var model = NativeScalarLocationTests.Model().Flow;
        var entry = model.Flow.Methods.Single(m => m.Source.Name == "Execute");
        var objectType = model.Flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == model.Flow.Types.ObjectBase);
        var catalog = new ObjectTypeCatalog([model.Flow.Types.ObjectBase], model.Flow.Types.ObjectBase, [objectType]);
        ScalarLocationIrProgram Program(int count)
        {
            var statements = ImmutableArray.CreateBuilder<IrStatement>();
            for (int i = 0; i < count; ++i)
                statements.Add(NativeScalarLocationTests.D(i, owners ? IrType.Location(IrType.Int32) : IrType.Int32,
                    owners ? new IrExpression.AddressLocal(0) : new IrExpression.Constant(0)));
            if (owners) statements.Add(NativeScalarLocationTests.D(count, IrType.Int32, new IrExpression.Constant(0)));
            var method = entry with { Parameters = [IrType.Int32], Locals = owners ? [IrType.Int32] : [],
                Blocks = [new(0, 0, [], statements.ToImmutable(), new IrReturn(1, owners ? count : 0))] };
            return new(new(new(model.Flow.Entry, [method], catalog), [model.Bindings.Single(b => b.Method == model.Flow.Entry)]));
        }
        int limit = owners ? ObjectRootAssignment.MaximumRootsPerMethod : ValueRootAssignment.MaximumScalarSlots - 1;
        Assert.Single(ObjectRootAssignment.AssignLocations(Program(limit)));
        string error = Assert.Throws<ArgumentException>(() => ObjectRootAssignment.AssignLocations(Program(limit + 1))).Message;
        Assert.Contains(owners ? "root-slot limit" : "expanded storage limit", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Scalar_profile_requires_explicit_entry_points_and_roots_each_carrier()
    {
        var program = NativeScalarLocationTests.Model();
        ScalarLocationIrValidator.Validate(program);
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program.Flow));
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program.Flow));
        var plans = ObjectRootAssignment.AssignLocations(program);
        var entry = program.Flow.Flow.Methods.Single(m => m.Source.Name == "Execute");
        var plan = plans.Single(p => p.Method == IrMethodId.From(entry.Source));
        foreach (var value in entry.Blocks[0].Values.Where(v => v.Type.Kind == IrTypeKind.ManagedLocation))
            Assert.Contains(plan.Slots, s => s.Kind == ObjectRootKind.Value && s.Block == 0 && s.Index == value.Id && s.LocationOwner);
        Assert.Contains(plan.Safepoints, s => s.Kind == ObjectSafepointKind.Allocation);
        Assert.Contains(plan.Safepoints, s => s.Kind == ObjectSafepointKind.Call);
        Assert.Throws<ArgumentNullException>(() => ObjectCppEmitter.EmitLocations(null!));
    }

    [Theory]
    [InlineData("private_field")]
    [InlineData("readonly_field")]
    [InlineData("missing_fault")]
    [InlineData("escape")]
    [InlineData("return")]
    [InlineData("entry_borrow")]
    [InlineData("reference_variance")]
    [InlineData("readonly_write")]
    public void Scalar_profile_preserves_access_lifetime_type_and_fault_restrictions(string mutation)
    {
        var wrapper = NativeScalarLocationTests.Model(); var program = wrapper.Flow; var flow = program.Flow;
        var entry = flow.Methods.Single(m => m.Source.Name == "Execute"); var originalEntry = entry;
        var statements = entry.Blocks[0].Statements;
        int FieldAddress(bool readOnly) => Enumerable.Range(0, statements.Length).Single(i => statements[i] is IrStatement.Define
            { Value.Expression: IrExpression.AddressField } d && d.Value.Type.ReadOnly == readOnly);
        switch (mutation)
        {
            case "private_field":
                var address = (IrExpression.AddressField)((IrStatement.Define)statements[FieldAddress(false)]).Value.Expression;
                var parent = flow.Types.Types.Single(t => ObjectTypeId.From(t.Source) == address.Field.Owner);
                var field = parent.Fields.Single(f => f.Id == address.Field);
                parent = parent with { Fields = parent.Fields.Replace(field, field with { Attributes = FieldAttributes.Private }) };
                flow = flow with { Types = flow.Types with { Types = flow.Types.Types.Select(t => t.Source == parent.Source ? parent : t).ToImmutableArray() } };
                break;
            case "readonly_field":
                int index = FieldAddress(true); var definition = (IrStatement.Define)statements[index];
                statements = statements.SetItem(index, definition with { Value = definition.Value with { Type = IrType.Location(IrType.Int32) } }); break;
            case "missing_fault":
                index = FieldAddress(false); definition = (IrStatement.Define)statements[index];
                statements = statements.SetItem(index, definition with { Exceptional = null }); break;
            case "escape": entry = entry with { Locals = entry.Locals.Add(IrType.Location(IrType.Int32)) }; break;
            case "return": entry = entry with { ReturnType = IrType.Location(IrType.Int32) }; break;
            case "entry_borrow": entry = entry with { Parameters = [IrType.Location(IrType.Int32)] }; break;
            case "reference_variance":
                var replace = flow.Methods.Single(m => m.Source.Name == "Replace");
                var type = IrType.Location(IrType.Reference(flow.Types.ObjectBase));
                replace = replace with { Parameters = replace.Parameters.SetItem(0, type), Blocks = [replace.Blocks[0] with
                    { Statements = replace.Blocks[0].Statements.SetItem(0, NativeScalarLocationTests.D(0, type, new IrExpression.Parameter(0))) }] };
                flow = flow with { Methods = flow.Methods.Select(m => m.Source.Name == "Replace" ? replace : m).ToImmutableArray() }; break;
            case "readonly_write":
                var mutate = flow.Methods.Single(m => m.Source.Name == "Mutate"); type = IrType.Location(IrType.Int32, true);
                mutate = mutate with { Parameters = [type], Blocks = [mutate.Blocks[0] with
                    { Statements = mutate.Blocks[0].Statements.SetItem(0, NativeScalarLocationTests.D(0, type, new IrExpression.Parameter(0))) }] };
                flow = flow with { Methods = flow.Methods.Select(m => m.Source.Name == "Mutate" ? mutate : m).ToImmutableArray() }; break;
        }
        entry = entry with { Blocks = [entry.Blocks[0] with { Statements = statements }] };
        flow = flow with { Methods = flow.Methods.Replace(originalEntry, entry) };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitLocations(new(program with { Flow = flow })));
    }
}
