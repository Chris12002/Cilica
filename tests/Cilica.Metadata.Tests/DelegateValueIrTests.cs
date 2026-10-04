using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DelegateValueIrTests
{
    internal static DelegateValueIrProgram Model()
    {
        string hash = new('d', 64);
        ObjectTypeId Id(int row) => new(hash, 0x02000000 + row);
        TypeSource Type(int row) => new("delegate-test", hash, Id(row).Token, "Type" + row);
        MethodSource Source(int row) => new("delegate-test", hash, 0x06000000 + row, "Method" + row);
        IrMethodId Method(int row) => IrMethodId.From(Source(row));
        var field = new ObjectField(new(Id(3), 0x04000001), "Number", FieldAttributes.Public, new ObjectStorage.Int32());
        var references = new ObjectTypeCatalog([Id(3), Id(4), Id(5)], Id(2),
            [new(Type(2), null, TypeAttributes.Public, []), new(Type(3), Id(2), TypeAttributes.Public, [field])])
        { Delegates = [new(Type(4), 1, false), new(Type(5), 1, true)] };
        IrStatement D(int id, int offset, IrType type, IrExpression expression, bool fault = false) =>
            new IrStatement.Define(new(id, type, offset, expression), fault ? IrExceptionTarget.MethodExit : null);
        IrMethod Body(int row, ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements, IrTerminator end) =>
            new(Source(row), parameters, result, [new(0, 0, [], statements, end)], []);
        var receiver = IrType.Reference(Id(3));
        var root = Body(1, [IrType.Int32], IrType.Int32,
            [D(0, 0, IrType.Int32, new IrExpression.Parameter(0)), D(1, 1, IrType.NullReference, new IrExpression.Null()),
             D(2, 2, receiver, new IrExpression.NewObject(Method(5), [0]), true),
             D(3, 3, IrType.Reference(Id(4)), new IrExpression.NewDelegate(Id(4), Method(2), 1), true),
             D(4, 4, IrType.Reference(Id(4)), new IrExpression.NewDelegate(Id(4), Method(3), 2), true),
             D(5, 5, IrType.Int32, new IrExpression.CallDelegate(Id(4), 3, [0]), true),
             D(6, 6, IrType.Int32, new IrExpression.CallDelegate(Id(4), 4, [5]), true),
             D(7, 7, IrType.Reference(Id(5)), new IrExpression.NewDelegate(Id(5), Method(4), 2), true),
             new IrStatement.InvokeDelegate(8, Id(5), 7, [6]),
             D(8, 9, IrType.Int32, new IrExpression.LoadField(field.Id, 2), true)], new IrReturn(10, 8));
        var add = Body(2, [IrType.Int32], IrType.Int32,
            [D(0, 0, IrType.Int32, new IrExpression.Parameter(0)), D(1, 1, IrType.Int32, new IrExpression.Constant(1)),
             D(2, 2, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 1))], new IrReturn(3, 2));
        var capture = Body(3, [receiver, IrType.Int32], IrType.Int32,
            [D(0, 0, receiver, new IrExpression.Parameter(0)), D(1, 1, IrType.Int32, new IrExpression.Parameter(1)),
             D(2, 2, IrType.Int32, new IrExpression.LoadField(field.Id, 0), true),
             D(3, 3, IrType.Int32, new IrExpression.Binary(Int32Operation.AddWrapping, 1, 2))], new IrReturn(4, 3));
        var setter = Body(4, [receiver, IrType.Int32], IrType.Void,
            [D(0, 0, receiver, new IrExpression.Parameter(0)), D(1, 1, IrType.Int32, new IrExpression.Parameter(1)),
             new IrStatement.StoreField(2, field.Id, 0, 1)], new IrVoidReturn(3));
        var ctor = Body(5, [receiver, IrType.Int32], IrType.Void,
            [D(0, 0, receiver, new IrExpression.Parameter(0)), D(1, 1, IrType.Int32, new IrExpression.Parameter(1)),
             new IrStatement.Invoke(2, Method(6), [0]), new IrStatement.StoreField(3, field.Id, 0, 1)], new IrVoidReturn(4));
        var baseCtor = Body(6, [IrType.Reference(Id(2))], IrType.Void, [], new IrVoidReturn(0));
        ObjectMethodBinding Binding(int row, int owner, ObjectMethodKind kind) => new(Method(row), Id(owner), kind,
            MethodAttributes.Public | MethodAttributes.HideBySig | (kind == ObjectMethodKind.Static ? MethodAttributes.Static : 0)
            | (kind is ObjectMethodKind.Constructor or ObjectMethodKind.ObjectBaseConstructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0), true);
        return new(new(new(Method(1), [root, add, capture, setter, ctor, baseCtor], new([], Id(7), [], references))),
            [Binding(1, 6, ObjectMethodKind.Static), Binding(2, 6, ObjectMethodKind.Static), Binding(3, 3, ObjectMethodKind.Instance),
             Binding(4, 3, ObjectMethodKind.Instance), Binding(5, 3, ObjectMethodKind.Constructor), Binding(6, 2, ObjectMethodKind.ObjectBaseConstructor)]);
    }

    [Fact]
    public void Static_and_captured_targets_are_reachable_and_rooted_with_void_invocation()
    {
        var program = Model(); DelegateValueIrValidator.Validate(program);
        var root = ValueRootAssignment.Assign(program).Single(p => p.Method == program.Flow.Flow.Entry);
        Assert.Equal(4, root.Safepoints.Count(p => p.Kind == ObjectSafepointKind.Allocation));
        Assert.Equal(3, root.Safepoints.Count(p => p.Kind == ObjectSafepointKind.Call));
        Assert.Contains(root.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 2);
        Assert.Contains(root.Slots, s => s.Kind == ObjectRootKind.Value && s.Index == 4);
    }

    [Fact]
    public void Existing_boxed_and_native_profiles_do_not_silently_accept_delegates()
    {
        var program = Model();
        Assert.Throws<ArgumentException>(() => BoxedValueIrValidator.Validate(new(program.Flow, program.Bindings)));
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitLocations(new(new(
            new(program.Flow.Flow.Entry, program.Flow.Flow.Methods, program.Flow.Flow.Types.References), program.Bindings))));
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("target")]
    [InlineData("static_receiver")]
    [InlineData("future_receiver")]
    [InlineData("wrong_delegate")]
    [InlineData("missing_fault")]
    [InlineData("invoke_arguments")]
    public void Invalid_delegate_shapes_targets_operands_and_fault_edges_are_rejected(string kind)
    {
        var program = Model(); var flow = program.Flow.Flow; var root = flow.Methods[0]; var block = root.Blocks[0];
        var statements = block.Statements.ToBuilder();
        int index = kind == "invoke_arguments" ? 5 : 3;
        var definition = (IrStatement.Define)statements[index];
        var creation = (IrExpression.NewDelegate)((IrStatement.Define)statements[3]).Value.Expression;
        IrExpression expression = kind switch
        {
            "signature" => creation with { Target = program.Bindings[3].Method },
            "target" => creation with { Target = new(creation.Target.ContentHash, 0x06009999) },
            "static_receiver" => creation with { Receiver = 2 },
            "future_receiver" => creation with { Receiver = 99 },
            "wrong_delegate" => creation with { Type = program.Bindings[2].Owner },
            "invoke_arguments" => ((IrExpression.CallDelegate)definition.Value.Expression) with { Arguments = [1] },
            _ => creation,
        };
        statements[index] = definition with { Value = definition.Value with { Expression = expression },
            Exceptional = kind == "missing_fault" ? null : definition.Exceptional };
        root = root with { Blocks = [block with { Statements = statements.ToImmutable() }] };
        program = program with { Flow = new(flow with { Methods = flow.Methods.SetItem(0, root) }) };
        Assert.Throws<ArgumentException>(() => DelegateValueIrValidator.Validate(program));
    }

    [Fact]
    public void Delegate_signatures_share_type_and_parameter_limits()
    {
        var catalog = Model().Flow.Flow.Types.References;
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with
            { Delegates = catalog.Delegates.SetItem(0, catalog.Delegates[0] with { ParameterCount = IrLimits.MaximumParameters }) }));
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog with
            { Delegates = catalog.Delegates.Add(catalog.Delegates[0]) }));
    }
    [Fact]
    public void Private_captured_methods_require_the_same_declaring_owner()
    {
        var program = Model(); var binding = program.Bindings[2];
        program = program with { Bindings = program.Bindings.SetItem(2, binding with
            { Attributes = (binding.Attributes & ~MethodAttributes.MemberAccessMask) | MethodAttributes.Private }) };
        Assert.Throws<ArgumentException>(() => DelegateValueIrValidator.Validate(program));
    }

    [Fact]
    public void Each_delegate_allocation_is_charged_to_the_phase_A_root_budget()
    {
        var program = Model(); var flow = program.Flow.Flow; var method = flow.Methods[0]; var block = method.Blocks[0];
        var capture = ((IrStatement.Define)block.Statements[3]).Value;
        var statements = block.Statements.ToBuilder();
        for (int i = 0; i < ObjectRootAssignment.MaximumRootsPerMethod; i++)
            statements.Add(new IrStatement.Define(capture with { Id = 9 + i, ILOffset = 20 + i }, IrExceptionTarget.MethodExit));
        method = method with { Blocks = [block with { Statements = statements.ToImmutable(), Terminator = new IrReturn(2000, 8) }] };
        program = program with { Flow = new(flow with { Methods = flow.Methods.SetItem(0, method) }) };
        DelegateValueIrValidator.Validate(program);
        Assert.Contains("1024", Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Void_invocations_require_a_void_shape_and_explicit_fault_exit()
    {
        var program = Model(); var flow = program.Flow.Flow; var method = flow.Methods[0]; var block = method.Blocks[0];
        var invoke = (IrStatement.InvokeDelegate)block.Statements[8];
        method = method with { Blocks = [block with { Statements = block.Statements.SetItem(8, invoke with { Exceptional = (IrExceptionTarget)99 }) }] };
        Assert.Throws<ArgumentException>(() => DelegateValueIrValidator.Validate(program with
            { Flow = new(flow with { Methods = flow.Methods.SetItem(0, method) }) }));
    }

}
