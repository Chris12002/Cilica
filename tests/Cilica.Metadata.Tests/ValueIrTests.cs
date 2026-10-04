using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueIrTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), ValueBase = new(Hash, 0x02000003), Pair = new(Hash, 0x02000004);
    private static readonly IrType PairType = IrType.Value(Pair);
    private static readonly ObjectFieldId Number = new(Pair, 0x04000001), Reference = new(Pair, 0x04000002);
    private static ValueTypeCatalog Catalog() => new([Pair], ValueBase,
        [new(new("fixture", Hash, Pair.Token, "Pair"), TypeAttributes.Public | TypeAttributes.Sealed,
            [new(Number, "Number", FieldAttributes.Public, new ObjectStorage.Int32()),
             new(Reference, "Reference", FieldAttributes.Public, new ObjectStorage.Reference(Object))])],
        new([Object], Object, [new(new("fixture", Hash, Object.Token, "System.Object"), null, TypeAttributes.Public, [])]));
    private static IrStatement.Define D(int id, IrType type, IrExpression expression, int offset = 0, bool fault = false) =>
        new(new(id, type, offset, expression), fault ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements,
        int returned, ImmutableArray<IrType> locals = default, int token = 0x06000001) =>
        new(new("fixture", Hash, token, "Method"), parameters, result,
            [new(0, 0, [], statements, new IrReturn(1, returned))], locals.IsDefault ? [] : locals);
    private static ValueIrProgram Program(IrMethod method) => new(IrMethodId.From(method.Source), [method], Catalog());
    private static IrMethod Identity() => Method([PairType], PairType, [D(0, PairType, new IrExpression.Parameter(0))], 0);

    [Fact]
    public void Value_copies_preserve_exact_type_across_locals_calls_and_returns()
    {
        var callee = Identity() with { Source = Identity().Source with { Token = 0x06000002 } };
        var entry = Method([PairType], PairType, [D(0, PairType, new IrExpression.Parameter(0)),
            new IrStatement.StoreLocal(0, 0, 0), D(1, PairType, new IrExpression.Local(0)),
            D(2, PairType, new IrExpression.Call(IrMethodId.From(callee.Source), [1]), fault: true)], 2, [PairType]);
        var program = Program(entry) with { Methods = [entry, callee] };
        ValueIrValidator.Validate(program);
        var roots = ValueRootAssignment.Assign(program);
        Assert.Equal(5, roots[0].Slots.Length); Assert.Equal(10, roots[0].ScalarSlots);
        Assert.All(roots[0].Slots, slot => Assert.Equal(new[] { Reference }, slot.Fields));
        Assert.Equal(5, roots[0].Slots.Select(s => (s.Kind, s.Index, s.Block)).Distinct().Count());
        Assert.Equal(ObjectSafepointKind.Call, Assert.Single(roots[0].Safepoints).Kind);
        Assert.Equal(2, roots[1].Slots.Length);
    }

    [Fact]
    public void Pure_field_replacement_and_extraction_require_no_allocation_safepoints()
    {
        var method = Method([IrType.Int32], IrType.Int32,
            [D(0, IrType.Int32, new IrExpression.Parameter(0)), D(1, PairType, new IrExpression.DefaultValue(Pair)),
             D(2, PairType, new IrExpression.WithValueField(Number, 1, 0)),
             D(3, IrType.NullReference, new IrExpression.Null()),
             D(4, PairType, new IrExpression.WithValueField(Reference, 2, 3)),
             D(5, IrType.Int32, new IrExpression.ValueField(Number, 4))], 5);
        var plan = Assert.Single(ValueRootAssignment.Assign(Program(method)));
        Assert.Empty(plan.Safepoints); Assert.Equal(4, plan.Slots.Length);
    }

    [Fact]
    public void Value_copies_flow_through_stack_joins_and_backedges_with_distinct_roots()
    {
        var method = Identity() with
        {
            Parameters = [PairType, IrType.Int32],
            Blocks = [new(0, 0, [], [D(0, PairType, new IrExpression.Parameter(0))], new IrJump(1, new(1, [0]))),
                new(1, 10, [PairType], [D(0, PairType, new IrExpression.StackInput(0), 10),
                    D(1, IrType.Int32, new IrExpression.Parameter(1), 11)],
                    new IrBranch(12, Int32Comparison.Nonzero, 1, null, new(1, [0]), new(2, [0]))),
                new(2, 20, [PairType], [D(0, PairType, new IrExpression.StackInput(0), 20)], new IrReturn(21, 0))]
        };
        var roots = Assert.Single(ValueRootAssignment.Assign(Program(method))).Slots;
        Assert.Equal(6, roots.Length);
        Assert.Equal(new[] { 1, 2 }, roots.Where(s => s.Kind == ObjectRootKind.BlockInput).Select(s => s.Block));
    }

    [Theory]
    [InlineData("as_reference")]
    [InlineData("null_value")]
    [InlineData("scalar_value")]
    [InlineData("unknown_value")]
    [InlineData("missing_identity")]
    [InlineData("null_identity_hash")]
    [InlineData("field_owner")]
    [InlineData("unknown_field")]
    [InlineData("field_type")]
    [InlineData("field_forward_operand")]
    [InlineData("field_replacement_type")]
    [InlineData("fault_edge")]
    [InlineData("arithmetic")]
    [InlineData("equality")]
    [InlineData("truth_test")]
    [InlineData("unused_value")]
    public void Invalid_value_operations_and_implicit_boxing_are_rejected(string shape)
    {
        var method = Identity(); var statements = method.Blocks[0].Statements;
        switch (shape)
        {
            case "as_reference": method = method with { ReturnType = IrType.Reference(Object) }; break;
            case "null_value": statements = [D(0, PairType, new IrExpression.Null())]; break;
            case "scalar_value": statements = [D(0, PairType, new IrExpression.Constant(0))]; break;
            case "unknown_value": method = method with { Locals = [IrType.Value(Pair with { Token = 0x02000009 })] }; break;
            case "missing_identity": method = method with { Locals = [new(IrTypeKind.Value)] }; break;
            case "null_identity_hash": method = method with { Locals = [IrType.Value(Pair with { ContentHash = null! })] }; break;
            case "field_owner": statements = statements.Add(D(1, IrType.Int32, new IrExpression.ValueField(Number with { Owner = Object }, 0))); break;
            case "unknown_field": statements = statements.Add(D(1, IrType.Int32, new IrExpression.ValueField(Number with { Token = 0x04000009 }, 0))); break;
            case "field_type": statements = statements.Add(D(1, IrType.Reference(Object), new IrExpression.ValueField(Number, 0))); break;
            case "field_forward_operand": statements = statements.Add(D(1, IrType.Int32, new IrExpression.ValueField(Number, 1))); break;
            case "field_replacement_type": statements = statements.Add(D(1, PairType, new IrExpression.WithValueField(Number, 0, 0))); break;
            case "fault_edge": statements = [D(0, PairType, new IrExpression.DefaultValue(Pair), fault: true)]; break;
            case "arithmetic": statements = statements.Add(D(1, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 0))); break;
            case "equality": statements = statements.Add(D(1, IrType.Int32, new IrExpression.Compare(Int32Comparison.Equal, 0, 0))); break;
            case "truth_test": method = method with { Blocks = [method.Blocks[0] with
                { Terminator = new IrBranch(1, Int32Comparison.Zero, 0, null, new(0, []), new(0, [])) }] }; break;
            case "unused_value": method = Method([], IrType.Int32, [D(0, IrType.Int32, new IrExpression.Constant(0))], 0); statements = method.Blocks[0].Statements; break;
        }
        method = method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with { Statements = statements }) };
        Assert.Throws<ArgumentException>(() => ValueIrValidator.Validate(Program(method)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_embedded_reference_consumes_the_existing_method_root_budget(bool excess)
    {
        // Parameter + its loaded copy account for two roots.
        var method = Identity() with { Locals = [.. Enumerable.Repeat(PairType, 1022 + (excess ? 1 : 0))] };
        var program = Program(method);
        ValueIrValidator.Validate(program);
        if (excess) Assert.Contains("root-slot limit", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program)).Message, StringComparison.Ordinal);
        else Assert.Equal(1024, Assert.Single(ValueRootAssignment.Assign(program)).Slots.Length);
    }

    [Fact]
    public void Existing_emitters_and_reference_profile_reject_inline_values()
    {
        var program = Program(Identity());
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(program.Entry, program.Methods, program.Types.References)));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(program.Entry, program.Methods)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expanded_execution_slots_have_an_independent_exact_limit(bool excess)
    {
        var catalog = Catalog();
        catalog = catalog with { Types = [catalog.Types[0] with { Fields = [.. Enumerable.Range(1, 16).Select(row =>
            new ObjectField(new(Pair, 0x04000000 | row), "Field" + row, FieldAttributes.Public, new ObjectStorage.Int32()))] }] };
        var method = Identity() with { Locals = [.. Enumerable.Repeat(PairType, 4094 + (excess ? 1 : 0))] };
        var program = Program(method) with { Types = catalog };
        ValueIrValidator.Validate(program);
        if (excess) Assert.Contains("scalar-slot limit", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program)).Message, StringComparison.Ordinal);
        else Assert.Equal(65536, Assert.Single(ValueRootAssignment.Assign(program)).ScalarSlots);
    }
}
