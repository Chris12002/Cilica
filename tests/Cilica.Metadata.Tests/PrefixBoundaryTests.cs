using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class PrefixBoundaryTests
{
    [Theory]
    [InlineData("branch", 0)]
    [InlineData("branch", 1)]
    [InlineData("branch", 2)]
    [InlineData("conditional", 0)]
    [InlineData("conditional", 1)]
    [InlineData("conditional", 2)]
    [InlineData("switch", 0)]
    [InlineData("switch", 1)]
    [InlineData("switch", 2)]
    public void Only_the_first_prefix_is_a_valid_branch_target(string transfer, int targetPart)
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle target = il.DefineLabel();
            il.LoadArgumentAddress(0);
            if (transfer == "conditional")
            {
                il.LoadConstantI4(1);
                il.Branch(ILOpCode.Brtrue_s, target);
            }
            else if (transfer == "switch")
            {
                il.LoadConstantI4(0);
                il.Switch(1).Branch(target);
            }
            else il.Branch(ILOpCode.Br_s, target);
            if (targetPart == 0) il.MarkLabel(target);
            il.OpCode(ILOpCode.Unaligned);
            il.CodeBuilder.WriteByte(1);
            if (targetPart == 1) il.MarkLabel(target);
            il.OpCode(ILOpCode.Volatile);
            if (targetPart == 2) il.MarkLabel(target);
            il.OpCode(ILOpCode.Ldind_i4);
            il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ret);
        }, parameters: 1);
        ILReport report = MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");

        if (targetPart == 0) Assert.Empty(report.Diagnostics);
        else Assert.Equal(DiagnosticIds.InvalidControlTransfer, Assert.Single(report.Diagnostics).Id);
    }

    [Theory]
    [InlineData("try-start")]
    [InlineData("try-end")]
    [InlineData("handler-start")]
    [InlineData("handler-end")]
    [InlineData("none")]
    public void Exception_boundaries_cannot_split_a_prefix_sequence(string boundary)
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle start = il.DefineLabel(), middle = il.DefineLabel(), handler = il.DefineLabel(), handlerMiddle = il.DefineLabel(), end = il.DefineLabel();
            il.MarkLabel(start);
            il.LoadArgumentAddress(0);
            il.OpCode(ILOpCode.Volatile);
            il.MarkLabel(middle);
            il.OpCode(ILOpCode.Ldind_i4);
            il.OpCode(ILOpCode.Pop);
            il.Branch(ILOpCode.Leave_s, end);
            il.MarkLabel(handler);
            il.LoadArgumentAddress(0);
            il.OpCode(ILOpCode.Volatile);
            il.MarkLabel(handlerMiddle);
            il.OpCode(ILOpCode.Ldind_i4);
            il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(end);
            il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddFinallyRegion(
                boundary == "try-start" ? middle : start,
                boundary == "try-end" ? middle : handler,
                boundary == "handler-start" ? handlerMiddle : handler,
                boundary == "handler-end" ? handlerMiddle : end);
        }, parameters: 1);

        ILReport report = MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");

        if (boundary == "none") Assert.Empty(report.Diagnostics);
        else Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InvalidExceptionRegion
            && d.Message.Contains("instruction boundaries", StringComparison.Ordinal));
    }
}
