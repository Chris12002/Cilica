using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Cli;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CppEmissionTests
{
    private static IrMethod Method() => new(new("ignored", new string('a', 64), 0x06000001, "ignored"),
        [IrType.Int32, IrType.Int32], IrType.Int32,
        [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
            new IrStatement.Define(new(1, IrType.Int32, 1, new IrExpression.Parameter(1))),
            new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Binary(Int32Operation.SubtractWrapping, 0, 1)))], new IrReturn(3, 2))], []);

    [Fact]
    public void Source_is_deterministic_culture_independent_and_never_interpolates_metadata_names()
    {
        var method = Method();
        CppArtifact expected = CppEmitter.Emit(method);
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var hostile = method with { Source = method.Source with { Name = "*/\n#error injected", AssemblyIdentity = "#include \"bad\"" } };
            Assert.Equal(expected.Text, CppEmitter.Emit(hostile).Text);
        }
        finally { CultureInfo.CurrentCulture = saved; }
        Assert.Equal("cilica_m_" + new string('a', 64) + "_06000001", expected.EntryPoint);
        Assert.Contains("// IL_0002", expected.Text, StringComparison.Ordinal);
        Assert.Contains("static_cast<std::uint64_t>(v0) - static_cast<std::uint64_t>(v1)", expected.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', expected.Text);
        Assert.Equal(method.Source, expected.Source);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("hash")]
    [InlineData("token")]
    [InlineData("signature")]
    [InlineData("parameters")]
    [InlineData("blocks")]
    [InlineData("block_id")]
    [InlineData("values")]
    [InlineData("empty")]
    [InlineData("null_value")]
    [InlineData("value_id")]
    [InlineData("type")]
    [InlineData("offset")]
    [InlineData("argument")]
    [InlineData("negative_argument")]
    [InlineData("offset_order")]
    [InlineData("forward_operand")]
    [InlineData("negative_operand")]
    [InlineData("operation")]
    [InlineData("expression")]
    [InlineData("return")]
    [InlineData("return_offset")]
    [InlineData("return_value")]
    public void Forged_ir_fails_before_any_artifact_is_returned(string damage)
    {
        IrMethod method = Method(); IrBlock block = method.Blocks[0];
        IrValue value = block.Values[2];
        method = damage switch
        {
            "source" => method with { Source = null! },
            "hash" => method with { Source = method.Source with { ContentHash = new string('z', 64) } },
            "token" => method with { Source = method.Source with { Token = 0x06000000 } },
            "signature" => method with { ReturnType = (IrType)99 },
            "parameters" => method with { Parameters = default },
            "blocks" => method with { Blocks = default },
            _ => method with { Blocks = [damage switch
            {
                "block_id" => block with { Id = 1 },
                "values" => block with { Statements = default },
                "empty" => block with { Statements = [] },
                "return" => block with { Terminator = null! },
                "return_offset" => block with { Terminator = ((IrReturn)block.Terminator) with { ILOffset = 1 } },
                "return_value" => block with { Terminator = ((IrReturn)block.Terminator) with { Value = 3 } },
                _ => block with { Statements = block.Statements.SetItem(2, new IrStatement.Define(damage switch
                {
                    "null_value" => null!,
                    "value_id" => value with { Id = 1 },
                    "type" => value with { Type = (IrType)99 },
                    "offset" => value with { ILOffset = -1 },
                    "argument" => value with { Expression = new IrExpression.Parameter(2) },
                    "negative_argument" => value with { Expression = new IrExpression.Parameter(-1) },
                    "offset_order" => value with { ILOffset = 0 },
                    "forward_operand" => value with { Expression = new IrExpression.Binary(Int32Operation.AddWrapping, 0, 2) },
                    "negative_operand" => value with { Expression = new IrExpression.Binary(Int32Operation.AddWrapping, -1, 1) },
                    "operation" => value with { Expression = new IrExpression.Binary((Int32Operation)99, 0, 1) },
                    _ => value with { Expression = null! },
                })) },
            }] },
        };
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method));
    }

    [Fact]
    public void Emitter_bounds_apply_to_caller_constructed_ir()
    {
        IrMethod method = Method();
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method with
        { Parameters = Enumerable.Repeat(IrType.Int32, CppEmitter.MaximumParameters + 1).ToImmutableArray() }));
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method with
        { Blocks = [method.Blocks[0] with { Statements = Enumerable.Repeat(method.Blocks[0].Statements[0], CppEmitter.MaximumValues + 1).ToImmutableArray() }] }));
        Assert.Throws<ArgumentNullException>(() => CppEmitter.Emit(null!));
    }

    [Theory]
    [InlineData("emit-cpp")]
    [InlineData("emit-cpp", "a.dll")]
    [InlineData("emit-cpp", "a.dll", "--root")]
    [InlineData("emit-cpp", "a.dll", "--root", "02000001")]
    [InlineData("emit-cpp", "a.dll", "--root", "06000000")]
    [InlineData("emit-cpp", "a.dll", "--root", "06000001", "--root", "06000002")]
    [InlineData("emit-cpp", "a.dll", "--root", "06000001", "--format", "json")]
    public void Emission_requires_one_explicit_method_root(params string[] args)
    {
        var result = Run(args); Assert.Equal(CilicaCommand.UsageError, result.Exit); Assert.Empty(result.Out);
    }

    [Fact]
    public void Cli_emits_only_cpp_and_preserves_existing_output_on_translation_or_resolution_failure()
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(new("Emit", new Version(1, 0, 0, 0)));
        builder.Type("", "Entry");
        builder.Method("Add", [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret); });
        builder.Method("Unsupported", [0, 0, 8], emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Add_ovf); il.LoadConstantI4(2); il.OpCode(ILOpCode.Ret); });
        string input = files.Write("input.dll", builder.Build());
        string output = Path.Combine(files.Root, "generated.cpp");
        var stdout = Run("emit-cpp", input, "--root", "06000001");
        Assert.Equal(CilicaCommand.Success, stdout.Exit); Assert.Empty(stdout.Err);
        Assert.StartsWith("// Cilica int32-control-flow-v1", stdout.Out, StringComparison.Ordinal);
        Assert.Equal(CilicaCommand.Success, Run("emit-cpp", input, "--root", "06000001", "--output", output).Exit);
        Assert.Equal(stdout.Out, File.ReadAllText(output));
        foreach (string root in new[] { "06000002", "06ffffff" })
        {
            var failure = Run("emit-cpp", input, "--root", root, "--output", output);
            Assert.Equal(CilicaCommand.ErrorsReported, failure.Exit); Assert.Empty(failure.Out); Assert.Contains("CIL", failure.Err, StringComparison.Ordinal);
            Assert.Equal(stdout.Out, File.ReadAllText(output));
        }
        var missing = Run("emit-cpp", input + ".missing", "--root", "06000001", "--output", output);
        Assert.Equal(CilicaCommand.ErrorsReported, missing.Exit); Assert.Empty(missing.Out); Assert.Equal(stdout.Out, File.ReadAllText(output));
        var invalidReference = Run("emit-cpp", input, "--root", "06000001", "--reference", input, "--output", output);
        Assert.Equal(CilicaCommand.ErrorsReported, invalidReference.Exit); Assert.Empty(invalidReference.Out); Assert.Equal(stdout.Out, File.ReadAllText(output));
        Assert.Equal(CilicaCommand.ErrorsReported, Run("emit-cpp", input, "--root", "06000001", "--output", files.Root).Exit);
    }

    private static (int Exit, string Out, string Err) Run(params string[] args)
    {
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        int exit = CilicaCommand.Run(args, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }
}
