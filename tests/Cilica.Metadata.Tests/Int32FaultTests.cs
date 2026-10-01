using System;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Compiler;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class Int32FaultTests
{
    private static IrMethod Translate(ILOpCode operation)
    {
        var builder = new ResolutionAssemblyBuilder(new("Fault", new Version(1, 0, 0, 0)));
        builder.Type("", "Entry");
        var root = builder.Method("Divide", [0, 2, 8, 8, 8], emit: il =>
        { il.LoadArgument(0); il.LoadArgument(1); il.OpCode(operation); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var translated = Int32Translator.Translate(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, root);
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        return translated.Method!;
    }

    [Theory]
    [InlineData(ILOpCode.Div, Int32Operation.DivideSigned)]
    [InlineData(ILOpCode.Div_un, Int32Operation.DivideUnsigned)]
    public void Division_has_typed_operands_an_explicit_failure_exit_and_a_distinct_native_entry(ILOpCode cil, Int32Operation operation)
    {
        var method = Translate(cil);
        var define = Assert.IsType<IrStatement.Define>(Assert.Single(method.Blocks).Statements[2]);
        Assert.Equal(new IrExpression.Binary(operation, 0, 1), define.Value.Expression);
        Assert.Equal(2, define.Value.ILOffset);
        Assert.Equal(IrExceptionTarget.MethodExit, define.Exceptional);
        var artifact = CppEmitter.Emit(method);
        Assert.True(artifact.MayThrow); Assert.Equal(artifact.FaultEntryPoint, artifact.EntryPoint);
        Assert.EndsWith("_try", artifact.EntryPoint, StringComparison.Ordinal);
        Assert.DoesNotContain("extern \"C\" std::int32_t", artifact.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ILOpCode.Div, 0)]
    [InlineData(ILOpCode.Div, 1)]
    [InlineData(ILOpCode.Div_un, 0)]
    [InlineData(ILOpCode.Div_un, 1)]
    public void Division_underflow_returns_no_partial_ir(ILOpCode operation, int operands)
    {
        var builder = new ResolutionAssemblyBuilder(new("Fault", new Version(1, 0, 0, 0)));
        builder.Type("", "Entry");
        var root = builder.Method("Bad", [0, 0, 8], emit: il =>
        { for (int i = 0; i < operands; i++) il.LoadConstantI4(1); il.OpCode(operation); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var result = Int32Translator.Translate(resolver, resolver.OpenInput(files.Write("bad.dll", builder.Build())).Value!, root);
        Assert.Null(result.Method);
        Assert.Equal(DiagnosticIds.InconsistentStack, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_or_unknown_failure_edges_are_rejected(bool unknown)
    {
        var method = Translate(ILOpCode.Div); var block = method.Blocks[0];
        var define = (IrStatement.Define)block.Statements[2];
        method = method with { Blocks = [block with { Statements = block.Statements.SetItem(2,
            define with { Exceptional = unknown ? (IrExceptionTarget)99 : null }) }] };
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method));
    }

    [Fact]
    public void Pure_definitions_cannot_forge_a_failure_edge()
    {
        var method = Translate(ILOpCode.Add); var block = method.Blocks[0];
        var define = (IrStatement.Define)block.Statements[2];
        Assert.False(CppEmitter.Emit(method).MayThrow);
        method = method with { Blocks = [block with { Statements = block.Statements.SetItem(2,
            define with { Exceptional = IrExceptionTarget.MethodExit }) }] };
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recursive_effect_analysis_propagates_faults_and_preserves_pure_cycles(bool fault)
    {
        // A -> B -> A, with a direct division in B only in the throwing case.
        // This authored graph tests compilation; it is never executed.
        var prototype = Translate(ILOpCode.Div);
        var a = prototype.Source with { Token = 0x06000001 };
        var b = prototype.Source with { Token = 0x06000002 };
        IrMethod Method(Cilica.Metadata.IL.MethodSource source, Cilica.Metadata.IL.MethodSource target, bool divides) =>
            new(source, [], IrType.Int32, [new(0, 0, [], [
                new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(1))),
                new IrStatement.Define(new(1, IrType.Int32, 1, new IrExpression.Call(IrMethodId.From(target), [])), IrExceptionTarget.MethodExit),
                new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Binary(
                    divides ? Int32Operation.DivideSigned : Int32Operation.AddWrapping, 0, 1)), divides ? IrExceptionTarget.MethodExit : null),
            ], new IrReturn(3, 2))], []);
        var artifact = CppEmitter.EmitProgram(new(IrMethodId.From(a), [Method(a, b, false), Method(b, a, fault)]));
        Assert.Equal(fault, artifact.MayThrow);
        Assert.Equal(fault, artifact.Text.Contains("if (context.pending != 0u) return 0;", StringComparison.Ordinal));
        Assert.Equal(!fault, artifact.Text.Contains("extern \"C\" std::int32_t", StringComparison.Ordinal));
        // Call nodes still require their explicit edge even when analysis proves them pure.
        var root = Method(a, b, false); var block = root.Blocks[0];
        root = root with { Blocks = [block with { Statements = block.Statements.SetItem(1,
            ((IrStatement.Define)block.Statements[1]) with { Exceptional = null }) }] };
        Assert.Throws<ArgumentException>(() => CppEmitter.EmitProgram(new(IrMethodId.From(a), [root, Method(b, a, fault)])));
    }
}
