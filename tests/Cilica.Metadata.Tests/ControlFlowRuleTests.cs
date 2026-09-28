using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

/// <summary>
/// One reduced case per structural rule, built as synthetic IL. Each case also serves
/// as the regression for the defect noted beside it.
/// </summary>
public sealed class ControlFlowRuleTests
{
    [Fact]
    public void Region_ending_at_the_last_IL_byte_is_valid()
    {
        // Regression: the code size once included the body header and exception table.
        ILReport report = Single(il =>
        {
            LabelHandle tryStart = il.DefineLabel(), handlerStart = il.DefineLabel(), handlerEnd = il.DefineLabel();
            il.MarkLabel(tryStart);
            il.Branch(ILOpCode.Leave_s, tryStart);
            il.MarkLabel(handlerStart);
            il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(handlerEnd);
            il.ControlFlowBuilder!.AddFinallyRegion(tryStart, handlerStart, handlerStart, handlerEnd);
        });

        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    public void Virtual_token_reports_CIL1014()
    {
        // Regression: a token with the high bit set threw InvalidCastException.
        ILReport report = Single(il =>
        {
            il.OpCode(ILOpCode.Ldtoken);
            il.Token(unchecked((int)0x9B000001));
            il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ret);
        });

        Assert.Equal(DiagnosticIds.InvalidOperand, Assert.Single(report.Diagnostics).Id);
    }

    [Fact]
    public void Inconsistent_depth_at_a_join_reports_CIL1013()
    {
        ILReport report = Single(il =>
        {
            LabelHandle join = il.DefineLabel();
            il.LoadArgument(0);
            il.Branch(ILOpCode.Brtrue_s, join);
            il.LoadConstantI4(1);
            il.MarkLabel(join);
            il.OpCode(ILOpCode.Ret);
        }, parameters: 1);

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InconsistentStack && d.Message.Contains("on one path", StringComparison.Ordinal));
    }

    [Fact]
    public void Exceeding_the_declared_maximum_stack_reports_CIL1013()
    {
        // Small bodies use the tiny header format, which implies a maximum of eight.
        ILReport report = Single(il =>
        {
            for (int index = 0; index < 9; index++)
            {
                il.LoadConstantI4(index);
            }
            for (int index = 0; index < 9; index++)
            {
                il.OpCode(ILOpCode.Pop);
            }
            il.OpCode(ILOpCode.Ret);
        });

        Assert.Equal("The stack reaches 9 values, exceeding the declared maximum of 8.", Assert.Single(report.Diagnostics).Message);
    }

    [Fact]
    public void Returning_with_extra_values_reports_CIL1013()
    {
        ILReport report = Single(il =>
        {
            il.LoadConstantI4(1);
            il.OpCode(ILOpCode.Ret);
        });

        Assert.Equal("ret leaves 1 extra values on the stack.", Assert.Single(report.Diagnostics).Message);
    }

    [Fact]
    public void Falling_off_the_end_reports_CIL1011()
    {
        ILReport report = Single(il => il.OpCode(ILOpCode.Nop));

        Assert.Equal("Control falls through the end of the method body.", Assert.Single(report.Diagnostics).Message);
    }

    [Fact]
    public void Ret_inside_a_protected_block_reports_CIL1011()
    {
        ILReport report = Single(il =>
        {
            LabelHandle tryStart = il.DefineLabel(), tryEnd = il.DefineLabel(), handlerEnd = il.DefineLabel();
            il.MarkLabel(tryStart);
            il.OpCode(ILOpCode.Ret);
            il.MarkLabel(tryEnd);
            il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(handlerEnd);
            il.ControlFlowBuilder!.AddFinallyRegion(tryStart, tryEnd, tryEnd, handlerEnd);
        });

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InvalidControlTransfer && d.Message.StartsWith("ret inside", StringComparison.Ordinal));
    }

    [Fact]
    public void Branching_out_of_a_protected_block_reports_CIL1011()
    {
        ILReport report = Single(il =>
        {
            LabelHandle tryStart = il.DefineLabel(), tryEnd = il.DefineLabel(), handlerEnd = il.DefineLabel(), after = il.DefineLabel();
            il.MarkLabel(tryStart);
            il.Branch(ILOpCode.Br_s, after);
            il.MarkLabel(tryEnd);
            il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(handlerEnd);
            il.MarkLabel(after);
            il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddFinallyRegion(tryStart, tryEnd, tryEnd, handlerEnd);
        });

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InvalidControlTransfer && d.Message.Contains("without leave", StringComparison.Ordinal));
    }

    [Fact]
    public void Leaving_a_finally_handler_reports_CIL1011()
    {
        ILReport report = Single(il =>
        {
            LabelHandle tryStart = il.DefineLabel(), tryEnd = il.DefineLabel(), handlerEnd = il.DefineLabel(), after = il.DefineLabel();
            il.MarkLabel(tryStart);
            il.Branch(ILOpCode.Leave_s, after);
            il.MarkLabel(tryEnd);
            il.Branch(ILOpCode.Leave_s, after);
            il.MarkLabel(handlerEnd);
            il.MarkLabel(after);
            il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddFinallyRegion(tryStart, tryEnd, tryEnd, handlerEnd);
        });

        Assert.Contains(report.Diagnostics, d => d.Message.Contains("exits a finally or fault handler", StringComparison.Ordinal));
    }

    [Fact]
    public void Overlapping_regions_report_CIL1012()
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle a = il.DefineLabel(), b = il.DefineLabel(), c = il.DefineLabel(), d = il.DefineLabel(), e = il.DefineLabel(), end = il.DefineLabel();
            il.MarkLabel(a); il.OpCode(ILOpCode.Nop);
            il.MarkLabel(b); il.OpCode(ILOpCode.Nop);
            il.MarkLabel(c); il.Branch(ILOpCode.Leave_s, end);
            il.MarkLabel(d); il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(e); il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(end); il.OpCode(ILOpCode.Ret);
            // [a, c) and [b, d) overlap without either containing the other.
            il.ControlFlowBuilder!.AddFinallyRegion(a, c, d, e);
            il.ControlFlowBuilder!.AddFinallyRegion(b, d, e, end);
        });

        ILReport report = MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InvalidExceptionRegion && d.Message.Contains("overlaps", StringComparison.Ordinal));
    }

    [Fact]
    public void Filter_reading_an_argument_is_general()
    {
        // An argument can be changed by a finally block that runs before a one-pass evaluation.
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle tryStart = il.DefineLabel(), filterStart = il.DefineLabel(), handlerStart = il.DefineLabel(), handlerEnd = il.DefineLabel();
            il.MarkLabel(tryStart);
            il.Branch(ILOpCode.Leave_s, handlerEnd);
            il.MarkLabel(filterStart);
            il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0);
            il.OpCode(ILOpCode.Endfilter);
            il.MarkLabel(handlerStart);
            il.OpCode(ILOpCode.Pop);
            il.Branch(ILOpCode.Leave_s, handlerEnd);
            il.MarkLabel(handlerEnd);
            il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddFilterRegion(tryStart, filterStart, handlerStart, handlerEnd, filterStart);
        }, parameters: 1);

        ILReport report = MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");

        Assert.Empty(report.Diagnostics);
        Assert.Equal("general", Assert.Single(Assert.Single(report.Methods).Regions).FilterKind);
    }

    private static ILReport Single(Action<InstructionEncoder> emit, int parameters = 0)
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", emit, parameters);
        return MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");
    }
}
