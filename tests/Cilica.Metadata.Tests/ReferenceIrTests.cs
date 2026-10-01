using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ReferenceIrTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002);
    private static readonly ObjectTypeId Node = new(Hash, 0x02000003);
    private static readonly ObjectTypeId Other = new(new string('b', 64), 0x02000003);
    private static readonly IrType NodeRef = IrType.Reference(Node);
    private static readonly IrType ObjectRef = IrType.Reference(Object);
    private static readonly IrType OtherRef = IrType.Reference(Other);

    private static ObjectTypeLayout Layout(ObjectTypeId id) => new(
        new("fixture", id.ContentHash, id.Token, id == Object ? "System.Object" : "SameName"),
        id == Object ? null : Object, TypeAttributes.Public, []);
    private static ObjectTypeCatalog Catalog(params ObjectTypeId[] roots) => new([.. roots], Object,
        [Layout(Object), .. roots.Where(r => r != Object).Distinct().Select(Layout)]);
    private static IrStatement.Define Define(int id, IrType type, IrExpression expression, bool throwing = false) =>
        new(new(id, type, 0, expression), throwing ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(int token, ImmutableArray<IrType> parameters, IrType returned,
        ImmutableArray<IrStatement> statements, int value = 0, ImmutableArray<IrType> locals = default) =>
        new(new("fixture", Hash, token, "ignored"), parameters, returned,
            [new(0, 0, [], statements, new IrReturn(1, value))], locals.IsDefault ? [] : locals);
    private static ReferenceIrProgram Program(IrMethod method, ObjectTypeCatalog? catalog = null) =>
        new(IrMethodId.From(method.Source), [method], catalog ?? Catalog(Node));
    private static IrMethod Identity() => Method(0x06000001, [NodeRef], NodeRef, [Define(0, NodeRef, new IrExpression.Parameter(0))]);

    [Fact]
    public void Null_and_concrete_references_flow_through_locals_and_widen_to_object()
    {
        var method = Method(0x06000001, [NodeRef], ObjectRef,
            [Define(0, IrType.NullReference, new IrExpression.Null()), new IrStatement.StoreLocal(0, 0, 0),
             Define(1, NodeRef, new IrExpression.Parameter(0)), new IrStatement.StoreLocal(0, 0, 1),
             Define(2, NodeRef, new IrExpression.Local(0))], 2, [NodeRef]);
        ReferenceIrValidator.Validate(Program(method));
        Assert.Equal(NodeRef, new IrType(IrTypeKind.Reference, new(Hash, Node.Token)));
    }

    [Fact]
    public void Null_can_be_returned_with_a_concrete_reference_signature()
    {
        ReferenceIrValidator.Validate(Program(Method(0x06000001, [], NodeRef,
            [Define(0, IrType.NullReference, new IrExpression.Null())])));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("integer")]
    [InlineData("node")]
    [InlineData("object")]
    [InlineData("other")]
    public void Calls_use_declared_argument_and_result_types(string source)
    {
        IrType type = source switch { "null" => IrType.NullReference, "integer" => IrType.Int32,
            "object" => ObjectRef, "other" => OtherRef, _ => NodeRef };
        IrExpression input = source == "null" ? new IrExpression.Null() : new IrExpression.Parameter(0);
        var callee = Identity() with { Source = Identity().Source with { Token = 0x06000002 } };
        var entry = Method(0x06000001, source == "null" ? [] : [type], NodeRef,
            [Define(0, type, input), Define(1, NodeRef, new IrExpression.Call(IrMethodId.From(callee.Source), [0]), true)], 1);
        var program = new ReferenceIrProgram(IrMethodId.From(entry.Source), [entry, callee],
            source == "other" ? Catalog(Node, Other) : Catalog(Node));
        if (source is "null" or "node") ReferenceIrValidator.Validate(program);
        else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program));
    }

    [Fact]
    public void Concrete_call_results_can_be_returned_as_object_but_cannot_be_retagged()
    {
        var callee = Identity() with { Source = Identity().Source with { Token = 0x06000002 } };
        var entry = Method(0x06000001, [NodeRef], ObjectRef,
            [Define(0, NodeRef, new IrExpression.Parameter(0)),
             Define(1, NodeRef, new IrExpression.Call(IrMethodId.From(callee.Source), [0]), true)], 1);
        var program = new ReferenceIrProgram(IrMethodId.From(entry.Source), [entry, callee], Catalog(Node));
        ReferenceIrValidator.Validate(program);
        var forged = entry with { Blocks = [entry.Blocks[0] with { Statements = entry.Blocks[0].Statements.SetItem(1,
            Define(1, ObjectRef, new IrExpression.Call(IrMethodId.From(callee.Source), [0]), true)) }] };
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program with { Methods = [forged, callee] }));
    }

    [Theory]
    [InlineData("type_kind")]
    [InlineData("int_with_class")]
    [InlineData("null_with_class")]
    [InlineData("reference_without_class")]
    [InlineData("unknown_class")]
    [InlineData("noncanonical_class")]
    [InlineData("bad_hash_length")]
    [InlineData("null_hash")]
    [InlineData("invalid_token")]
    [InlineData("null_parameter")]
    [InlineData("null_local")]
    [InlineData("null_return")]
    [InlineData("missing_type")]
    [InlineData("parameter_retag")]
    [InlineData("local_retag")]
    [InlineData("constant_reference")]
    [InlineData("null_retag")]
    [InlineData("integer_return")]
    [InlineData("downcast_return")]
    [InlineData("null_expression")]
    [InlineData("pure_fault_edge")]
    public void Invalid_storage_and_expression_types_are_rejected(string damage)
    {
        var method = Identity(); var block = method.Blocks[0];
        IrType invalid = damage switch
        {
            "type_kind" => new((IrTypeKind)99), "int_with_class" => new(IrTypeKind.Int32, Node),
            "null_with_class" => new(IrTypeKind.NullReference, Node), "reference_without_class" => new(IrTypeKind.Reference),
            "unknown_class" => OtherRef, "noncanonical_class" => IrType.Reference(Node with { ContentHash = Hash.ToUpperInvariant() }),
            "bad_hash_length" => IrType.Reference(Node with { ContentHash = new string('a', 65) }),
            "null_hash" => IrType.Reference(Node with { ContentHash = null! }),
            "invalid_token" => IrType.Reference(Node with { Token = 0x01000003 }),
            "missing_type" => null!, _ => IrType.NullReference,
        };
        method = damage switch
        {
            "null_local" => method with { Locals = [IrType.NullReference] },
            "null_return" => method with { ReturnType = IrType.NullReference },
            "parameter_retag" => method with { Blocks = [block with { Statements = [Define(0, ObjectRef, new IrExpression.Parameter(0))] }] },
            "local_retag" => method with { Locals = [NodeRef], Blocks = [block with { Statements = [Define(0, ObjectRef, new IrExpression.Local(0))] }] },
            "constant_reference" => method with { Blocks = [block with { Statements = [Define(0, NodeRef, new IrExpression.Constant(0))] }] },
            "null_retag" => method with { Blocks = [block with { Statements = [Define(0, NodeRef, new IrExpression.Null())] }] },
            "integer_return" => method with { ReturnType = IrType.Int32 },
            "downcast_return" => method with { Parameters = [ObjectRef], Blocks = [block with { Statements = [Define(0, ObjectRef, new IrExpression.Parameter(0))] }] },
            "null_expression" => method with { Blocks = [block with { Statements = [Define(0, NodeRef, null!)] }] },
            "pure_fault_edge" => method with { Blocks = [block with { Statements = [Define(0, NodeRef, new IrExpression.Parameter(0), true)] }] },
            _ => method with { Parameters = [invalid] },
        };
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
    }

    [Theory]
    [InlineData(Int32Comparison.Equal, true)]
    [InlineData(Int32Comparison.NotEqual, true)]
    [InlineData(Int32Comparison.LessSigned, false)]
    [InlineData(Int32Comparison.GreaterUnsigned, false)]
    [InlineData(Int32Comparison.Zero, false)]
    [InlineData((Int32Comparison)99, false)]
    public void References_support_only_binary_identity_comparisons(Int32Comparison comparison, bool accepted)
    {
        var method = Method(0x06000001, [NodeRef], IrType.Int32,
            [Define(0, NodeRef, new IrExpression.Parameter(0)), Define(1, IrType.NullReference, new IrExpression.Null()),
             Define(2, IrType.Int32, new IrExpression.Compare(comparison, 0, 1))], 2);
        if (accepted) ReferenceIrValidator.Validate(Program(method));
        else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
    }

    [Theory]
    [InlineData("binary")]
    [InlineData("unary")]
    [InlineData("mixed_comparison")]
    [InlineData("mixed_store")]
    public void References_cannot_be_treated_as_integer_bits(string operation)
    {
        IrExpression expression = operation switch { "unary" => new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, 0),
            "mixed_comparison" => new IrExpression.Compare(Int32Comparison.Equal, 0, 1),
            _ => new IrExpression.Binary(Int32Operation.AddWrapping, 0, 1) };
        var method = Method(0x06000001, [NodeRef], IrType.Int32,
            [Define(0, NodeRef, new IrExpression.Parameter(0)), Define(1, IrType.Int32, new IrExpression.Constant(0)),
             operation == "mixed_store" ? new IrStatement.StoreLocal(0, 0, 0) : Define(2, IrType.Int32, expression)],
            operation == "mixed_store" ? 1 : 2, [IrType.Int32]);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
    }

    private static IrMethod Join(IrType input, IrType left, IrType right) => new(
        new("fixture", Hash, 0x06000001, "join"), [left, right, IrType.Int32], input,
        [new(0, 0, [], [Define(0, IrType.Int32, new IrExpression.Parameter(2)),
            Define(1, left, new IrExpression.Parameter(0)), Define(2, right, new IrExpression.Parameter(1))],
            new IrBranch(1, Int32Comparison.Nonzero, 0, null, new(1, [1]), new(1, [2]))),
         new(1, 2, [input], [new IrStatement.Define(new(0, input, 2, new IrExpression.StackInput(0)))], new IrReturn(3, 0))], []);

    [Theory]
    [InlineData("object", true)]
    [InlineData("node", false)]
    [InlineData("integer", false)]
    public void Branch_joins_use_assignability_and_exact_content_identity(string joined, bool accepted)
    {
        IrType type = joined switch { "object" => ObjectRef, "integer" => IrType.Int32, _ => NodeRef };
        var program = Program(Join(type, NodeRef, OtherRef), Catalog(Node, Other));
        if (accepted) ReferenceIrValidator.Validate(program);
        else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(Int32Comparison.Zero)]
    [InlineData(Int32Comparison.Nonzero)]
    public void Null_joins_and_reference_truth_branches_preserve_declared_stack_types(Int32Comparison comparison)
    {
        var joined = Join(NodeRef, NodeRef, NodeRef);
        var block = joined.Blocks[0];
        joined = joined with { Blocks = [block with { Statements = block.Statements.SetItem(2,
            Define(2, IrType.NullReference, new IrExpression.Null())),
            Terminator = new IrBranch(1, comparison, 1, null, new(1, [1]), new(1, [2])) }, joined.Blocks[1]] };
        ReferenceIrValidator.Validate(Program(joined));
        var forged = joined with { Blocks = [joined.Blocks[0], joined.Blocks[1] with {
            Statements = [new IrStatement.Define(new(0, ObjectRef, 2, new IrExpression.StackInput(0)))] }] };
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(forged)));
    }

    [Fact]
    public void Back_edges_and_recursive_calls_are_bounded_without_recursive_validation()
    {
        var method = Join(NodeRef, NodeRef, NodeRef);
        method = method with { Blocks = [method.Blocks[0], method.Blocks[1] with {
            Terminator = new IrBranch(3, Int32Comparison.Nonzero, 0, null, new(1, [0]), new(2, [0])) },
            new(2, 4, [NodeRef], [new IrStatement.Define(new(0, NodeRef, 4, new IrExpression.StackInput(0)))], new IrReturn(5, 0))] };
        ReferenceIrValidator.Validate(Program(method));
        var recursive = Identity();
        recursive = recursive with { Blocks = [recursive.Blocks[0] with { Statements = [
            Define(0, NodeRef, new IrExpression.Parameter(0)), Define(1, NodeRef,
                new IrExpression.Call(IrMethodId.From(recursive.Source), [0]), true)], Terminator = new IrReturn(1, 1) }] };
        ReferenceIrValidator.Validate(Program(recursive));
    }

    [Theory]
    [InlineData("no_fault")]
    [InlineData("wrong_fault")]
    [InlineData("missing_target")]
    [InlineData("default_args")]
    [InlineData("forward_arg")]
    [InlineData("arity")]
    [InlineData("null_callee_parameter")]
    public void Calls_cannot_bypass_fault_arity_or_callee_validation(string damage)
    {
        var callee = Identity() with { Source = Identity().Source with { Token = 0x06000002 } };
        var target = damage == "missing_target" ? new IrMethodId(Hash, 0x06000003) : IrMethodId.From(callee.Source);
        ImmutableArray<int> arguments = damage switch { "default_args" => default, "forward_arg" => [1], "arity" => [], _ => [0] };
        var call = Define(1, NodeRef, new IrExpression.Call(target, arguments), true);
        if (damage == "no_fault") call = call with { Exceptional = null };
        if (damage == "wrong_fault") call = call with { Exceptional = (IrExceptionTarget)99 };
        if (damage == "null_callee_parameter") callee = callee with { Parameters = [null!] };
        var entry = Method(0x06000001, [NodeRef], NodeRef, [Define(0, NodeRef, new IrExpression.Parameter(0)), call], 1);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(IrMethodId.From(entry.Source), [entry, callee], Catalog(Node))));
    }

    [Fact]
    public void Catalogs_reject_unrelated_types_and_duplicate_roots_but_follow_reference_fields()
    {
        var catalog = Catalog(Node, Other);
        ObjectModelValidator.Validate(catalog);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(Identity(), catalog)));
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with { Roots = [Node, Node] }));
        var node = Layout(Node) with { Fields = [new(new(Node, 0x04000001), "link", FieldAttributes.Public, new ObjectStorage.Reference(Other))] };
        catalog = catalog with { Roots = [Node], Types = [Layout(Object), node, Layout(Other)] };
        ReferenceIrValidator.Validate(Program(Identity(), catalog));
    }

    [Fact]
    public void Reference_programs_cannot_enter_the_primitive_native_emitter()
    {
        var method = Identity(); ReferenceIrValidator.Validate(Program(method));
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new IrProgram(IrMethodId.From(method.Source), [method])));
        var nullOnly = Method(0x06000001, [], IrType.Int32,
            [Define(0, IrType.NullReference, new IrExpression.Null()), Define(1, IrType.Int32, new IrExpression.Constant(1))], 1);
        ReferenceIrValidator.Validate(Program(nullOnly, Catalog(Object)));
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(nullOnly));
    }

    [Fact]
    public void Aggregate_reference_storage_obeys_existing_program_limits()
    {
        var callee = Identity() with { Source = Identity().Source with { Token = 0x06000002 },
            Locals = [.. Enumerable.Repeat(NodeRef, IrLimits.MaximumLocals)] };
        var entry = Method(0x06000001, [NodeRef], NodeRef, [Define(0, NodeRef, new IrExpression.Parameter(0)),
            Define(1, NodeRef, new IrExpression.Call(IrMethodId.From(callee.Source), [0]), true)], 1, [NodeRef]);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(IrMethodId.From(entry.Source), [entry, callee], Catalog(Node))));
    }

    [Theory]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void Reference_call_arguments_obey_the_exact_aggregate_boundary(int calls, bool accepted)
    {
        var callee = Identity() with { Source = Identity().Source with { Token = 0x06000002 },
            Parameters = [.. Enumerable.Repeat(NodeRef, 16)] };
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        statements.Add(Define(0, NodeRef, new IrExpression.Parameter(0)));
        for (int id = 1; id <= calls; id++) statements.Add(Define(id, NodeRef,
            new IrExpression.Call(IrMethodId.From(callee.Source), [.. Enumerable.Repeat(0, 16)]), true));
        var entry = Method(0x06000001, [NodeRef], NodeRef, statements.ToImmutable(), calls);
        var program = new ReferenceIrProgram(IrMethodId.From(entry.Source), [entry, callee], Catalog(Node));
        if (accepted) ReferenceIrValidator.Validate(program);
        else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(program));
    }

    [Theory]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public void Reference_stack_inputs_and_edge_copies_obey_the_exact_boundary(int inputs, bool accepted)
    {
        var method = Identity();
        method = method with { Blocks = [method.Blocks[0] with { Terminator = new IrJump(1,
            new(1, [.. Enumerable.Repeat(0, inputs)])) }, new(1, 2, [.. Enumerable.Repeat(NodeRef, inputs)],
                [new IrStatement.Define(new(0, NodeRef, 2, new IrExpression.StackInput(inputs - 1)))], new IrReturn(3, 0))] };
        if (accepted) ReferenceIrValidator.Validate(Program(method));
        else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
    }

    [Theory]
    [InlineData(Int32Comparison.Equal, true)]
    [InlineData(Int32Comparison.NotEqual, true)]
    [InlineData(Int32Comparison.LessUnsigned, false)]
    [InlineData((Int32Comparison)99, false)]
    public void Binary_reference_branches_check_the_comparison_kind(Int32Comparison comparison, bool accepted)
    {
        var method = Join(NodeRef, NodeRef, NodeRef);
        method = method with { Blocks = [method.Blocks[0] with {
            Terminator = new IrBranch(1, comparison, 1, 2, new(1, [1]), new(1, [2])) }, method.Blocks[1]] };
        if (accepted) ReferenceIrValidator.Validate(Program(method));
        else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
    }

    [Fact]
    public void Integer_fault_edges_remain_required_inside_reference_value_flow()
    {
        var method = Method(0x06000001, [NodeRef], IrType.Int32,
            [Define(0, IrType.Int32, new IrExpression.Constant(1)), Define(1, IrType.Int32, new IrExpression.Constant(0)),
             Define(2, IrType.Int32, new IrExpression.Binary(Int32Operation.DivideSigned, 0, 1), true)], 2);
        ReferenceIrValidator.Validate(Program(method));
        var block = method.Blocks[0];
        method = method with { Blocks = [block with { Statements = block.Statements.SetItem(2,
            ((IrStatement.Define)block.Statements[2]) with { Exceptional = null }) }] };
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
    }
}
