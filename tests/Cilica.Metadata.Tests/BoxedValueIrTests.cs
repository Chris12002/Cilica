using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class BoxedValueIrTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), ValueBase = new(Hash, 0x02000003),
        Leaf = new(Hash, 0x02000004), Pair = new(Hash, 0x02000005);
    private static readonly ObjectFieldId Number = new(Leaf, 0x04000001), Reference = new(Leaf, 0x04000002),
        Left = new(Pair, 0x04000003), Right = new(Pair, 0x04000004);
    private static readonly IrType ObjectRef = IrType.Reference(Object);
    private static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    internal static BoxedValueIrProgram Model(bool nested = false, bool empty = false)
    {
        var leaf = new ValueTypeLayout(new("fixture", Hash, Leaf.Token, "Leaf"), TypeAttributes.Public | TypeAttributes.Sealed,
            empty ? [] : [new(Number, "Number", FieldAttributes.Public, new ObjectStorage.Int32()),
                new(Reference, "Reference", FieldAttributes.Public, new ObjectStorage.Reference(Object))]);
        var pair = new ValueTypeLayout(new("fixture", Hash, Pair.Token, "Pair"), TypeAttributes.Public | TypeAttributes.Sealed,
            [new(Left, "Left", FieldAttributes.Public, new ObjectStorage.Value(Leaf)),
                new(Right, "Right", FieldAttributes.Public, new ObjectStorage.Value(Leaf))]);
        var type = nested ? Pair : Leaf; var value = IrType.Value(type);
        var catalog = new ValueTypeCatalog([type], ValueBase, nested ? [leaf, pair] : [leaf],
            new([Object], Object, [new(new("fixture", Hash, Object.Token, "System.Object"), null, TypeAttributes.Public, [])]));
        var entry = new IrMethod(new("fixture", Hash, 0x06000001, "Entry"), [], IrType.Int32,
            [new(0, 0, [], [
                D(0, value, new IrExpression.DefaultValue(type)),
                D(1, ObjectRef, new IrExpression.BoxValue(type, 0), true),
                D(2, value, new IrExpression.UnboxValue(type, 1), true),
                D(3, IrType.Location(value), new IrExpression.UnboxLocation(type, 1), true),
                D(4, ObjectRef, new IrExpression.BoxValue(type, 2), true),
                new IrStatement.StoreLocation(0, 3, 0),
                D(5, value, new IrExpression.LoadLocation(3), true),
                D(6, IrType.Int32, new IrExpression.Constant(0))], new IrReturn(1, 6))], []);
        return new(new(new(IrMethodId.From(entry.Source), [entry], catalog)),
            [new(IrMethodId.From(entry.Source), Leaf, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]);
    }

    private static BoxedValueIrProgram Change(BoxedValueIrProgram program, Func<ImmutableArray<IrStatement>, ImmutableArray<IrStatement>> change)
    {
        var method = program.Flow.Flow.Methods[0];
        return program with { Flow = new(program.Flow.Flow with { Methods = [method with
        { Blocks = [method.Blocks[0] with { Statements = change(method.Blocks[0].Statements) }] }] }) };
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 0)]
    public void Boxes_share_bounded_payload_paths_and_keep_copy_and_borrow_roots_distinct(bool nested, bool empty, int references)
    {
        var program = Model(nested, empty);
        var payload = Assert.Single(BoxedValueIrValidator.PlanPayloads(program));
        Assert.Equal(nested ? Pair : Leaf, payload.Type);
        Assert.Equal(references, payload.References.Length);
        if (nested && !empty)
            Assert.Equal(new[] { Left, Right }, payload.References.Select(r => r.Fields[0]));
        var roots = Assert.Single(ValueRootAssignment.Assign(program));
        Assert.Equal(3 * references + 3, roots.Slots.Length);
        Assert.Equal(3 * payload.Slots.Length + 5, roots.ScalarSlots);
        Assert.Equal(3, Assert.Single(roots.Slots.Where(s => s.LocationOwner)).Index);
        Assert.Equal(new[] { 1, 4 }, roots.Safepoints.Select(p => p.Statement));
        Assert.All(roots.Safepoints, p => Assert.Equal(ObjectSafepointKind.Allocation, p.Kind));
        // Separate box operations remain allocations even for identical/empty input.
        Assert.Equal(2, program.Flow.Flow.Methods[0].Blocks[0].Values.Count(v => v.Expression is IrExpression.BoxValue));
    }

    [Fact]
    public void Null_unbox_operands_are_valid_IR_with_explicit_runtime_fault_edges()
    {
        var program = Change(Model(), s => s.SetItem(1, D(1, IrType.NullReference, new IrExpression.Null())));
        BoxedValueIrValidator.Validate(program);
        Assert.Single(BoxedValueIrValidator.PlanPayloads(program));
    }

    [Fact]
    public void Copy_only_unboxing_derives_payloads_without_any_box_allocation()
    {
        var program = Change(Model(), _ => [D(0, IrType.NullReference, new IrExpression.Null()),
            D(1, IrType.Value(Leaf), new IrExpression.UnboxValue(Leaf, 0), true),
            D(2, IrType.Int32, new IrExpression.Constant(0))]);
        var method = program.Flow.Flow.Methods[0];
        program = program with { Flow = new(program.Flow.Flow with { Methods = [method with
            { Blocks = [method.Blocks[0] with { Terminator = new IrReturn(1, 2) }] }] }) };
        Assert.Equal(Leaf, Assert.Single(BoxedValueIrValidator.PlanPayloads(program)).Type);
        Assert.Empty(Assert.Single(ValueRootAssignment.Assign(program)).Safepoints);
    }

    [Fact]
    public void Borrowed_payloads_can_be_forwarded_to_allocating_static_helpers()
    {
        var program = Model(); var value = IrType.Value(Leaf); var location = IrType.Location(value);
        var helper = new IrMethod(new("fixture", Hash, 0x06000002, "Mutate"), [location], IrType.Void,
            [new(0, 0, [], [D(0, location, new IrExpression.Parameter(0)),
                D(1, value, new IrExpression.LoadLocation(0), true),
                D(2, ObjectRef, new IrExpression.BoxValue(Leaf, 1), true),
                new IrStatement.InitializeLocation(0, 0)], new IrVoidReturn(1))], []);
        program = Change(program, s => s.Insert(4, new IrStatement.Invoke(0, IrMethodId.From(helper.Source), [3])));
        program = program with { Flow = new(program.Flow.Flow with { Methods = program.Flow.Flow.Methods.Add(helper) }),
            Bindings = program.Bindings.Add(new(IrMethodId.From(helper.Source), Leaf, ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)) };
        var roots = ValueRootAssignment.Assign(program);
        Assert.Contains(roots[0].Safepoints, p => p.Kind == ObjectSafepointKind.Call);
        Assert.Contains(roots[1].Slots, s => s.Kind == ObjectRootKind.Parameter && s.LocationOwner);
        Assert.Equal(ObjectSafepointKind.Allocation, Assert.Single(roots[1].Safepoints).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Payload_field_borrows_propagate_readonly_capability(bool readOnly)
    {
        var program = Change(Model(), s => s.SetItem(3,
            D(3, IrType.Location(IrType.Value(Leaf), readOnly), new IrExpression.UnboxLocation(Leaf, 1), true))
            .SetItem(5, D(5, IrType.Location(IrType.Int32, readOnly), new IrExpression.LocationField(Number, 3)))
            .SetItem(6, D(6, IrType.Int32, new IrExpression.LoadLocation(5), true)).RemoveAt(7));
        BoxedValueIrValidator.Validate(program);
        if (readOnly)
        {
            var bad = Change(program, s => s.SetItem(5,
                D(5, IrType.Location(IrType.Int32), new IrExpression.LocationField(Number, 3))));
            Assert.Throws<ArgumentException>(() => BoxedValueIrValidator.Validate(bad));
        }
    }

    [Theory]
    [InlineData("box_fault")]
    [InlineData("copy_fault")]
    [InlineData("borrow_fault")]
    [InlineData("box_reference")]
    [InlineData("box_forward")]
    [InlineData("box_null_type")]
    [InlineData("box_object_type")]
    [InlineData("box_primitive")]
    [InlineData("copy_scalar")]
    [InlineData("borrow_value")]
    [InlineData("copy_as_location")]
    [InlineData("borrow_as_copy")]
    [InlineData("copy_unknown_type")]
    [InlineData("borrow_unknown_type")]
    [InlineData("box_wrong_result")]
    [InlineData("readonly_store")]
    [InlineData("borrow_in_local")]
    [InlineData("borrow_in_join")]
    [InlineData("borrow_return")]
    public void Malformed_boxing_and_escaping_payload_borrows_are_rejected(string kind)
    {
        var program = Model(); var value = IrType.Value(Leaf); var location = IrType.Location(value);
        program = Change(program, s => kind switch
        {
            "box_fault" => s.SetItem(1, ((IrStatement.Define)s[1]) with { Exceptional = null }),
            "copy_fault" => s.SetItem(2, ((IrStatement.Define)s[2]) with { Exceptional = null }),
            "borrow_fault" => s.SetItem(3, ((IrStatement.Define)s[3]) with { Exceptional = null }),
            "box_reference" => s.SetItem(4, D(4, ObjectRef, new IrExpression.BoxValue(Leaf, 1), true)),
            "box_forward" => s.SetItem(1, D(1, ObjectRef, new IrExpression.BoxValue(Leaf, 1), true)),
            "box_null_type" => s.SetItem(1, D(1, ObjectRef, new IrExpression.BoxValue(null!, 0), true)),
            "box_object_type" => s.SetItem(1, D(1, ObjectRef, new IrExpression.BoxValue(Object, 0), true)),
            "box_primitive" => s.SetItem(0, D(0, IrType.Int32, new IrExpression.Constant(0))),
            "copy_scalar" => s.SetItem(2, D(2, value, new IrExpression.UnboxValue(Leaf, 0), true)),
            "borrow_value" => s.SetItem(3, D(3, location, new IrExpression.UnboxLocation(Leaf, 0), true)),
            "copy_as_location" => s.SetItem(2, D(2, location, new IrExpression.UnboxValue(Leaf, 1), true)),
            "borrow_as_copy" => s.SetItem(3, D(3, value, new IrExpression.UnboxLocation(Leaf, 1), true)),
            "copy_unknown_type" => s.SetItem(2, D(2, value, new IrExpression.UnboxValue(Pair, 1), true)),
            "borrow_unknown_type" => s.SetItem(3, D(3, location, new IrExpression.UnboxLocation(Pair, 1), true)),
            "box_wrong_result" => s.SetItem(1, D(1, value, new IrExpression.BoxValue(Leaf, 0), true)),
            "readonly_store" => s.SetItem(3, D(3, IrType.Location(value, true), new IrExpression.UnboxLocation(Leaf, 1), true)),
            _ => s,
        });
        var method = program.Flow.Flow.Methods[0];
        if (kind == "borrow_in_local") method = method with { Locals = [location] };
        if (kind == "borrow_in_join") method = method with { Blocks = [method.Blocks[0] with { Inputs = [location] }] };
        if (kind == "borrow_return") method = method with { ReturnType = location, Blocks = [method.Blocks[0] with { Terminator = new IrReturn(1, 3) }] };
        program = program with { Flow = new(program.Flow.Flow with { Methods = [method] }) };
        Assert.Throws<ArgumentException>(() => BoxedValueIrValidator.PlanPayloads(program));
    }

    [Fact]
    public void Earlier_profiles_and_native_lowering_remain_closed_to_boxing()
    {
        var program = Model();
        Assert.Throws<ArgumentException>(() => ValueIrValidator.Validate(program.Flow.Flow));
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => ValueArrayIrValidator.Validate(new(program.Flow, program.Bindings)));
        Assert.Throws<ArgumentException>(() => ValueObjectIrValidator.Validate(new(program.Flow, program.Bindings)));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerObjects(new(program.Flow, program.Bindings)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Equal_empty_layouts_never_alias_nominal_or_image_scoped_payload_identity(bool otherImage)
    {
        var program = Model(empty: true);
        var other = otherImage ? Leaf with { ContentHash = new string('b', 64) } : Pair;
        var layout = program.Flow.Flow.Types.Types[0];
        var catalog = program.Flow.Flow.Types with { Roots = [Leaf, other], Types = [layout,
            layout with { Source = layout.Source with { ContentHash = other.ContentHash, Token = other.Token } }] };
        program = program with { Flow = new(program.Flow.Flow with { Types = catalog }) };
        // A dynamically wrong box is legal input to unbox; native checking must
        // fault on its exact identity. The result's static type is still exact.
        program = Change(program, s => s.SetItem(2, D(2, IrType.Value(other), new IrExpression.UnboxValue(other, 1), true))
            .SetItem(4, D(4, ObjectRef, new IrExpression.BoxValue(other, 2), true)));
        var plans = BoxedValueIrValidator.PlanPayloads(program);
        Assert.Equal(new[] { Leaf, other }, plans.Select(p => p.Type));
        Assert.All(plans, p => Assert.Empty(p.Slots));
        var wrongBox = Change(program, s => s.SetItem(4, D(4, ObjectRef, new IrExpression.BoxValue(other, 0), true)));
        Assert.Throws<ArgumentException>(() => BoxedValueIrValidator.Validate(wrongBox));
        var wrongCopy = Change(program, s => s.SetItem(2, D(2, IrType.Value(Leaf), new IrExpression.UnboxValue(other, 1), true)));
        Assert.Throws<ArgumentException>(() => BoxedValueIrValidator.Validate(wrongCopy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_boxes_still_consume_the_exact_phase_A_root_budget(bool excess)
    {
        int count = ObjectRootAssignment.MaximumRootsPerMethod + (excess ? 1 : 0);
        var program = Change(Model(empty: true), _ => [D(0, IrType.Value(Leaf), new IrExpression.DefaultValue(Leaf)),
            .. Enumerable.Range(1, count).Select(i => D(i, ObjectRef, new IrExpression.BoxValue(Leaf, 0), true)),
            D(count + 1, IrType.Int32, new IrExpression.Constant(0))]);
        var method = program.Flow.Flow.Methods[0];
        program = program with { Flow = new(program.Flow.Flow with { Methods = [method with
            { Blocks = [method.Blocks[0] with { Terminator = new IrReturn(1, count + 1) }] }] }) };
        if (excess) Assert.Contains("root-slot limit", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program)).Message, StringComparison.Ordinal);
        else Assert.Equal(count, Assert.Single(ValueRootAssignment.Assign(program)).Slots.Length);
    }
}
