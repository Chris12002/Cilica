using System;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class Int64OperandInputTests
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    [InlineData(-4294967296L)]
    [InlineData(4294967296L)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    public void Wide_constants_survive_detached_snapshots_without_narrowing(long value)
    {
        var builder = new ResolutionAssemblyBuilder(new("Wide", new Version(1, 0, 0, 0)));
        builder.Type("Example", "Probe");
        var root = builder.Method("Read", [0, 0, 8], emit: il =>
        { il.LoadConstantI8(value); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(42); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        MethodInput input;
        using (var resolver = files.Open())
        {
            var assembly = resolver.OpenInput(files.Write("wide.dll", builder.Build())).Value!;
            var result = resolver.ReadMethodInput(assembly, root);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); input = result.Value!;
            // Capturing the operand must not silently extend the Int32 execution profile.
            Assert.False(Int32Translator.TranslateProgram(resolver, assembly, root).Success);
            var shortRead = resolver.ReadMethodInput(assembly, root, new(MaximumBodyBytes: 8));
            Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(shortRead.Diagnostics).Id);
        }
        var wide = input.Instructions[0]; Assert.Equal("ldc.i8", wide.OpCode);
        Assert.Equal(value, wide.Int64Operand); Assert.Null(wide.IntegerOperand);
        Assert.Equal(9, wide.Length); Assert.Equal(0, wide.Offset);
        Assert.All(input.Instructions.Skip(1), instruction => Assert.Null(instruction.Int64Operand));
        Assert.Equal(42, input.Instructions[2].IntegerOperand);
    }
}
