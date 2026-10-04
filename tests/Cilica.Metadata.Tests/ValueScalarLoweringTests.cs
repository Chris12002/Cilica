using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueScalarLoweringTests
{
    [Fact]
    public void Lowering_uses_scalar_reference_packets_without_boxing_or_struct_layouts()
    {
        var (program, _) = NativeValueCopyTests.Model(false);
        var lowered = ValueScalarLowering.Lower(program);
        var helper = lowered.Methods.Single(m => m.Source.Name == "Read");
        Assert.Equal(3, helper.Parameters.Length);
        Assert.Equal(new[] { IrTypeKind.Int32, IrTypeKind.Reference, IrTypeKind.Reference }, helper.Parameters.Select(p => p.Kind));
        Assert.All(lowered.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v =>
            Assert.IsNotType<IrExpression.NewObject>(v.Expression));
        Assert.All(lowered.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values).Select(v => v.Expression).OfType<IrExpression.Call>(),
            call => Assert.Equal(3, call.Arguments.Length));
    }

    [Fact]
    public void Struct_returns_fail_explicitly_instead_of_losing_fields()
    {
        var (program, _) = NativeValueCopyTests.Model(false);
        var helper = program.Methods[1]; var type = helper.Parameters[0];
        helper = helper with { ReturnType = type, Blocks = [helper.Blocks[0] with
            { Statements = [helper.Blocks[0].Statements[0]], Terminator = new IrReturn(1, 0) }] };
        var field = program.Types.Types[0].Fields[0].Id;
        var entry = program.Methods[0] with { Blocks = [new(0, 0, [],
            [new IrStatement.Define(new(0, type, 0, new IrExpression.DefaultValue(type.Class!))),
             new IrStatement.Define(new(1, type, 0, new IrExpression.Call(IrMethodId.From(helper.Source), [0])), IrExceptionTarget.MethodExit),
             new IrStatement.Define(new(2, IrType.Int32, 0, new IrExpression.ValueField(field, 1)))], new IrReturn(1, 2))], Locals = [] };
        program = program with { Methods = [entry, helper] };
        ValueIrValidator.Validate(program);
        Assert.Contains("struct return packets", Assert.Throws<ArgumentException>(() => ValueScalarLowering.Lower(program)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_stores_cannot_bypass_the_expanded_statement_budget()
    {
        var (program, _) = NativeValueCopyTests.Model(false);
        var value = program.Types.Types[0]; var id = ObjectTypeId.From(value.Source); var type = IrType.Value(id);
        value = value with { Fields = [.. Enumerable.Range(1, 128).Select(row => new ObjectField(new(id, 0x04000000 | row),
            "Field" + row, FieldAttributes.Public, new ObjectStorage.Int32()))] };
        var references = program.Types.References;
        references = references with { Roots = [references.ObjectBase], String = null };
        var catalog = program.Types with { Types = [value], References = references };
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        statements.Add(new IrStatement.Define(new(0, type, 0, new IrExpression.DefaultValue(id))));
        for (int i = 0; i < 512; ++i) statements.Add(new IrStatement.StoreLocal(0, 0, 0));
        statements.Add(new IrStatement.Define(new(1, IrType.Int32, 0, new IrExpression.Constant(0))));
        var entry = program.Methods[0] with { Blocks = [new(0, 0, [], statements.ToImmutable(), new IrReturn(1, 1))], Locals = [type] };
        program = program with { Types = catalog, Methods = [entry] };
        ValueIrValidator.Validate(program); ValueRootAssignment.Assign(program);
        Assert.Contains("expanded statement limit", Assert.Throws<ArgumentException>(() => ValueScalarLowering.Lower(program)).Message, StringComparison.Ordinal);
    }
}
