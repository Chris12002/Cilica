using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class Int32TranslationTests
{
    private static ResolutionAssemblyBuilder Builder() => new(new("Integer", new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static TranslationResult Translate(ResolutionAssemblyBuilder builder, MethodDefinitionHandle root, MethodReadLimits? limits = null)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        return Int32Translator.Translate(resolver, resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, root, limits);
    }

    [Fact]
    public void Translation_preserves_order_operand_direction_types_and_provenance_after_disposal()
    {
        var builder = Builder(); builder.Type("Example", "Entry");
        var root = builder.Method("Calculate", [0, 2, 8, 8, 8], emit: il =>
        {
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Sub);
            il.LoadConstantI4(3); il.OpCode(ILOpCode.Mul); il.LoadConstantI4(2); il.OpCode(ILOpCode.Add); Return(il);
        });
        var result = Translate(builder, root);
        Assert.True(result.Success); Assert.Empty(result.Diagnostics);
        var method = result.Method!; Assert.Equal(0x06000001, method.Source.Token); Assert.Equal("Example.Entry::Calculate", method.Source.Name);
        Assert.Equal(64, method.Source.ContentHash.Length); Assert.StartsWith("Integer,", method.Source.AssemblyIdentity, StringComparison.Ordinal);
        Assert.Equal(new[] { IrType.Int32, IrType.Int32 }, method.Parameters);
        var block = Assert.Single(method.Blocks); Assert.Equal(7, block.Values.Length);
        Assert.Equal(new IrExpression.Binary(Int32Operation.SubtractWrapping, 0, 1), block.Values[2].Expression);
        Assert.Equal(new IrExpression.Binary(Int32Operation.MultiplyWrapping, 2, 3), block.Values[4].Expression);
        Assert.Equal(new IrExpression.Binary(Int32Operation.AddWrapping, 4, 5), block.Values[6].Expression);
        Assert.Equal(6, ((IrReturn)block.Terminator).Value);
        Assert.Equal(2, block.Values[2].ILOffset);
        Assert.All(block.Values, value => Assert.Equal(IrType.Int32, value.Type));
        Assert.Equal(23, EvaluateAuthoredFixture(method, [10, 3]));
        Assert.Contains("Calculate", JsonSerializer.Serialize(method), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-128)]
    [InlineData(127)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Constant_encodings_preserve_the_int32_bit_pattern(int constant)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Value", [0, 0, 8], emit: il => { il.LoadConstantI4(constant); Return(il); });
        var result = Translate(builder, root); Assert.True(result.Success);
        Assert.Equal(new IrExpression.Constant(constant), Assert.Single(Assert.Single(result.Method!.Blocks).Values).Expression);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(255)]
    [InlineData(256)]
    public void Argument_encodings_resolve_to_parameter_indices(int index)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var signature = new BlobBuilder(); new BlobEncoder(signature).MethodSignature().Parameters(index + 1,
            r => r.Type().Int32(), parameters => { for (int i = 0; i <= index; i++) parameters.AddParameter().Type().Int32(); });
        var root = builder.Method("Value", signature.ToArray(), emit: il => { il.LoadArgument(index); Return(il); });
        var result = Translate(builder, root); Assert.True(result.Success);
        Assert.Equal(new IrExpression.Parameter(index), Assert.Single(Assert.Single(result.Method!.Blocks).Values).Expression);
    }

    [Theory]
    [InlineData(ILOpCode.Ldnull)]
    [InlineData(ILOpCode.Neg)]
    [InlineData(ILOpCode.Conv_i8)]
    public void Unsupported_instructions_never_return_partial_ir(ILOpCode opcode)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Bad", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(opcode); Return(il); });
        var result = Translate(builder, root); Assert.False(result.Success); Assert.Null(result.Method);
        var diagnostic = Assert.Single(result.Diagnostics); Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
        Assert.Contains("IL_0001", diagnostic.Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("underflow")]
    [InlineData("empty_return")]
    [InlineData("residual_return")]
    [InlineData("fallthrough")]
    [InlineData("trailing")]
    [InlineData("bad_argument")]
    public void Invalid_stack_operand_and_return_shapes_keep_locations(string kind)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Bad", [0, 0, 8], emit: il =>
        {
            switch (kind)
            {
                case "underflow": il.OpCode(ILOpCode.Add); Return(il); break;
                case "empty_return": Return(il); break;
                case "residual_return": il.LoadConstantI4(1); il.LoadConstantI4(2); Return(il); break;
                case "fallthrough": il.LoadConstantI4(1); break;
                case "trailing": il.LoadConstantI4(1); Return(il); il.OpCode(ILOpCode.Nop); break;
                default: il.LoadArgument(0); Return(il); break;
            }
        });
        var result = Translate(builder, root); Assert.Null(result.Method);
        Assert.Contains("IL_", Assert.Single(result.Diagnostics).Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Non_int32_signatures_and_instance_methods_require_a_later_profile(bool instance)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Bad", instance ? [0x20, 0, 8] : [0, 1, 8, 0x0a],
            attributes: instance ? MethodAttributes.Public : MethodAttributes.Public | MethodAttributes.Static,
            emit: il => { il.LoadConstantI4(1); Return(il); });
        Assert.Equal(DiagnosticIds.ExecutionSignatureUnsupported, Assert.Single(Translate(builder, root).Diagnostics).Id);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("type_initializer")]
    [InlineData("module_initializer")]
    [InlineData("runtime")]
    [InlineData("generic")]
    public void Runtime_obligations_and_open_methods_are_not_silently_ignored(string kind)
    {
        var builder = Builder();
        if (kind == "module_initializer") builder.Method(".cctor", [0, 0, 1], emit: Return);
        builder.Type("", "Entry");
        if (kind == "type_initializer") builder.Method(".cctor", [0, 0, 1], emit: Return);
        var root = builder.Method("Value", kind == "generic" ? [0x10, 1, 0, 8] : [0, 0, 8],
            arity: kind == "generic" ? 1 : 0,
            emit: kind == "runtime" ? null : il => { il.LoadConstantI4(1); Return(il); });
        if (kind == "reference") builder.MarkReferenceAssembly();
        var result = Translate(builder, root); Assert.Null(result.Method);
        Assert.Contains(Assert.Single(result.Diagnostics).Id, new[] { DiagnosticIds.ExecutionUnsupported, DiagnosticIds.ExecutionSignatureUnsupported });
    }

    [Fact]
    public void Method_reads_are_bounded_and_session_owned()
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Value", [0, 0, 8], emit: il => { il.LoadConstantI4(1000); Return(il); });
        using var files = new ResolverFiles(); using var resolver = files.Open(); using var other = files.Open();
        var assembly = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(Int32Translator.Translate(resolver, assembly, root, new(MaximumBodyBytes: 1)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(Int32Translator.Translate(resolver, assembly, root, new(MaximumInstructions: 1)).Diagnostics).Id);
        Assert.True(Int32Translator.Translate(resolver, assembly, root, new(MaximumInstructions: 2)).Success);
        Assert.Throws<ArgumentException>(() => Int32Translator.Translate(other, assembly, root));
        Assert.Throws<ArgumentOutOfRangeException>(() => Int32Translator.Translate(resolver, assembly, root, new(MaximumInstructions: 0)));
        resolver.Dispose(); Assert.Throws<ObjectDisposedException>(() => Int32Translator.Translate(resolver, assembly, root));
    }

    [Fact]
    public void Declared_stack_maximum_is_enforced_before_ir_is_accepted()
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Bad", [0, 0, 8], emit: il =>
        {
            for (int index = 0; index < 9; index++) il.LoadConstantI4(1);
            for (int index = 0; index < 8; index++) il.OpCode(ILOpCode.Add);
            Return(il);
        });
        var result = Translate(builder, root); Assert.Null(result.Method);
        Assert.Equal(DiagnosticIds.InconsistentStack, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_and_truncated_opcode_operands_return_metadata_diagnostics(bool truncated)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Bad", [0, 0, 8], emit: il => il.CodeBuilder.WriteByte(truncated ? (byte)0x20 : (byte)0xff));
        var result = Translate(builder, root); Assert.Null(result.Method);
        Assert.Equal(DiagnosticIds.MalformedMethodBody, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Exception_regions_and_initializer_scan_limits_remain_visible()
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Bad", [0, 0, 8], emit: il =>
        {
            var start = il.DefineLabel(); var handler = il.DefineLabel(); var end = il.DefineLabel();
            il.MarkLabel(start); il.Branch(ILOpCode.Leave_s, end);
            il.MarkLabel(handler); il.OpCode(ILOpCode.Endfinally);
            il.MarkLabel(end); il.LoadConstantI4(1); Return(il);
            il.ControlFlowBuilder!.AddFinallyRegion(start, handler, handler, end);
        });
        builder.Method("Other", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(Int32Translator.Translate(resolver, assembly, root).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(Int32Translator.Translate(resolver, assembly, root, new(MaximumInitializerMembers: 1)).Diagnostics).Id);
    }

    [Fact]
    public void A_damaged_body_header_is_not_misreported_as_a_signature_error()
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Value", [0, 0, 8], emit: il => { il.LoadConstantI4(1); Return(il); });
        byte[] bytes = builder.Build();
        using (var pe = new PEReader(ImmutableArray.Create(bytes)))
        {
            int rva = pe.GetMetadataReader().GetMethodDefinition(root).RelativeVirtualAddress;
            var section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.VirtualSize);
            bytes[rva - section.VirtualAddress + section.PointerToRawData] = 0;
        }
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var result = Int32Translator.Translate(resolver, resolver.OpenInput(files.Write("bad.dll", bytes)).Value!, root);
        Assert.Null(result.Method); Assert.Equal(DiagnosticIds.MalformedMethodBody, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(int.MaxValue, 1, 1)]
    [InlineData(int.MinValue, -1, 0)]
    [InlineData(int.MinValue, 1, -1)]
    [InlineData(0x40000000, 4, 3)]
    [InlineData(17, -9, 23)]
    public void Authored_compiler_output_agrees_with_dotnet_wraparound(int left, int right, int offset)
    {
        int expected = IntegerProbe.Calculate(left, right, offset);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(typeof(IntegerProbe).Assembly.Location).Value!;
        var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(IntegerProbe).GetMethod(nameof(IntegerProbe.Calculate))!.MetadataToken);
        var result = Int32Translator.Translate(resolver, assembly, root);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(expected, EvaluateAuthoredFixture(result.Method!, [left, right, offset]));
    }

    // Test-only evaluator, used only for independently authored fixtures. This is
    // neither the compiler's execution path nor evidence of native generation.
    private static int EvaluateAuthoredFixture(IrMethod method, int[] arguments)
    {
        var block = Assert.Single(method.Blocks); var values = new Dictionary<int, int>();
        foreach (IrValue value in block.Values)
            values[value.Id] = value.Expression switch
            {
                IrExpression.Constant constant => constant.Value,
                IrExpression.Parameter parameter => arguments[parameter.Index],
                IrExpression.Binary binary => binary.Operation switch
                {
                    Int32Operation.AddWrapping => unchecked(values[binary.Left] + values[binary.Right]),
                    Int32Operation.SubtractWrapping => unchecked(values[binary.Left] - values[binary.Right]),
                    Int32Operation.MultiplyWrapping => unchecked(values[binary.Left] * values[binary.Right]),
                    _ => throw new InvalidOperationException(),
                },
                _ => throw new InvalidOperationException(),
            };
        return values[((IrReturn)block.Terminator).Value];
    }
}

internal static class IntegerProbe
{
    public static int Calculate(int left, int right, int offset) => unchecked(left * right + offset);
}
