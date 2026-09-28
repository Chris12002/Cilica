using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ExceptionValidationTests
{
    [Theory]
    [InlineData("self", "general")]
    [InlineData("cycle", "general")]
    [InlineData("backward-acyclic", "type-test")]
    [InlineData("forward", "type-test")]
    public void Filter_classification_checks_cycles_not_just_branch_direction(string shape, string expected)
    {
        ILReport report = Filter(il =>
        {
            il.OpCode(ILOpCode.Pop);
            LabelHandle first = il.DefineLabel(), second = il.DefineLabel(), result = il.DefineLabel();
            if (shape == "backward-acyclic")
            {
                il.Branch(ILOpCode.Br_s, second);
                il.MarkLabel(first);
                il.LoadConstantI4(1);
                il.OpCode(ILOpCode.Endfilter);
                il.MarkLabel(second);
                il.Branch(ILOpCode.Br_s, first);
            }
            else
            {
                il.MarkLabel(first);
                il.LoadConstantI4(1);
                il.Branch(ILOpCode.Brtrue_s, shape == "self" ? first : second);
                il.Branch(ILOpCode.Br_s, result);
                il.MarkLabel(second);
                il.Branch(ILOpCode.Br_s, shape == "cycle" ? first : result);
                il.MarkLabel(result);
                il.LoadConstantI4(0);
                il.OpCode(ILOpCode.Endfilter);
            }
        });

        Assert.Empty(report.Diagnostics);
        Assert.Equal(expected, Assert.Single(Assert.Single(report.Methods).Regions).FilterKind);
    }

    [Fact]
    public void Rethrow_outside_a_handler_is_rejected()
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il => il.OpCode(ILOpCode.Rethrow));

        AssertRethrowError(MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll"));
    }

    [Fact]
    public void Rethrow_in_a_filter_is_rejected()
    {
        ILReport report = Filter(il =>
        {
            il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Rethrow);
        });

        AssertRethrowError(report);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rethrow_in_catch_and_filter_handlers_is_valid(bool filtered)
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle start = il.DefineLabel(), filter = il.DefineLabel(), handler = il.DefineLabel(), end = il.DefineLabel();
            il.MarkLabel(start);
            il.OpCode(ILOpCode.Ldnull);
            il.OpCode(ILOpCode.Throw);
            il.MarkLabel(filter);
            if (filtered)
            {
                il.OpCode(ILOpCode.Pop);
                il.LoadConstantI4(1);
                il.OpCode(ILOpCode.Endfilter);
            }
            il.MarkLabel(handler);
            il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Rethrow);
            il.MarkLabel(end);
            if (filtered) il.ControlFlowBuilder!.AddFilterRegion(start, filter, handler, end, filter);
            else il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, end, assembly.Exception);
        });

        Assert.Empty(MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll").Diagnostics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rethrow_in_a_nested_try_is_valid_but_not_in_its_finally(bool inFinally)
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle start = il.DefineLabel(), handler = il.DefineLabel(), inner = il.DefineLabel(), cleanup = il.DefineLabel(), end = il.DefineLabel();
            il.MarkLabel(start);
            il.OpCode(ILOpCode.Ldnull);
            il.OpCode(ILOpCode.Throw);
            il.MarkLabel(handler);
            il.OpCode(ILOpCode.Pop);
            il.MarkLabel(inner);
            if (inFinally) il.Branch(ILOpCode.Leave_s, end);
            else il.OpCode(ILOpCode.Rethrow);
            il.MarkLabel(cleanup);
            il.OpCode(inFinally ? ILOpCode.Rethrow : ILOpCode.Endfinally);
            il.MarkLabel(end);
            il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, end, assembly.Exception);
            il.ControlFlowBuilder.AddFinallyRegion(inner, cleanup, cleanup, end);
        });
        ILReport report = MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");

        if (inFinally) AssertRethrowError(report);
        else Assert.Empty(report.Diagnostics);
    }

    private static void AssertRethrowError(ILReport report)
    {
        Diagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(DiagnosticIds.InvalidControlTransfer, diagnostic.Id);
        Assert.Equal("rethrow outside a catch handler.", diagnostic.Message);
    }

    private static ILReport Filter(Action<InstructionEncoder> emit)
    {
        var assembly = new SyntheticAssembly();
        assembly.AddMethod("Method", il =>
        {
            LabelHandle start = il.DefineLabel(), filter = il.DefineLabel(), handler = il.DefineLabel(), end = il.DefineLabel();
            il.MarkLabel(start);
            il.OpCode(ILOpCode.Ldnull);
            il.OpCode(ILOpCode.Throw);
            il.MarkLabel(filter);
            emit(il);
            il.MarkLabel(handler);
            il.OpCode(ILOpCode.Pop);
            il.Branch(ILOpCode.Leave_s, end);
            il.MarkLabel(end);
            il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddFilterRegion(start, filter, handler, end, filter);
        });
        return MethodBodyInspector.Inspect(assembly.Build(), "synthetic.dll");
    }
}
