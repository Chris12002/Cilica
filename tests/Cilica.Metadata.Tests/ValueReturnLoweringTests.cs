using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueReturnLoweringTests
{
    private static (ManagedLocationIrProgram Program, ImmutableArray<ObjectMethodBinding> Bindings) Model(int fields, int calls = 1, bool parameter = false)
    {
        var (program, bindings) = NativeValueReturnTests.Model(); var catalog = program.Flow.Types;
        var leaf = catalog.Types.Single(); var id = ObjectTypeId.From(leaf.Source); var type = IrType.Value(id);
        leaf = leaf with { Fields = [.. Enumerable.Range(1, fields).Select(row => new ObjectField(new(id, 0x04000000 | row),
            "Field" + row, FieldAttributes.Public, new ObjectStorage.Int32()))] };
        var references = catalog.References with { Roots = [catalog.References.ObjectBase], String = null,
            Types = [catalog.References.Types.Single(t => ObjectTypeId.From(t.Source) == catalog.References.ObjectBase)] };
        catalog = catalog with { Types = [leaf], References = references };
        var helper = program.Flow.Methods.Single(m => m.Source.Name == "Make") with { Parameters = parameter ? [IrType.Int32] : [],
            Blocks = [new(0, 0, [], [NativeValueBorrowTests.D(0, type, new IrExpression.DefaultValue(id))], new IrReturn(1, 0))] };
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        statements.Add(NativeValueBorrowTests.D(0, IrType.Int32, new IrExpression.Constant(7)));
        for (int i = 0; i < calls; ++i) statements.Add(NativeValueBorrowTests.D(i + 1, type,
            new IrExpression.Call(IrMethodId.From(helper.Source), parameter ? [0] : []), true));
        var entry = program.Flow.Methods[0] with { Parameters = [], Blocks = [new(0, 0, [], statements.ToImmutable(), new IrReturn(1, 0))] };
        return (new(new(program.Flow.Entry, [helper, entry], catalog)),
            [bindings.Single(b => b.Method == IrMethodId.From(helper.Source)), bindings.Single(b => b.Method == program.Flow.Entry)]);
    }

    [Fact]
    public void Empty_returns_keep_the_call_but_need_no_result_storage()
    {
        var (program, bindings) = Model(0);
        var lowered = ValueScalarLowering.LowerLocations(program, bindings).Flow.Flow;
        Assert.Equal(IrType.Void, lowered.Methods[0].ReturnType); Assert.Empty(lowered.Methods[0].Parameters);
        Assert.Empty(lowered.Methods[1].Locals);
        Assert.Empty(Assert.Single(lowered.Methods[1].Blocks[0].Statements.OfType<IrStatement.Invoke>()).Arguments);
    }

    [Theory]
    [InlineData("parameters")]
    [InlineData("locals")]
    [InlineData("roots")]
    public void Hidden_return_storage_is_charged_to_existing_resource_limits(string limit)
    {
        var (program, bindings) = limit switch
        {
            "parameters" => Model(4096, parameter: true),
            "locals" => Model(64, calls: 65),
            _ => Model(1, calls: 1025),
        };
        _ = ValueRootAssignment.Assign(program);
        string expected = limit switch { "parameters" => "return parameter limit", "locals" => "return packet local limit", _ => "root-slot limit" };
        Assert.Contains(expected, Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerLocations(program, bindings)).Message, StringComparison.Ordinal);
    }
}
