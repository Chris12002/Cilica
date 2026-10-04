using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ManagedLocationIrTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), ValueBase = new(Hash, 0x02000003), Pair = new(Hash, 0x02000004),
        Outer = new(Hash, 0x02000005), Box = new(Hash, 0x02000006);
    private static readonly IrType PairType = IrType.Value(Pair), PairLocation = IrType.Location(PairType), IntLocation = IrType.Location(IrType.Int32);
    private static readonly ObjectFieldId Number = new(Pair, 0x04000001), Reference = new(Pair, 0x04000002),
        Nested = new(Outer, 0x04000003), BoxReference = new(Box, 0x04000004), BoxNumber = new(Box, 0x04000005);
    private static ValueTypeCatalog Catalog(bool nested = false, bool heap = false, bool readOnly = false)
    {
        var types = ImmutableArray.Create(new ValueTypeLayout(new("fixture", Hash, Pair.Token, "Pair"), TypeAttributes.Public | TypeAttributes.Sealed,
            [new(Number, "Number", FieldAttributes.Public, new ObjectStorage.Int32()),
             new(Reference, "Reference", FieldAttributes.Public, new ObjectStorage.Reference(Object))]));
        if (nested) types = types.Add(new(new("fixture", Hash, Outer.Token, "Outer"), TypeAttributes.Public | TypeAttributes.Sealed,
            [new(Nested, "Nested", FieldAttributes.Public | (readOnly ? FieldAttributes.InitOnly : 0), new ObjectStorage.Value(Pair))]));
        var references = ImmutableArray.Create(new ObjectTypeLayout(new("fixture", Hash, Object.Token, "System.Object"), null, TypeAttributes.Public, []));
        if (heap) references = references.Add(new(new("fixture", Hash, Box.Token, "Box"), Object, TypeAttributes.Public,
            [new(BoxReference, "Reference", FieldAttributes.Public, new ObjectStorage.Reference(Object)),
             new(BoxNumber, "Number", FieldAttributes.Public | FieldAttributes.InitOnly, new ObjectStorage.Int32())]));
        return new([nested ? Outer : Pair], ValueBase, types, new([heap ? Box : Object], Object, references));
    }
    private static IrStatement.Define D(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements,
        ImmutableArray<IrType> locals, int returned = 0, int token = 0x06000001) =>
        new(new("fixture", Hash, token, "Method"), parameters, result,
            [new(0, 0, [], statements, result == IrType.Void ? new IrVoidReturn(1) : new IrReturn(1, returned))], locals);
    private static ManagedLocationIrProgram Program(IrMethod entry, ValueTypeCatalog? catalog = null, params IrMethod[] others) =>
        new(new(IrMethodId.From(entry.Source), [entry, .. others], catalog ?? Catalog()));
    private static IrMethod Frame() => Method([IrType.Int32], IrType.Int32,
        [D(0, PairLocation, new IrExpression.AddressLocal(0)), new IrStatement.InitializeLocation(0, 0),
         D(1, IntLocation, new IrExpression.LocationField(Number, 0)), D(2, IntLocation, new IrExpression.AddressParameter(0)),
         D(3, IrType.Int32, new IrExpression.LoadLocation(2), fault: true), new IrStatement.StoreLocation(0, 1, 3),
         D(4, IrType.Int32, new IrExpression.LoadLocation(1), fault: true)], [PairType], returned: 4);

    [Fact]
    public void Frame_locations_and_nested_fields_keep_backing_storage_roots_separate_from_owner_roots()
    {
        var program = Program(Frame()); ManagedLocationIrValidator.Validate(program);
        var roots = Assert.Single(ValueRootAssignment.Assign(program));
        Assert.Equal(3, roots.Slots.Count(s => s.LocationOwner));
        Assert.All(roots.Slots.Where(s => s.LocationOwner), s => Assert.Empty(s.Fields));
        Assert.Equal(new[] { Reference }, Assert.Single(roots.Slots.Where(s => !s.LocationOwner)).Fields);
        Assert.Equal(11, roots.ScalarSlots); Assert.Empty(roots.Safepoints);
    }

    [Fact]
    public void Callers_can_lend_storage_to_void_callees_and_reborrow_synchronously()
    {
        var mutate = Method([PairLocation, IrType.Int32], IrType.Void,
            [D(0, PairLocation, new IrExpression.Parameter(0)), D(1, IntLocation, new IrExpression.LocationField(Number, 0)),
             D(2, IrType.Int32, new IrExpression.Parameter(1)), new IrStatement.StoreLocation(0, 1, 2)], [], token: 0x06000002);
        var forward = Method([PairLocation, IrType.Int32], IrType.Void,
            [D(0, PairLocation, new IrExpression.Parameter(0)), D(1, IrType.Int32, new IrExpression.Parameter(1)),
             new IrStatement.Invoke(0, IrMethodId.From(mutate.Source), [0, 1])], [], token: 0x06000003);
        var entry = Method([IrType.Int32], IrType.Int32,
            [D(0, PairLocation, new IrExpression.AddressLocal(0)), D(1, IrType.Int32, new IrExpression.Parameter(0)),
             new IrStatement.Invoke(0, IrMethodId.From(forward.Source), [0, 1]),
             D(2, PairType, new IrExpression.LoadLocation(0), fault: true), D(3, IrType.Int32, new IrExpression.ValueField(Number, 2))], [PairType], returned: 3);
        var roots = ValueRootAssignment.Assign(Program(entry, null, mutate, forward));
        Assert.Equal(ObjectSafepointKind.Call, Assert.Single(roots[0].Safepoints).Kind);
        Assert.Equal(ObjectSafepointKind.Call, Assert.Single(roots[2].Safepoints).Kind);
        Assert.Equal(3, roots[1].Slots.Count(s => s.LocationOwner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Heap_field_locations_have_explicit_fault_edges_and_keep_the_owner_rooted(bool nullReceiver)
    {
        var entry = Method(nullReceiver ? [] : [IrType.Reference(Box)], IrType.Int32,
            [D(0, PairType, new IrExpression.DefaultValue(Pair)),
             D(1, nullReceiver ? IrType.NullReference : IrType.Reference(Box), nullReceiver ? new IrExpression.Null() : new IrExpression.Parameter(0)),
             D(2, IrType.Location(IrType.Reference(Object)), new IrExpression.AddressField(BoxReference, 1), fault: true),
             D(3, IrType.NullReference, new IrExpression.Null()), new IrStatement.StoreLocation(0, 2, 3),
             D(4, IrType.Reference(Object), new IrExpression.LoadLocation(2), fault: true),
             D(5, IrType.Location(IrType.Int32, readOnly: true), new IrExpression.AddressField(BoxNumber, 1), fault: true),
             D(6, IrType.Int32, new IrExpression.LoadLocation(5), fault: true)], [], returned: 6);
        var program = Program(entry, Catalog(heap: true));
        if (nullReceiver)
        {
            // Include the field owner as declared storage even though this access always faults.
            entry = entry with { Locals = [IrType.Reference(Box)] }; program = Program(entry, Catalog(heap: true));
        }
        var roots = Assert.Single(ValueRootAssignment.Assign(program));
        Assert.Equal(2, roots.Slots.Count(s => s.LocationOwner));
        Assert.Contains(roots.Slots, s => s.Index == 4 && s.Kind == ObjectRootKind.Value && !s.LocationOwner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nested_readonly_fields_propagate_readonly_locations(bool readOnly)
    {
        var outer = IrType.Value(Outer); var location = IrType.Location(PairType, readOnly);
        var entry = Method([], IrType.Int32,
            [D(0, IrType.Location(outer), new IrExpression.AddressLocal(0)),
             D(1, location, new IrExpression.LocationField(Nested, 0)),
             D(2, IrType.Location(IrType.Int32, readOnly), new IrExpression.LocationField(Number, 1)),
             D(3, IrType.Int32, new IrExpression.LoadLocation(2), fault: true)], [outer], returned: 3);
        var program = Program(entry, Catalog(nested: true, readOnly: readOnly));
        var roots = Assert.Single(ValueRootAssignment.Assign(program));
        Assert.Equal(new[] { Nested, Reference }, Assert.Single(roots.Slots.Where(s => !s.LocationOwner)).Fields);
        if (readOnly)
        {
            var block = entry.Blocks[0]; var bad = block.Statements.SetItem(2, D(2, IntLocation, new IrExpression.LocationField(Number, 1)));
            Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(program with
            { Flow = program.Flow with { Methods = [entry with { Blocks = [block with { Statements = bad }] }] } }));
        }
    }

    [Theory]
    [InlineData("return")]
    [InlineData("entry_borrow")]
    [InlineData("local_escape")]
    [InlineData("edge_escape")]
    [InlineData("store_reference")]
    [InlineData("store_value_field")]
    [InlineData("pointer_arithmetic")]
    [InlineData("comparison")]
    [InlineData("null")]
    [InlineData("default_location")]
    [InlineData("location_of_location")]
    [InlineData("invalid_element")]
    [InlineData("invalid_identity")]
    [InlineData("stray_element")]
    [InlineData("stray_readonly")]
    [InlineData("wrong_pointee")]
    [InlineData("unknown_field")]
    [InlineData("wrong_owner")]
    [InlineData("forward_location")]
    [InlineData("address_value_copy")]
    [InlineData("missing_load_edge")]
    [InlineData("store_edge")]
    [InlineData("init_edge")]
    [InlineData("readonly_store")]
    [InlineData("readonly_init")]
    [InlineData("readonly_call")]
    public void Escapes_forged_addresses_and_invalid_memory_operations_are_rejected(string damage)
    {
        var entry = Frame(); var statements = entry.Blocks[0].Statements;
        switch (damage)
        {
            case "return": entry = entry with { ReturnType = PairLocation }; entry = entry with { Blocks = [entry.Blocks[0] with { Terminator = new IrReturn(1, 0) }] }; break;
            case "entry_borrow": entry = entry with { Parameters = [IntLocation] }; statements = statements.SetItem(3, D(2, IntLocation, new IrExpression.Parameter(0))); break;
            case "local_escape": entry = entry with { Locals = [PairLocation] }; break;
            case "edge_escape": entry = entry with { Blocks = [entry.Blocks[0] with { Terminator = new IrJump(1, new(1, [0])) },
                new(1, 10, [PairLocation], [new IrStatement.Define(new(0, IrType.Int32, 10, new IrExpression.Constant(0)))], new IrReturn(11, 0))] }; break;
            case "store_reference": entry = entry with { Locals = [PairType, IrType.Reference(Object)] }; statements = statements.Add(new IrStatement.StoreLocal(0, 1, 0)); break;
            case "store_value_field": statements = statements.Add(D(5, PairType, new IrExpression.DefaultValue(Pair))).Add(D(6, PairType, new IrExpression.WithValueField(Reference, 5, 0))); break;
            case "pointer_arithmetic": statements = statements.Add(D(5, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 4))); break;
            case "comparison": statements = statements.Add(D(5, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, 0, 0))); break;
            case "null": statements = statements.SetItem(0, D(0, PairLocation, new IrExpression.Null())); break;
            case "default_location": statements = statements.SetItem(0, D(0, PairLocation, new IrExpression.DefaultValue(Pair))); break;
            case "location_of_location": statements = statements.SetItem(0, D(0, IrType.Location(PairLocation), new IrExpression.AddressLocal(0))); break;
            case "invalid_element": statements = statements.SetItem(0, D(0, IrType.Location(IrType.Void), new IrExpression.AddressLocal(0))); break;
            case "invalid_identity": statements = statements.SetItem(0, D(0, IrType.Location(IrType.Value(Pair with { ContentHash = null! })), new IrExpression.AddressLocal(0))); break;
            case "stray_element": entry = entry with { Parameters = [new(IrTypeKind.Int32, Element: PairType)] }; break;
            case "stray_readonly": entry = entry with { Parameters = [new(IrTypeKind.Int32, ReadOnly: true)] }; break;
            case "wrong_pointee": statements = statements.SetItem(0, D(0, IntLocation, new IrExpression.AddressLocal(0))); break;
            case "unknown_field": statements = statements.SetItem(2, D(1, IntLocation, new IrExpression.LocationField(Number with { Token = 0x04009999 }, 0))); break;
            case "wrong_owner": statements = statements.SetItem(2, D(1, IntLocation, new IrExpression.LocationField(Number with { Owner = Outer }, 0))); break;
            case "forward_location": statements = statements.SetItem(2, D(1, IntLocation, new IrExpression.LocationField(Number, 1))); break;
            case "address_value_copy": entry = entry with { Parameters = [PairType] }; statements = [D(0, PairType, new IrExpression.Parameter(0)), D(1, PairLocation, new IrExpression.LocationField(Number, 0))]; entry = entry with { ReturnType = PairType, Blocks = [entry.Blocks[0] with { Terminator = new IrReturn(1, 0) }] }; break;
            case "missing_load_edge": statements = statements.SetItem(4, D(3, IrType.Int32, new IrExpression.LoadLocation(2))); break;
            case "store_edge": statements = statements.SetItem(5, new IrStatement.StoreLocation(0, 1, 3, (IrExceptionTarget)99)); break;
            case "init_edge": statements = statements.SetItem(1, new IrStatement.InitializeLocation(0, 0, (IrExceptionTarget)99)); break;
            case "readonly_store": statements = statements.SetItem(0, D(0, IrType.Location(PairType, true), new IrExpression.AddressLocal(0)))
                .SetItem(2, D(1, IrType.Location(IrType.Int32, true), new IrExpression.LocationField(Number, 0))).RemoveAt(1); break;
            case "readonly_init": statements = statements.SetItem(0, D(0, IrType.Location(PairType, true), new IrExpression.AddressLocal(0))); break;
            case "readonly_call":
                var target = Method([IrType.Location(PairType, true)], IrType.Int32,
                    [D(0, IrType.Location(PairType, true), new IrExpression.Parameter(0)),
                     D(1, IntLocation, new IrExpression.LocationField(Number, 0)), D(2, IrType.Int32, new IrExpression.Constant(0))], [], returned: 2, token: 0x06000002);
                var caller = entry with { Blocks = [entry.Blocks[0] with { Statements = statements.Add(D(5, IrType.Int32, new IrExpression.Call(IrMethodId.From(target.Source), [0]), fault: true)) }] };
                Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(Program(caller, null, target))); return;
        }
        entry = entry with { Blocks = entry.Blocks.SetItem(0, entry.Blocks[0] with { Statements = statements }) };
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(Program(entry)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Borrowed_locations_are_invariant_and_cannot_weaken_readonly(bool weakenReadonly)
    {
        var source = weakenReadonly ? IrType.Location(PairType, true) : IrType.Location(IrType.Reference(Box));
        var target = weakenReadonly ? PairLocation : IrType.Location(IrType.Reference(Object));
        var method = Method([target], IrType.Int32, [D(0, IrType.Int32, new IrExpression.Constant(0))], [], token: 0x06000002);
        var entry = Method([], IrType.Int32, [D(0, source, new IrExpression.AddressLocal(0)),
            D(1, IrType.Int32, new IrExpression.Call(IrMethodId.From(method.Source), [0]), fault: true)],
            weakenReadonly ? [PairType] : [IrType.Reference(Box), PairType], returned: 1);
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(Program(entry, Catalog(heap: !weakenReadonly), method)));
    }

    [Fact]
    public void A_writable_location_can_be_lent_as_readonly_without_exposing_a_mutable_alias_to_the_callee()
    {
        var borrow = IrType.Location(PairType, true);
        var method = Method([borrow], IrType.Int32,
            [D(0, borrow, new IrExpression.Parameter(0)), D(1, IrType.Location(IrType.Int32, true), new IrExpression.LocationField(Number, 0)),
             D(2, IrType.Int32, new IrExpression.LoadLocation(1), fault: true)], [], returned: 2, token: 0x06000002);
        var entry = Method([], IrType.Int32, [D(0, PairLocation, new IrExpression.AddressLocal(0)),
            D(1, IrType.Int32, new IrExpression.Call(IrMethodId.From(method.Source), [0]), fault: true)], [PairType], returned: 1);
        var roots = ValueRootAssignment.Assign(Program(entry, null, method));
        Assert.Single(roots[0].Safepoints);
        Assert.Equal(3, roots[1].Slots.Count(s => s.LocationOwner));
        var block = method.Blocks[0];
        var changed = method with { Blocks = [block with { Statements = block.Statements.Add(new IrStatement.InitializeLocation(0, 0)) }] };
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(Program(entry, null, changed)));
    }

    [Theory]
    [InlineData("missing_edge")]
    [InlineData("unknown_field")]
    [InlineData("wrong_owner")]
    [InlineData("wrong_receiver")]
    [InlineData("forward_receiver")]
    [InlineData("writable_readonly_field")]
    public void Heap_locations_validate_exact_fields_receivers_readonly_and_fault_edges(string damage)
    {
        var statements = ImmutableArray.Create<IrStatement>(D(0, IrType.Reference(Box), new IrExpression.Parameter(0)),
            D(1, IrType.Location(IrType.Reference(Object)), new IrExpression.AddressField(BoxReference, 0), fault: true),
            D(2, IrType.Int32, new IrExpression.Constant(0)));
        var method = Method([IrType.Reference(Box)], IrType.Int32, statements, [PairType], returned: 2);
        var program = Program(method, Catalog(heap: true));
        ManagedLocationIrValidator.Validate(program);
        switch (damage)
        {
            case "missing_edge": statements = statements.SetItem(1, D(1, IrType.Location(IrType.Reference(Object)), new IrExpression.AddressField(BoxReference, 0))); break;
            case "unknown_field": statements = statements.SetItem(1, D(1, IrType.Location(IrType.Reference(Object)), new IrExpression.AddressField(BoxReference with { Token = 0x04009999 }, 0), fault: true)); break;
            case "wrong_owner": statements = statements.SetItem(1, D(1, IrType.Location(IrType.Reference(Object)), new IrExpression.AddressField(BoxReference with { Owner = Object }, 0), fault: true)); break;
            case "wrong_receiver": statements = statements.SetItem(0, D(0, IrType.Int32, new IrExpression.Constant(0))); break;
            case "forward_receiver": statements = statements.SetItem(1, D(1, IrType.Location(IrType.Reference(Object)), new IrExpression.AddressField(BoxReference, 1), fault: true)); break;
            case "writable_readonly_field": statements = statements.SetItem(1, D(1, IntLocation, new IrExpression.AddressField(BoxNumber, 0), fault: true)); break;
        }
        var damaged = program with { Flow = program.Flow with { Methods = [method with { Blocks = [method.Blocks[0] with { Statements = statements }] }] } };
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(damaged));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Location_owners_consume_the_existing_exact_root_limit(bool excess)
    {
        var entry = Frame() with { Locals = [PairType, .. Enumerable.Repeat(IrType.Reference(Object), 1020 + (excess ? 1 : 0))] };
        var program = Program(entry); ManagedLocationIrValidator.Validate(program);
        if (excess) Assert.Contains("root-slot limit", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program)).Message, StringComparison.Ordinal);
        else Assert.Equal(1024, Assert.Single(ValueRootAssignment.Assign(program)).Slots.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Location_carriers_consume_two_slots_in_the_exact_expanded_storage_budget(bool excess)
    {
        var catalog = Catalog();
        catalog = catalog with { Types = [catalog.Types[0] with { Fields = [.. Enumerable.Range(1, 16).Select(row =>
            new ObjectField(new(Pair, 0x04000000 | row), "Field" + row, FieldAttributes.Public, new ObjectStorage.Int32()))] }] };
        var entry = Frame() with { Locals = [.. Enumerable.Repeat(PairType, 4094)] };
        var block = entry.Blocks[0]; var statements = block.Statements;
        for (int id = 5; id < 28 + (excess ? 1 : 0); id++) statements = statements.Add(D(id, IrType.Int32, new IrExpression.Constant(id)));
        entry = entry with { Blocks = [block with { Statements = statements }] };
        var program = Program(entry, catalog); ManagedLocationIrValidator.Validate(program);
        if (excess) Assert.Contains("scalar-slot limit", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program)).Message, StringComparison.Ordinal);
        else Assert.Equal(65536, Assert.Single(ValueRootAssignment.Assign(program)).ScalarSlots);
    }

    [Fact]
    public void Legacy_profiles_and_native_lowering_cannot_consume_managed_locations()
    {
        var program = Program(Frame());
        Assert.Throws<ArgumentException>(() => ValueIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.Lower(program.Flow));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(program.Flow.Entry, program.Flow.Methods, program.Flow.Types.References)));
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(new(new(program.Flow.Entry, program.Flow.Methods, program.Flow.Types.References), [])));
        Assert.Throws<ArgumentNullException>(() => ValueRootAssignment.Assign((ManagedLocationIrProgram)null!));
    }

    [Fact]
    public void Malformed_nested_location_types_are_rejected_without_recursive_type_comparison()
    {
        IrType invalid = IrType.Int32;
        for (int i = 0; i < 8192; i++) invalid = IrType.Location(invalid);
        var method = Method([invalid], IrType.Int32, [D(0, IrType.Int32, new IrExpression.Constant(0))], [PairType]);
        Assert.Throws<ArgumentException>(() => ManagedLocationIrValidator.Validate(Program(method)));
    }
}
