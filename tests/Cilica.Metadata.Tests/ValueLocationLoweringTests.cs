using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueLocationLoweringTests
{
    [Fact]
    public void Mutable_values_expand_into_validated_scalar_borrows_and_explicit_readonly_views()
    {
        var (program, bindings) = NativeValueBorrowTests.Model();
        var lowered = ValueScalarLowering.LowerLocations(program, bindings);
        ScalarLocationIrValidator.Validate(lowered);
        Assert.All(lowered.Flow.Flow.Methods.SelectMany(m => m.Parameters.Concat(m.Locals)
            .Concat(m.Blocks.SelectMany(b => b.Values).Select(v => v.Type))), type =>
                Assert.True(type.Kind != IrTypeKind.Value && type.Element?.Kind != IrTypeKind.Value));
        Assert.Contains(lowered.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v =>
            v.Expression is IrExpression.ReadOnlyLocation && v.Type.ReadOnly);
    }

    [Theory]
    [InlineData("writable")]
    [InlineData("not_location")]
    [InlineData("wrong_element")]
    public void Readonly_views_cannot_launder_a_different_type_or_writable_capability(string invalid)
    {
        var (program, bindings) = NativeValueBorrowTests.Model(); var entry = program.Flow.Methods[0];
        var block = entry.Blocks[0]; var definition = block.Statements.OfType<IrStatement.Define>().Single(d => d.Value.Expression is IrExpression.ReadOnlyLocation);
        var replacement = invalid switch
        {
            "writable" => definition with { Value = definition.Value with { Type = definition.Value.Type with { ReadOnly = false } } },
            "not_location" => definition with { Value = definition.Value with { Expression = new IrExpression.ReadOnlyLocation(0) } },
            _ => definition with { Value = definition.Value with { Type = IrType.Location(IrType.Int32, true) } },
        };
        entry = entry with { Blocks = [block with { Statements = block.Statements.Replace(definition, replacement) }] };
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.SetItem(0, entry) } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program, bindings));
    }

    private static (ManagedLocationIrProgram Program, ImmutableArray<ObjectMethodBinding> Bindings) LeafModel(int fields, bool readonlyField = false)
    {
        var (program, bindings) = NativeValueBorrowTests.Model(); var types = program.Flow.Types;
        var leaf = types.Types.Single(t => t.Source.Name.EndsWith("ValueLeaf", StringComparison.Ordinal)); var id = ObjectTypeId.From(leaf.Source);
        leaf = leaf with { Fields = [.. Enumerable.Range(1, fields).Select(row => new ObjectField(new(id, 0x04000000 | row), "Field" + row,
            FieldAttributes.Public | (readonlyField ? FieldAttributes.InitOnly : 0), new ObjectStorage.Int32()))] };
        var references = types.References with { Roots = [types.References.ObjectBase], String = null, Arrays = [],
            Types = [types.References.Types.Single(t => ObjectTypeId.From(t.Source) == types.References.ObjectBase)] };
        var catalog = types with { Roots = [id], Types = [leaf], References = references };
        var type = IrType.Value(id); var location = IrType.Location(type);
        var entry = program.Flow.Methods[0] with { Locals = [type], Blocks = [new(0, 0, [],
            [NativeValueBorrowTests.D(0, location, new IrExpression.AddressLocal(0)), new IrStatement.InitializeLocation(0, 0),
             NativeValueBorrowTests.D(1, type, new IrExpression.LoadLocation(0), true), new IrStatement.StoreLocation(0, 0, 1),
             NativeValueBorrowTests.D(2, IrType.Int32, new IrExpression.Constant(0))], new IrReturn(1, 2))] };
        return (new(new(program.Flow.Entry, [entry], catalog)), [bindings.Single(b => b.Method == program.Flow.Entry)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Whole_value_operations_handle_empty_and_readonly_field_layouts(int fields)
    {
        var (program, bindings) = LeafModel(fields, readonlyField: true);
        var lowered = ValueScalarLowering.LowerLocations(program, bindings);
        Assert.Single(lowered.Flow.Flow.Methods);
        if (fields == 0) Assert.Empty(lowered.Flow.Flow.Methods[0].Locals);
        else Assert.Contains(lowered.Flow.Flow.Methods[0].Blocks[0].Statements, s => s is IrStatement.StoreLocation);
    }

    [Fact]
    public void Empty_borrow_arguments_disappear_without_removing_the_call()
    {
        var (program, bindings) = LeafModel(0); var entry = program.Flow.Methods[0];
        var empty = entry.Locals[0]; var location = IrType.Location(empty);
        var helper = entry with { Source = entry.Source with { Token = entry.Source.Token + 1, Name = "Empty" }, Parameters = [location],
            ReturnType = IrType.Void, Locals = [], Blocks = [new(0, 0, [],
                [NativeValueBorrowTests.D(0, location, new IrExpression.Parameter(0)), new IrStatement.InitializeLocation(0, 0)], new IrVoidReturn(1))] };
        var block = entry.Blocks[0];
        entry = entry with { Blocks = [block with { Statements = block.Statements.Insert(1, new IrStatement.Invoke(0, IrMethodId.From(helper.Source), [0])) }] };
        program = program with { Flow = program.Flow with { Methods = [entry, helper] } };
        bindings = bindings.Add(bindings[0] with { Method = IrMethodId.From(helper.Source) });
        var lowered = ValueScalarLowering.LowerLocations(program, bindings).Flow.Flow;
        Assert.Empty(lowered.Methods[1].Parameters);
        Assert.Empty(Assert.Single(lowered.Methods[0].Blocks[0].Statements.OfType<IrStatement.Invoke>()).Arguments);
    }

    [Fact]
    public void Borrowed_leaf_expansion_is_bounded_before_creating_carrier_arrays()
    {
        var (program, bindings) = LeafModel(64); var entry = program.Flow.Methods[0]; var type = IrType.Location(entry.Locals[0]);
        var statements = Enumerable.Range(0, 512).Select(i => (IrStatement)NativeValueBorrowTests.D(i, type, new IrExpression.AddressLocal(0))).ToImmutableArray()
            .Add(NativeValueBorrowTests.D(512, IrType.Int32, new IrExpression.Constant(0)));
        entry = entry with { Blocks = [new(0, 0, [], statements, new IrReturn(1, 512))] };
        program = program with { Flow = program.Flow with { Methods = [entry] } };
        _ = ValueRootAssignment.Assign(program);
        Assert.Contains("expanded location storage limit", Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program, bindings)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_whole_stores_charge_each_expanded_statement()
    {
        var (program, bindings) = LeafModel(64); var entry = program.Flow.Methods[0]; var block = entry.Blocks[0];
        var statements = block.Statements.Take(3).Concat(Enumerable.Range(0, 1024).Select(_ => new IrStatement.StoreLocation(0, 0, 1)))
            .Append(block.Statements[^1]).ToImmutableArray();
        entry = entry with { Blocks = [block with { Statements = statements }] };
        program = program with { Flow = program.Flow with { Methods = [entry] } };
        _ = ValueRootAssignment.Assign(program);
        Assert.Contains("expanded statement limit", Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program, bindings)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lowering_requires_static_bindings_and_does_not_accept_null_programs()
    {
        var (program, bindings) = NativeValueBorrowTests.Model();
        Assert.Throws<ArgumentNullException>(() => ValueScalarLowering.LowerLocations(null!, bindings));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program, default));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program,
            bindings.SetItem(0, bindings[0] with { Kind = ObjectMethodKind.Instance })));
    }
}
