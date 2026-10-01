using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class Int32ControlFlowTests
{
    private static ResolutionAssemblyBuilder Builder()
    {
        var builder = new ResolutionAssemblyBuilder(new("Flow", new Version(1, 0, 0, 0)));
        builder.Type("", "Entry"); return builder;
    }
    private static TranslationResult Translate(ResolutionAssemblyBuilder builder, MethodDefinitionHandle method)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        return Int32Translator.Translate(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, method);
    }
    private static byte[] Locals(int count)
    {
        var signature = new BlobBuilder(); signature.WriteByte(7); signature.WriteCompressedInteger(count);
        for (int index = 0; index < count; index++) signature.WriteByte(8);
        return signature.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(255)]
    [InlineData(256)]
    public void Local_encodings_and_ordered_stores_retain_the_declared_index(int index)
    {
        var builder = Builder(); var method = builder.Method("Local", [0, 0, 8], locals: Locals(index + 1), emit: il =>
        { il.LoadConstantI4(31); il.StoreLocal(index); il.LoadLocal(index); il.OpCode(ILOpCode.Ret); });
        var result = Translate(builder, method); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var block = Assert.Single(result.Method!.Blocks); Assert.Equal(index + 1, result.Method.Locals.Length);
        Assert.Equal(index, Assert.IsType<IrStatement.StoreLocal>(block.Statements[1]).Local);
        Assert.Equal(new IrExpression.Local(index), block.Values[1].Expression);
        Assert.Equal(1, Assert.IsType<IrReturn>(block.Terminator).Value);
    }

    [Theory]
    [InlineData("uninitialized", DiagnosticIds.ExecutionUnsupported)]
    [InlineData("bool", DiagnosticIds.ExecutionSignatureUnsupported)]
    [InlineData("int64", DiagnosticIds.ExecutionSignatureUnsupported)]
    [InlineData("pinned", DiagnosticIds.ExecutionSignatureUnsupported)]
    [InlineData("bad_index", DiagnosticIds.InvalidOperand)]
    [InlineData("store_underflow", DiagnosticIds.InconsistentStack)]
    public void Invalid_or_unsupported_local_storage_never_returns_ir(string kind, string expected)
    {
        var builder = Builder();
        byte[] signature = kind switch { "bool" => [7, 1, 2], "int64" => [7, 1, 0x0a], "pinned" => [7, 1, 0x45, 8], _ => [7, 1, 8] };
        var method = builder.Method("Bad", [0, 0, 8], locals: signature, initializeLocals: kind != "uninitialized", emit: il =>
        {
            if (kind == "store_underflow") il.StoreLocal(0);
            il.LoadLocal(kind == "bad_index" ? 1 : 0); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(builder, method); Assert.Null(result.Method); Assert.Equal(expected, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Initialized_locals_are_zero_and_stack_dup_pop_have_explicit_depth_effects()
    {
        var builder = Builder(); var method = builder.Method("Zero", [0, 0, 8], locals: [7, 1, 8], emit: il =>
        { il.LoadLocal(0); il.OpCode(ILOpCode.Dup); il.OpCode(ILOpCode.Pop); il.OpCode(ILOpCode.Ret); });
        var result = Translate(builder, method); Assert.True(result.Success);
        Assert.Single(result.Method!.Blocks[0].Values);
        Assert.Contains("std::uint32_t l0 = 0u;", CppEmitter.Emit(result.Method).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Joins_receive_ordered_typed_stack_inputs_or_diagnose_incompatible_depth(bool mismatch)
    {
        var builder = Builder(); var method = builder.Method("Join", [0, 1, 8, 8], emit: il =>
        {
            var taken = il.DefineLabel(); var join = il.DefineLabel();
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue_s, taken);
            il.LoadConstantI4(11); il.Branch(ILOpCode.Br_s, join);
            il.MarkLabel(taken); if (!mismatch) il.LoadConstantI4(22); else il.OpCode(ILOpCode.Nop);
            il.Branch(ILOpCode.Br_s, join); il.MarkLabel(join); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(builder, method);
        if (mismatch)
        {
            Assert.Null(result.Method); var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticIds.InconsistentStack, diagnostic.Id); Assert.Contains("incompatible", diagnostic.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.True(result.Success); var methodIr = result.Method!;
            Assert.Equal(4, methodIr.Blocks.Length); Assert.Equal(new[] { IrType.Int32 }, methodIr.Blocks[3].Inputs);
            Assert.Equal(new IrExpression.StackInput(0), Assert.Single(methodIr.Blocks[3].Values).Expression);
            Assert.Equal(new[] { 0 }, Assert.IsType<IrJump>(methodIr.Blocks[1].Terminator).Edge.Arguments);
            Assert.NotEmpty(CppEmitter.Emit(methodIr).Text);
        }
    }

    internal static (ResolutionAssemblyBuilder Builder, MethodDefinitionHandle Root) StackLoop()
    {
        var builder = Builder(); var root = builder.Method("StackLoop", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il =>
        {
            var body = il.DefineLabel(); var test = il.DefineLabel();
            il.LoadArgument(0); il.LoadArgument(1); il.StoreLocal(0); il.Branch(ILOpCode.Br_s, test);
            il.MarkLabel(body); il.LoadConstantI4(1); il.OpCode(ILOpCode.Add);
            il.LoadLocal(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(0);
            il.MarkLabel(test); il.LoadLocal(0); il.Branch(ILOpCode.Brtrue_s, body); il.OpCode(ILOpCode.Ret);
        });
        return (builder, root);
    }

    [Fact]
    public void Backedges_keep_live_stack_values_and_capture_inputs_before_transfer()
    {
        var fixture = StackLoop(); var result = Translate(fixture.Builder, fixture.Root); Assert.True(result.Success);
        Assert.Equal(4, result.Method!.Blocks.Length);
        var body = result.Method.Blocks[1]; var test = result.Method.Blocks[2];
        Assert.Single(body.Inputs); Assert.Single(test.Inputs);
        Assert.Equal(1, Assert.IsType<IrBranch>(test.Terminator).Taken.Target);
        Assert.Equal(new IrExpression.StackInput(0), body.Values[0].Expression);
        Assert.Contains("s1_0 = v0;", CppEmitter.Emit(result.Method).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Branches_to_an_operand_or_past_the_body_are_rejected(bool intoOperand)
    {
        var builder = Builder(); var root = builder.Method("Bad", [0, 0, 8], emit: il =>
        {
            if (intoOperand) il.LoadConstantI4(0x12345678);
            il.OpCode(ILOpCode.Br_s); il.CodeBuilder.WriteSByte(intoOperand ? (sbyte)-6 : (sbyte)0);
        });
        var result = Translate(builder, root); Assert.Null(result.Method);
        Assert.Equal(DiagnosticIds.InvalidControlTransfer, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(4095, true)]
    [InlineData(4096, false)]
    public void Block_budget_has_an_exact_accept_reject_boundary(int branches, bool accepted)
    {
        var builder = Builder(); var root = builder.Method("Chain", [0, 0, 8], emit: il =>
        {
            for (int index = 0; index < branches; index++)
            {
                // Every target is the next instruction: no label fixups needed.
                il.OpCode(ILOpCode.Br_s); il.CodeBuilder.WriteSByte(0);
            }
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(builder, root); Assert.True(accepted == result.Success, string.Join("; ", result.Diagnostics));
        if (accepted) Assert.Equal(IrLimits.MaximumBlocks, result.Method!.Blocks.Length);
        else Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Synthetic_stack_inputs_count_toward_the_aggregate_statement_budget(int depth, bool accepted)
    {
        var builder = Builder(); var root = builder.Method("DeepChain", [0, 0, 8], maxStack: depth, emit: il =>
        {
            for (int index = 0; index < depth; index++) il.LoadConstantI4(0);
            for (int index = 0; index < 4095; index++) { il.OpCode(ILOpCode.Br_s); il.CodeBuilder.WriteSByte(0); }
            for (int index = 1; index < depth; index++) il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(builder, root); Assert.True(accepted == result.Success, string.Join("; ", result.Diagnostics));
        if (accepted) Assert.Equal(IrLimits.MaximumStatements, result.Method!.Blocks.Sum(b => b.Statements.Length));
        else Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(2049, false)]
    public void Both_conditional_edges_count_toward_the_argument_copy_budget(int branches, bool accepted)
    {
        var builder = Builder(); var root = builder.Method("Copies", [0, 0, 8], maxStack: 17, emit: il =>
        {
            for (int index = 0; index < 16; index++) il.LoadConstantI4(0);
            for (int index = 0; index < branches; index++)
            { il.LoadConstantI4(1); il.OpCode(ILOpCode.Brtrue_s); il.CodeBuilder.WriteSByte(0); }
            for (int index = 1; index < 16; index++) il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(builder, root); Assert.True(accepted == result.Success, string.Join("; ", result.Diagnostics));
        if (!accepted)
        {
            var diagnostic = Assert.Single(result.Diagnostics); Assert.Equal(DiagnosticIds.TranslationLimit, diagnostic.Id);
            Assert.Contains("edge-argument", diagnostic.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(ILOpCode.Brfalse, Int32Comparison.Zero)]
    [InlineData(ILOpCode.Brfalse_s, Int32Comparison.Zero)]
    [InlineData(ILOpCode.Brtrue, Int32Comparison.Nonzero)]
    [InlineData(ILOpCode.Brtrue_s, Int32Comparison.Nonzero)]
    [InlineData(ILOpCode.Beq, Int32Comparison.Equal)]
    [InlineData(ILOpCode.Beq_s, Int32Comparison.Equal)]
    [InlineData(ILOpCode.Bne_un, Int32Comparison.NotEqual)]
    [InlineData(ILOpCode.Bne_un_s, Int32Comparison.NotEqual)]
    [InlineData(ILOpCode.Blt, Int32Comparison.LessSigned)]
    [InlineData(ILOpCode.Blt_s, Int32Comparison.LessSigned)]
    [InlineData(ILOpCode.Bgt, Int32Comparison.GreaterSigned)]
    [InlineData(ILOpCode.Bgt_s, Int32Comparison.GreaterSigned)]
    [InlineData(ILOpCode.Ble, Int32Comparison.LessOrEqualSigned)]
    [InlineData(ILOpCode.Ble_s, Int32Comparison.LessOrEqualSigned)]
    [InlineData(ILOpCode.Bge, Int32Comparison.GreaterOrEqualSigned)]
    [InlineData(ILOpCode.Bge_s, Int32Comparison.GreaterOrEqualSigned)]
    [InlineData(ILOpCode.Blt_un, Int32Comparison.LessUnsigned)]
    [InlineData(ILOpCode.Blt_un_s, Int32Comparison.LessUnsigned)]
    [InlineData(ILOpCode.Bgt_un, Int32Comparison.GreaterUnsigned)]
    [InlineData(ILOpCode.Bgt_un_s, Int32Comparison.GreaterUnsigned)]
    [InlineData(ILOpCode.Ble_un, Int32Comparison.LessOrEqualUnsigned)]
    [InlineData(ILOpCode.Ble_un_s, Int32Comparison.LessOrEqualUnsigned)]
    [InlineData(ILOpCode.Bge_un, Int32Comparison.GreaterOrEqualUnsigned)]
    [InlineData(ILOpCode.Bge_un_s, Int32Comparison.GreaterOrEqualUnsigned)]
    public void Conditional_encodings_preserve_predicate_signedness_and_operand_order(ILOpCode opcode, Int32Comparison comparison)
    {
        var builder = Builder(); var root = builder.Method("Compare", [0, 2, 8, 8, 8], emit: il =>
        {
            var taken = il.DefineLabel(); il.LoadArgument(0);
            if (comparison is not (Int32Comparison.Zero or Int32Comparison.Nonzero)) il.LoadArgument(1);
            il.Branch(opcode, taken); il.LoadConstantI4(-1); il.OpCode(ILOpCode.Ret);
            il.MarkLabel(taken); il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
        });
        var result = Translate(builder, root); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var branch = Assert.IsType<IrBranch>(result.Method!.Blocks[0].Terminator);
        Assert.Equal(comparison, branch.Comparison); Assert.Equal(0, branch.Left);
        Assert.Equal(comparison is Int32Comparison.Zero or Int32Comparison.Nonzero ? (int?)null : 1, branch.Right);
        Assert.NotEmpty(CppEmitter.Emit(result.Method).Text);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("arity")]
    [InlineData("input_type")]
    [InlineData("operand")]
    [InlineData("condition")]
    [InlineData("local")]
    [InlineData("unreachable")]
    public void Caller_constructed_control_flow_cannot_bypass_emitter_validation(string damage)
    {
        var fixture = StackLoop(); IrMethod method = Translate(fixture.Builder, fixture.Root).Method!;
        var blocks = method.Blocks; var test = blocks[2]; var branch = (IrBranch)test.Terminator;
        switch (damage)
        {
            case "target": blocks = blocks.SetItem(2, test with { Terminator = branch with { Taken = branch.Taken with { Target = 99 } } }); break;
            case "arity": blocks = blocks.SetItem(2, test with { Terminator = branch with { Taken = branch.Taken with { Arguments = [] } } }); break;
            case "input_type": blocks = blocks.SetItem(1, blocks[1] with { Inputs = [new IrType((IrTypeKind)99)] }); break;
            case "operand": blocks = blocks.SetItem(2, test with { Terminator = branch with { Left = 99 } }); break;
            case "condition": blocks = blocks.SetItem(2, test with { Terminator = branch with { Comparison = (Int32Comparison)99 } }); break;
            case "local": method = method with { Locals = [] }; break;
            default: blocks = blocks.Add(new(blocks.Length, 100, [], [new IrStatement.Define(new(0, IrType.Int32, 100, new IrExpression.Constant(1)))], new IrReturn(101, 0))); break;
        }
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method with { Blocks = blocks }));
    }
}
