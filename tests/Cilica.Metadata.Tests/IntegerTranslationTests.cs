using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class IntegerTranslationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Translated_integer_CIL_matches_authored_dotnet_methods(bool optimized)
    {
        var probes = new Dictionary<string, Func<ulong, ulong, ulong>>
        {
            [nameof(IntegerWide.Add)] = (a, b) => unchecked((ulong)IntegerWide.Add((long)a, (long)b)),
            [nameof(IntegerWide.Subtract)] = (a, b) => unchecked((ulong)IntegerWide.Subtract((long)a, (long)b)),
            [nameof(IntegerWide.Multiply)] = (a, b) => unchecked((ulong)IntegerWide.Multiply((long)a, (long)b)),
            [nameof(IntegerWide.And)] = (a, b) => unchecked((ulong)IntegerWide.And((long)a, (long)b)),
            [nameof(IntegerWide.Or)] = (a, b) => unchecked((ulong)IntegerWide.Or((long)a, (long)b)),
            [nameof(IntegerWide.Xor)] = (a, b) => unchecked((ulong)IntegerWide.Xor((long)a, (long)b)),
            [nameof(IntegerWide.Left)] = (a, b) => unchecked((ulong)IntegerWide.Left((long)a, (long)b)),
            [nameof(IntegerWide.Right)] = (a, b) => unchecked((ulong)IntegerWide.Right((long)a, (long)b)),
            [nameof(IntegerWide.UnsignedRight)] = (a, b) => IntegerWide.UnsignedRight(a, unchecked((long)b)),
            [nameof(IntegerWide.Divide)] = (a, b) => unchecked((ulong)IntegerWide.Divide((long)a, (long)b)),
            [nameof(IntegerWide.UnsignedDivide)] = IntegerWide.UnsignedDivide,
            [nameof(IntegerWide.AddChecked)] = (a, b) => unchecked((ulong)IntegerWide.AddChecked((long)a, (long)b)),
            [nameof(IntegerWide.SubtractChecked)] = (a, b) => unchecked((ulong)IntegerWide.SubtractChecked((long)a, (long)b)),
            [nameof(IntegerWide.MultiplyChecked)] = (a, b) => unchecked((ulong)IntegerWide.MultiplyChecked((long)a, (long)b)),
            [nameof(IntegerWide.AddUnsignedChecked)] = IntegerWide.AddUnsignedChecked,
            [nameof(IntegerWide.SubtractUnsignedChecked)] = IntegerWide.SubtractUnsignedChecked,
            [nameof(IntegerWide.MultiplyUnsignedChecked)] = IntegerWide.MultiplyUnsignedChecked,
            [nameof(IntegerWide.Remainder)] = (a, b) => unchecked((ulong)IntegerWide.Remainder((long)a, (long)b)),
            [nameof(IntegerWide.UnsignedRemainder)] = IntegerWide.UnsignedRemainder,
            [nameof(IntegerWide.Widen)] = (a, _) => unchecked((ulong)IntegerWide.Widen((int)a)),
            [nameof(IntegerWide.WidenUnsigned)] = (a, _) => IntegerWide.WidenUnsigned(unchecked((uint)a)),
            [nameof(IntegerWide.Narrow)] = (a, _) => unchecked((uint)IntegerWide.Narrow((long)a)),
            [nameof(IntegerWide.Complement)] = (a, _) => unchecked((ulong)IntegerWide.Complement((long)a)),
            [nameof(IntegerWide.Negate)] = (a, _) => unchecked((ulong)IntegerWide.Negate((long)a)),
            [nameof(IntegerWide.Choose)] = (a, b) => unchecked((ulong)IntegerWide.Choose((long)a, (long)b)),
            [nameof(IntegerWide.Compare)] = (a, b) => unchecked((uint)IntegerWide.Compare((long)a, (long)b)),
            [nameof(IntegerWide.CompareUnsigned)] = (a, b) => unchecked((uint)IntegerWide.CompareUnsigned(a, b)),
            [nameof(IntegerWide.Loop)] = (a, b) => unchecked((ulong)IntegerWide.Loop((long)a, (long)b)),
            [nameof(IntegerWide.Recursive)] = (a, b) => unchecked((ulong)IntegerWide.Recursive((long)a, (long)b)),
            [nameof(IntegerWide.FaultCall)] = (a, b) => unchecked((ulong)IntegerWide.FaultCall((long)a, (long)b)),
            [nameof(IntegerWide.Constant)] = (a, _) => unchecked((ulong)IntegerWide.Constant((long)a)),
        };
        Assert.Equal(typeof(IntegerWide).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Length, probes.Count);
        var programs = probes.Keys.Select(name => { var result = Translate(name); Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!; }).ToArray();
        await NativeInteger64Tests.Verify(programs, probes.Values.ToArray(), optimized);
    }

    internal static IntegerTranslationResult Translate(string name)
    {
        using var resolver = AssemblyResolver.Create([]).Value!;
        var input = resolver.OpenInput(typeof(IntegerWide).Assembly.Location).Value!;
        return IntegerTranslator.TranslateProgram(resolver, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(IntegerWide).GetMethod(name)!.MetadataToken));
    }

    [Fact]
    public void Authored_scalar_closures_translate_with_widths_preserved()
    {
        foreach (var method in typeof(IntegerWide).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
        {
            var result = Translate(method.Name); Assert.True(result.Success, method.Name + ": " + string.Join("; ", result.Diagnostics));
            Assert.NotEmpty(CppEmitter.EmitIntegers(result.Program!).Text);
        }
    }

    [Theory]
    [InlineData("add")]
    [InlineData("compare")]
    [InlineData("return")]
    [InlineData("local")]
    [InlineData("shift")]
    [InlineData("join")]
    public void Invalid_stack_widths_are_diagnostics_not_implicit_conversions(string kind)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("Widths", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var root = b.Method("Run", [0, 0, 0x0a], locals: kind == "local" ? [7, 1, 0x0a] : null, emit: il =>
        {
            if (kind == "join")
            {
                var other = il.DefineLabel(); var join = il.DefineLabel();
                il.LoadConstantI4(0); il.Branch(ILOpCode.Brtrue_s, other); il.LoadConstantI8(1); il.Branch(ILOpCode.Br_s, join);
                il.MarkLabel(other); il.LoadConstantI4(2); il.MarkLabel(join); il.OpCode(ILOpCode.Ret); return;
            }
            if (kind == "shift") { il.LoadConstantI8(1); il.LoadConstantI8(1); il.OpCode(ILOpCode.Shl); }
            else if (kind == "return") il.LoadConstantI4(1);
            else if (kind == "local") { il.LoadConstantI4(1); il.StoreLocal(0); il.LoadConstantI8(0); }
            else { il.LoadConstantI8(1); il.LoadConstantI4(1); il.OpCode(kind == "add" ? ILOpCode.Add : ILOpCode.Ceq); }
            il.OpCode(ILOpCode.Ret);
        });
        var result = IntegerTranslator.TranslateProgram(resolver, resolver.OpenInput(files.Write("input.dll", b.Build())).Value!, root);
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.InconsistentStack, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Calls_require_the_declared_parameter_width()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("CallWidths", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var helper = b.Method("Helper", [0, 1, 0x0a, 0x0a], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var root = b.Method("Run", [0, 0, 0x0a], emit: il => { il.LoadConstantI4(1); il.Call(helper); il.OpCode(ILOpCode.Ret); });
        var result = IntegerTranslator.TranslateProgram(resolver, resolver.OpenInput(files.Write("input.dll", b.Build())).Value!, root);
        Assert.Equal(DiagnosticIds.InconsistentStack, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Checked_conversions_and_non_integer_signatures_remain_explicitly_unsupported()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("UnsupportedWide", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var convert = b.Method("Convert", [0, 1, 8, 0x0a], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Conv_ovf_i4); il.OpCode(ILOpCode.Ret); });
        var boolean = b.Method("Boolean", [0, 1, 8, 2], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(IntegerTranslator.TranslateProgram(resolver, input, convert).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.ExecutionSignatureUnsupported, Assert.Single(IntegerTranslator.TranslateProgram(resolver, input, boolean).Diagnostics).Id);
        Assert.False(Int32Translator.TranslateProgram(resolver, input, convert).Success);
    }
}
