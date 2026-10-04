using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ManagedExceptionIrTests
{
    internal static ManagedExceptionIrProgram Model()
    {
        string hash = new('e', 64);
        ObjectTypeId Id(int row) => new(hash, 0x02000000 + row);
        TypeSource Source(int row) => new("exceptions", hash, Id(row).Token, "Type" + row);
        var classes = ImmutableArray.CreateBuilder<ObjectTypeLayout>();
        classes.Add(new(Source(2), null, TypeAttributes.Public, []));
        for (int i = 3; i <= 12; ++i) classes.Add(new(Source(i), Id(i == 3 ? 2 : 3), TypeAttributes.Public, []));
        var references = new ObjectTypeCatalog([.. Enumerable.Range(3, 10).Select(Id)], Id(2), classes.ToImmutable());
        uint[] statuses = [1, 2, 4, 5, 7, 10, 11, 12];
        var exceptions = new ManagedExceptionTypes(Id(3), [.. Enumerable.Range(3, 10).Select(Id)],
            [.. statuses.Select((status, index) => new RuntimeExceptionBinding(status, Id(index + 4)))]);
        var source = new MethodSource("exceptions", hash, 0x06000001, "Execute");
        var method = new IrMethod(source, [], IrType.Int32,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Reference(Id(12)), 0, new IrExpression.NewException(Id(12))), IrExceptionTarget.MethodExit)], new IrThrow(1, 0)),
             new(1, 2, [IrType.Reference(Id(3))], [new IrStatement.Define(new(0, IrType.Reference(Id(3)), 2, new IrExpression.StackInput(0))),
                 new IrStatement.StoreLocal(2, 0, 0)], new IrLeave(3, new(3, []))),
             new(2, 4, [], [new IrStatement.Define(new(0, IrType.Int32, 4, new IrExpression.Constant(5))),
                 new IrStatement.StoreLocal(4, 1, 0)], new IrEndFinally(5)),
             new(3, 6, [], [new IrStatement.Define(new(0, IrType.Int32, 6, new IrExpression.Local(1)))], new IrReturn(7, 0))],
             [IrType.Reference(Id(3)), IrType.Int32])
        { ExceptionRegions = [new(0, 1, 1, 2, IrExceptionClause.Catch, Id(3)), new(0, 2, 2, 3, IrExceptionClause.Finally)] };
        var id = IrMethodId.From(source);
        return new(new(new(new(id, [method], new([], Id(14), [], references))),
            [new(id, Id(13), ObjectMethodKind.Static, MethodAttributes.Public | MethodAttributes.Static, true)]), exceptions);
    }
    private static ManagedExceptionIrProgram Method(ManagedExceptionIrProgram program, Func<IrMethod, IrMethod> change)
    {
        var flow = program.Flow.Flow.Flow;
        return program with { Flow = program.Flow with { Flow = new(flow with { Methods = [change(flow.Methods[0])] }) } };
    }
    [Fact]
    public void Nested_catch_and_finally_reserve_exception_identity_roots()
    {
        var program = Model(); ManagedExceptionIrValidator.Validate(program);
        var roots = Assert.Single(ValueRootAssignment.Assign(program));
        Assert.Equal(4, roots.Slots.Count(s => s.Kind == ObjectRootKind.Exception));
        Assert.Single(roots.Safepoints);
        Assert.Throws<ArgumentException>(() => DelegateValueIrValidator.Validate(program.Flow));
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerDelegates(program.Flow));
    }
    [Theory]
    [InlineData("return-try")]
    [InlineData("branch-handler")]
    [InlineData("ordinary-exit")]
    [InlineData("rethrow-outside")]
    [InlineData("cleanup-exit")]
    [InlineData("bad-catch")]
    [InlineData("overlap")]
    [InlineData("missing-edge")]
    public void Invalid_region_flow_and_exception_contracts_are_rejected(string kind)
    {
        var program = Method(Model(), method => kind switch
        {
            "return-try" => method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with
                { Statements = method.Blocks[0].Statements.Add(new IrStatement.Define(new(1, IrType.Int32, 0, new IrExpression.Constant(0)))), Terminator = new IrReturn(1, 1) }) },
            "branch-handler" => method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with { Terminator = new IrJump(1, new(1, [0])) }) },
            "ordinary-exit" => method with { Blocks = method.Blocks.SetItem(1, method.Blocks[1] with { Terminator = new IrJump(3, new(3, [])) }) },
            "rethrow-outside" => method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with { Terminator = new IrRethrow(1) }) },
            "cleanup-exit" => method with { Blocks = method.Blocks.SetItem(2, method.Blocks[2] with { Terminator = new IrLeave(5, new(3, [])) }) },
            "bad-catch" => method with { ExceptionRegions = method.ExceptionRegions.SetItem(0, method.ExceptionRegions[0] with { CatchType = Model().Flow.Flow.Flow.Types.References.ObjectBase }) },
            "overlap" => method with { ExceptionRegions = method.ExceptionRegions.SetItem(0, method.ExceptionRegions[0] with { TryEnd = 2 }) },
            _ => method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with
                { Statements = [((IrStatement.Define)method.Blocks[0].Statements[0]) with { Exceptional = null }] }) },
        });
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
    }
    [Fact]
    public void Runtime_fault_types_are_complete_distinct_and_nominal()
    {
        var program = Model();
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with
            { Exceptions = program.Exceptions with { RuntimeFaults = program.Exceptions.RuntimeFaults.RemoveAt(0) } }));
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with
            { Exceptions = program.Exceptions with { RuntimeFaults = program.Exceptions.RuntimeFaults.SetItem(0, program.Exceptions.RuntimeFaults[1] with { Status = 1 }) } }));
    }
    [Fact]
    public void Rethrow_is_valid_inside_a_catch()
    {
        var program = Method(Model(), method => method with { Blocks = method.Blocks.SetItem(1, method.Blocks[1] with { Terminator = new IrRethrow(3) })
            .SetItem(2, method.Blocks[2] with { Terminator = new IrEndFinally(5) }).RemoveAt(3),
            ExceptionRegions = method.ExceptionRegions });
        ManagedExceptionIrValidator.Validate(program);
    }
    [Fact]
    public void Saved_exception_roots_count_toward_the_existing_phase_A_bound()
    {
        var program = Method(Model(), method => method with { Blocks = method.Blocks.SetItem(0, method.Blocks[0] with
        {
            Statements = [.. Enumerable.Range(0, 1018).Select(id => (IrStatement)new IrStatement.Define(
                ((IrStatement.Define)method.Blocks[0].Statements[0]).Value with { Id = id }, IrExceptionTarget.MethodExit))],
        }) });
        ManagedExceptionIrValidator.Validate(program);
        Assert.Throws<ArgumentException>(() => ValueRootAssignment.Assign(program));
    }

    internal static ManagedExceptionIrProgram FilterModel()
    {
        var program = Model(); var basis = program.Exceptions.Base; var custom = program.Exceptions.Types[^1];
        return Method(program, method => method with
        {
            Blocks = [method.Blocks[0],
                new(1, 2, [IrType.Reference(basis)],
                    [new IrStatement.Define(new(0, IrType.Reference(basis), 2, new IrExpression.StackInput(0))),
                     new IrStatement.Define(new(1, IrType.Reference(custom), 2, new IrExpression.IsInstance(custom, 0)), IrExceptionTarget.MethodExit),
                     new IrStatement.Define(new(2, IrType.NullReference, 2, new IrExpression.Null())),
                     new IrStatement.Define(new(3, IrType.Int32, 2, new IrExpression.Compare(Int32Comparison.NotEqual, 1, 2)))], new IrEndFilter(3, 3)),
                new(2, 4, [IrType.Reference(basis)], [new IrStatement.Define(new(0, IrType.Reference(basis), 4, new IrExpression.StackInput(0))),
                    new IrStatement.StoreLocal(4, 0, 0)], new IrLeave(5, new(4, []))),
                new(3, 6, [], [new IrStatement.Define(new(0, IrType.Int32, 6, new IrExpression.Constant(5))),
                    new IrStatement.StoreLocal(6, 1, 0)], new IrEndFinally(7)),
                new(4, 8, [], [new IrStatement.Define(new(0, IrType.Int32, 8, new IrExpression.Local(1)))], new IrReturn(9, 0))],
            ExceptionRegions = [new(0, 1, 2, 3, IrExceptionClause.Filter, FilterStart: 1), new(0, 3, 3, 4, IrExceptionClause.Finally)],
        });
    }
    [Fact]
    public void Pure_type_filter_is_accepted_and_stateful_or_cyclic_filters_are_rejected()
    {
        var program = FilterModel(); ManagedExceptionIrValidator.Validate(program);
        var allocation = Method(program, method => method with { Blocks = method.Blocks.SetItem(1, method.Blocks[1] with
        {
            Statements = method.Blocks[1].Statements.SetItem(1, new IrStatement.Define(new(1,
                IrType.Reference(program.Exceptions.Types[^1]), 2, new IrExpression.NewException(program.Exceptions.Types[^1])), IrExceptionTarget.MethodExit)),
        }) });
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(allocation));
        var cycle = Method(program, method => method with { Blocks = method.Blocks.SetItem(1, method.Blocks[1] with
            { Terminator = new IrJump(3, new(1, [0])) }) });
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(cycle));
        var oldLocal = Method(program, method => method with { Blocks = method.Blocks.SetItem(1, method.Blocks[1] with
            { Statements = method.Blocks[1].Statements.SetItem(0, new IrStatement.Define(new(0, IrType.Reference(program.Exceptions.Base), 2, new IrExpression.Local(0)))) }) });
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(oldLocal));
    }

    [Fact]
    public void Exception_creation_cannot_bypass_type_visibility()
    {
        var program = Model(); var flow = program.Flow.Flow.Flow;
        var references = flow.Types.References;
        references = references with { Types = references.Types.SetItem(references.Types.Length - 1,
            references.Types[^1] with { Attributes = TypeAttributes.NotPublic }) };
        program = program with { Flow = program.Flow with { Flow = new(flow with { Types = flow.Types with { References = references } }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program));
    }

}
