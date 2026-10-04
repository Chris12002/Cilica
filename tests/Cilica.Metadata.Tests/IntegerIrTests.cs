using System;
using System.Collections.Immutable;
using System.Linq;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class IntegerIrTests
{
    internal static bool Shift(Int64Operation op) => op is Int64Operation.ShiftLeft or Int64Operation.ShiftRightSigned or Int64Operation.ShiftRightUnsigned;
    internal static bool Faults(Int64Operation op) => op is Int64Operation.DivideSigned or Int64Operation.DivideUnsigned
        or Int64Operation.RemainderSigned or Int64Operation.RemainderUnsigned or Int64Operation.AddCheckedSigned
        or Int64Operation.SubtractCheckedSigned or Int64Operation.MultiplyCheckedSigned or Int64Operation.AddCheckedUnsigned
        or Int64Operation.SubtractCheckedUnsigned or Int64Operation.MultiplyCheckedUnsigned;
    internal static MethodSource Source(int token = 0x06000001) => new("Authored, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", new string('a', 64), token, "Authored.Wide");
    internal static IntegerIrProgram Program(IrMethod method) => new(new(IrMethodId.From(method.Source), [method]));
    internal static IrMethod Binary(Int64Operation op)
    {
        var right = Shift(op) ? IrType.Int32 : IrType.Int64;
        return new(Source(0x06000001 + (int)op), [IrType.Int64, right], IrType.Int64,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int64, 0, new IrExpression.Parameter(0))),
                new IrStatement.Define(new(1, right, 1, new IrExpression.Parameter(1))),
                new IrStatement.Define(new(2, IrType.Int64, 2, new IrExpression.Binary64(op, 0, 1)), Faults(op) ? IrExceptionTarget.MethodExit : null)], new IrReturn(3, 2))], []);
    }

    [Fact]
    public void Existing_profiles_reject_wide_IR_and_explicit_integer_profile_validates_it()
    {
        var program = Program(Binary(Int64Operation.AddWrapping));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(program.Flow));
        Assert.Contains(IntegerIrProgram.Profile, CppEmitter.EmitIntegers(program).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("result")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("unknown")]
    [InlineData("missing-fault")]
    [InlineData("extra-fault")]
    [InlineData("forged-type")]
    public void Invalid_wide_operations_do_not_reach_native_emission(string mutation)
    {
        var method = Binary(mutation == "missing-fault" ? Int64Operation.AddCheckedSigned : Int64Operation.AddWrapping);
        var block = method.Blocks[0]; var statements = block.Statements.ToArray(); var define = (IrStatement.Define)statements[2];
        statements[2] = mutation switch
        {
            "result" => define with { Value = define.Value with { Type = IrType.Int32 } },
            "unknown" => define with { Value = define.Value with { Expression = new IrExpression.Binary64((Int64Operation)999, 0, 1) } },
            "missing-fault" => define with { Exceptional = null },
            "extra-fault" => define with { Exceptional = IrExceptionTarget.MethodExit },
            "forged-type" => define with { Value = define.Value with { Type = IrType.Int64 with { ReadOnly = true } } },
            _ => define,
        };
        if (mutation is "left" or "right")
        {
            int i = mutation == "left" ? 0 : 1;
            statements[i] = new IrStatement.Define(new(i, IrType.Int32, i, new IrExpression.Constant(7)));
        }
        method = method with { Blocks = [block with { Statements = [.. statements] }] };
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitIntegers(Program(method)));
    }

    [Fact]
    public void Shift_counts_and_conversions_have_exact_widths()
    {
        var method = Binary(Int64Operation.ShiftLeft);
        Assert.NotEmpty(CppEmitter.EmitIntegers(Program(method)).Text);
        var block = method.Blocks[0]; var items = block.Statements.ToArray();
        items[1] = new IrStatement.Define(new(1, IrType.Int64, 1, new IrExpression.Parameter(1)));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitIntegers(Program(method with { Parameters = [IrType.Int64, IrType.Int64], Blocks = [block with { Statements = [.. items] }] })));
        foreach (var conversion in Enum.GetValues<IntegerConversion>())
        {
            var input = conversion == IntegerConversion.Truncate64 ? IrType.Int64 : IrType.Int32;
            var output = conversion == IntegerConversion.Truncate64 ? IrType.Int32 : IrType.Int64;
            var valid = Conversion(conversion, input, output);
            Assert.NotEmpty(CppEmitter.EmitIntegers(Program(valid)).Text);
            Assert.Throws<ArgumentException>(() => CppEmitter.EmitIntegers(Program(Conversion(conversion, output, input))));
        }
    }

    internal static IrMethod Conversion(IntegerConversion conversion, IrType input, IrType output) => new(Source(0x06000100 + (int)conversion), [input], output,
        [new(0, 0, [], [new IrStatement.Define(new(0, input, 0, new IrExpression.Parameter(0))),
            new IrStatement.Define(new(1, output, 1, new IrExpression.ConvertInteger(conversion, 0)))], new IrReturn(2, 1))], []);

    internal static IrMethod Compare(Int32Comparison comparison) => new(Source(0x06000200 + (int)comparison), [IrType.Int64, IrType.Int64], IrType.Int32,
        [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int64, 0, new IrExpression.Parameter(0))),
            new IrStatement.Define(new(1, IrType.Int64, 1, new IrExpression.Parameter(1))),
            new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Compare(comparison, 0, 1)))], new IrReturn(3, 2))], []);
}
