using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class Int32BitwiseTests
{
    [Theory]
    [InlineData(ILOpCode.And, Int32Operation.BitwiseAnd)]
    [InlineData(ILOpCode.Or, Int32Operation.BitwiseOr)]
    [InlineData(ILOpCode.Xor, Int32Operation.BitwiseXor)]
    [InlineData(ILOpCode.Shl, Int32Operation.ShiftLeft)]
    [InlineData(ILOpCode.Shr, Int32Operation.ShiftRightSigned)]
    [InlineData(ILOpCode.Shr_un, Int32Operation.ShiftRightUnsigned)]
    public void Raw_binary_operations_preserve_value_then_count_or_right_operand(ILOpCode opcode, Int32Operation operation)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var root = builder.Method("Value", [0, 2, 8, 8, 8], emit: il =>
        { il.LoadArgument(0); il.LoadArgument(1); il.OpCode(opcode); il.OpCode(ILOpCode.Ret); });
        using var resolver = files.Open();
        var result = Int32Translator.TranslateProgram(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, root);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var block = Assert.Single(Assert.Single(result.Program!.Methods).Blocks);
        Assert.Equal(new IrExpression.Binary(operation, 0, 1), block.Values[2].Expression);
        Assert.Equal(2, block.Values[2].ILOffset); Assert.Equal(IrType.Int32, block.Values[2].Type);
        Assert.Equal(2, Assert.IsType<IrReturn>(block.Terminator).Value);
        Assert.NotEmpty(CppEmitter.EmitProgram(result.Program).Text);
    }

    [Fact]
    public void Complement_consumes_one_value_and_defines_a_new_Int32_temporary()
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var root = builder.Method("Value", [0, 1, 8, 8], emit: il =>
        { il.LoadArgument(0); il.OpCode(ILOpCode.Not); il.OpCode(ILOpCode.Ret); });
        using var resolver = files.Open();
        var result = Int32Translator.Translate(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, root);
        Assert.True(result.Success);
        var value = Assert.Single(result.Method!.Blocks).Values[1];
        Assert.Equal(new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, 0), value.Expression);
        Assert.Equal(1, value.ILOffset); Assert.Equal(IrType.Int32, value.Type);
        Assert.NotEmpty(CppEmitter.Emit(result.Method).Text);
    }

    [Theory]
    [InlineData(ILOpCode.And, 0)]
    [InlineData(ILOpCode.And, 1)]
    [InlineData(ILOpCode.Or, 0)]
    [InlineData(ILOpCode.Or, 1)]
    [InlineData(ILOpCode.Xor, 0)]
    [InlineData(ILOpCode.Xor, 1)]
    [InlineData(ILOpCode.Shl, 0)]
    [InlineData(ILOpCode.Shl, 1)]
    [InlineData(ILOpCode.Shr, 0)]
    [InlineData(ILOpCode.Shr, 1)]
    [InlineData(ILOpCode.Shr_un, 0)]
    [InlineData(ILOpCode.Shr_un, 1)]
    [InlineData(ILOpCode.Not, 0)]
    public void Missing_operands_are_stack_errors_with_no_partial_program(ILOpCode opcode, int operands)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var root = builder.Method("Bad", [0, 0, 8], emit: il =>
        { for (int n = 0; n < operands; n++) il.LoadConstantI4(1); il.OpCode(opcode); il.OpCode(ILOpCode.Ret); });
        using var resolver = files.Open();
        var result = Int32Translator.TranslateProgram(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, root);
        Assert.Null(result.Program);
        var diagnostic = Assert.Single(result.Diagnostics); Assert.Equal(DiagnosticIds.InconsistentStack, diagnostic.Id);
        Assert.Contains("IL_" + operands.ToString("x4", System.Globalization.CultureInfo.InvariantCulture), diagnostic.Location!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unary_operation")]
    [InlineData("unary_forward")]
    [InlineData("unary_negative")]
    [InlineData("binary_operation")]
    [InlineData("shift_forward")]
    [InlineData("shift_negative")]
    public void Forged_operations_and_operands_cannot_bypass_validation(string damage)
    {
        IrExpression expression = damage switch
        {
            "unary_operation" => new IrExpression.Unary((Int32UnaryOperation)99, 0),
            "unary_forward" => new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, 1),
            "unary_negative" => new IrExpression.Unary(Int32UnaryOperation.BitwiseNot, -1),
            "binary_operation" => new IrExpression.Binary((Int32Operation)99, 0, 0),
            "shift_forward" => new IrExpression.Binary(Int32Operation.ShiftRightSigned, 0, 1),
            _ => new IrExpression.Binary(Int32Operation.ShiftLeft, -1, 0),
        };
        var method = new IrMethod(new("ignored", new string('a', 64), 0x06000001, "ignored"), [], IrType.Int32,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(1))),
                new IrStatement.Define(new(1, IrType.Int32, 1, expression))], new IrReturn(2, 1))], []);
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method));
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(IrMethodId.From(method.Source), [method])));
    }

    [Theory]
    [InlineData(ILOpCode.Starg)]
    [InlineData(ILOpCode.Starg_s)]
    public void Argument_mutation_remains_an_explicit_unsupported_boundary(ILOpCode opcode)
    {
        using var files = new ResolverFiles(); var builder = Builder();
        var root = builder.Method("Bad", [0, 1, 8, 8], emit: il =>
        {
            il.LoadConstantI4(1); il.OpCode(opcode);
            if (opcode == ILOpCode.Starg_s) il.CodeBuilder.WriteByte(0); else il.CodeBuilder.WriteUInt16(0);
            il.LoadArgument(0); il.OpCode(ILOpCode.Ret);
        });
        using var resolver = files.Open();
        var result = Int32Translator.TranslateProgram(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, root);
        Assert.Null(result.Program);
        var diagnostic = Assert.Single(result.Diagnostics); Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
        Assert.Contains("starg", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("IL_0001", diagnostic.Location!, StringComparison.Ordinal);
    }

    private static ResolutionAssemblyBuilder Builder()
    {
        var builder = new ResolutionAssemblyBuilder(new("Bits", new Version(1, 0, 0, 0)));
        builder.Type("", "Entry"); return builder;
    }
}
