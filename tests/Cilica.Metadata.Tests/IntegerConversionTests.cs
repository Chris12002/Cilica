using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class IntegerConversionTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Exact_authored_CIL_conversion_variants_match_dotnet(bool sourceWide, bool optimized)
    {
        // Only these test-authored instructions execute in the oracle; the
        // compiler continues to consume metadata without executing input images.
        ILOpCode[] codes = [ILOpCode.Conv_i1, ILOpCode.Conv_u1, ILOpCode.Conv_i2, ILOpCode.Conv_u2,
            ILOpCode.Conv_i4, ILOpCode.Conv_u4, ILOpCode.Conv_i8, ILOpCode.Conv_u8,
            ILOpCode.Conv_ovf_i1, ILOpCode.Conv_ovf_u1, ILOpCode.Conv_ovf_i2, ILOpCode.Conv_ovf_u2,
            ILOpCode.Conv_ovf_i4, ILOpCode.Conv_ovf_u4, ILOpCode.Conv_ovf_i8, ILOpCode.Conv_ovf_u8,
            ILOpCode.Conv_ovf_i1_un, ILOpCode.Conv_ovf_u1_un, ILOpCode.Conv_ovf_i2_un, ILOpCode.Conv_ovf_u2_un,
            ILOpCode.Conv_ovf_i4_un, ILOpCode.Conv_ovf_u4_un, ILOpCode.Conv_ovf_i8_un, ILOpCode.Conv_ovf_u8_un];
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("CastInstructions", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var roots = new List<MethodDefinitionHandle>(); var oracles = new List<Func<ulong, ulong, ulong>>();
        foreach (var code in codes)
        {
            bool targetWide = code.ToString().Contains("i8", StringComparison.Ordinal) || code.ToString().Contains("u8", StringComparison.Ordinal);
            roots.Add(b.Method(code.ToString(), [0, 1, targetWide ? (byte)0x0a : (byte)8, 0x0a], emit: il =>
            { il.LoadArgument(0); if (!sourceWide) il.OpCode(ILOpCode.Conv_i4); il.OpCode(code); il.OpCode(ILOpCode.Ret); }));
            var oracle = new System.Reflection.Emit.DynamicMethod(code.ToString(), typeof(ulong), [typeof(ulong)]);
            var emit = oracle.GetILGenerator(); emit.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            if (!sourceWide) emit.Emit(System.Reflection.Emit.OpCodes.Conv_I4);
            var opcode = (System.Reflection.Emit.OpCode)typeof(System.Reflection.Emit.OpCodes).GetFields()
                .Single(f => string.Equals(f.Name, code.ToString(), StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
            emit.Emit(opcode); if (!targetWide) emit.Emit(System.Reflection.Emit.OpCodes.Conv_U8); emit.Emit(System.Reflection.Emit.OpCodes.Ret);
            var run = oracle.CreateDelegate<Func<ulong, ulong>>(); oracles.Add((a, _) => run(a));
        }
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var programs = roots.Select(root => { var result = IntegerTranslator.TranslateProgram(resolver, input, root);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!; }).ToArray();
        await NativeInteger64Tests.Verify(programs, oracles, optimized, BoundaryValues().Select(a => (a, 0UL)).ToArray());
    }

    [Theory]
    [InlineData("target")] [InlineData("result")] [InlineData("operand")]
    [InlineData("missing-fault")] [InlineData("extra-fault")] [InlineData("legacy")]
    public void Malformed_cast_IR_is_rejected(string mutation)
    {
        var cast = new IrExpression.IntegerCast(IntegerCastTarget.Int8, mutation == "operand" ? 1 : 0, mutation != "extra-fault", false);
        if (mutation == "target") cast = cast with { Target = (IntegerCastTarget)999 };
        var method = new IrMethod(IntegerIrTests.Source(), [IrType.Int32], IrType.Int32,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
                new IrStatement.Define(new(1, mutation == "result" ? IrType.Int64 : IrType.Int32, 1, cast),
                    mutation == "missing-fault" ? null : IrExceptionTarget.MethodExit)], new IrReturn(2, 1))], []);
        Assert.Throws<ArgumentException>(() => mutation == "legacy" ? CppEmitter.EmitProgram(IntegerIrTests.Program(method).Flow)
            : CppEmitter.EmitIntegers(IntegerIrTests.Program(method)));
    }

    [Theory]
    [InlineData("int", false)] [InlineData("int", true)]
    [InlineData("uint", false)] [InlineData("uint", true)]
    [InlineData("long", false)] [InlineData("long", true)]
    [InlineData("ulong", false)] [InlineData("ulong", true)]
    public async Task Checked_and_wrapping_conversions_match_dotnet_at_boundaries(string source, bool optimized)
    {
        (string Name, string Source, Func<ulong, ulong> Oracle)[] probes =
        [
            (nameof(IntegerConversions.int_sbyte_wrapping), "int", value => unchecked((uint)IntegerConversions.int_sbyte_wrapping((int)value))),
            (nameof(IntegerConversions.int_byte_wrapping), "int", value => unchecked((uint)IntegerConversions.int_byte_wrapping((int)value))),
            (nameof(IntegerConversions.int_short_wrapping), "int", value => unchecked((uint)IntegerConversions.int_short_wrapping((int)value))),
            (nameof(IntegerConversions.int_ushort_wrapping), "int", value => unchecked((uint)IntegerConversions.int_ushort_wrapping((int)value))),
            (nameof(IntegerConversions.int_int_wrapping), "int", value => unchecked((uint)IntegerConversions.int_int_wrapping((int)value))),
            (nameof(IntegerConversions.int_uint_wrapping), "int", value => unchecked((uint)IntegerConversions.int_uint_wrapping((int)value))),
            (nameof(IntegerConversions.int_long_wrapping), "int", value => unchecked((ulong)IntegerConversions.int_long_wrapping((int)value))),
            (nameof(IntegerConversions.int_ulong_wrapping), "int", value => unchecked((ulong)IntegerConversions.int_ulong_wrapping((int)value))),
            (nameof(IntegerConversions.int_sbyte_checked), "int", value => unchecked((uint)IntegerConversions.int_sbyte_checked((int)value))),
            (nameof(IntegerConversions.int_byte_checked), "int", value => unchecked((uint)IntegerConversions.int_byte_checked((int)value))),
            (nameof(IntegerConversions.int_short_checked), "int", value => unchecked((uint)IntegerConversions.int_short_checked((int)value))),
            (nameof(IntegerConversions.int_ushort_checked), "int", value => unchecked((uint)IntegerConversions.int_ushort_checked((int)value))),
            (nameof(IntegerConversions.int_int_checked), "int", value => unchecked((uint)IntegerConversions.int_int_checked((int)value))),
            (nameof(IntegerConversions.int_uint_checked), "int", value => unchecked((uint)IntegerConversions.int_uint_checked((int)value))),
            (nameof(IntegerConversions.int_long_checked), "int", value => unchecked((ulong)IntegerConversions.int_long_checked((int)value))),
            (nameof(IntegerConversions.int_ulong_checked), "int", value => unchecked((ulong)IntegerConversions.int_ulong_checked((int)value))),
            (nameof(IntegerConversions.uint_sbyte_wrapping), "uint", value => unchecked((uint)IntegerConversions.uint_sbyte_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_byte_wrapping), "uint", value => unchecked((uint)IntegerConversions.uint_byte_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_short_wrapping), "uint", value => unchecked((uint)IntegerConversions.uint_short_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_ushort_wrapping), "uint", value => unchecked((uint)IntegerConversions.uint_ushort_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_int_wrapping), "uint", value => unchecked((uint)IntegerConversions.uint_int_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_uint_wrapping), "uint", value => unchecked((uint)IntegerConversions.uint_uint_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_long_wrapping), "uint", value => unchecked((ulong)IntegerConversions.uint_long_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_ulong_wrapping), "uint", value => unchecked((ulong)IntegerConversions.uint_ulong_wrapping((uint)value))),
            (nameof(IntegerConversions.uint_sbyte_checked), "uint", value => unchecked((uint)IntegerConversions.uint_sbyte_checked((uint)value))),
            (nameof(IntegerConversions.uint_byte_checked), "uint", value => unchecked((uint)IntegerConversions.uint_byte_checked((uint)value))),
            (nameof(IntegerConversions.uint_short_checked), "uint", value => unchecked((uint)IntegerConversions.uint_short_checked((uint)value))),
            (nameof(IntegerConversions.uint_ushort_checked), "uint", value => unchecked((uint)IntegerConversions.uint_ushort_checked((uint)value))),
            (nameof(IntegerConversions.uint_int_checked), "uint", value => unchecked((uint)IntegerConversions.uint_int_checked((uint)value))),
            (nameof(IntegerConversions.uint_uint_checked), "uint", value => unchecked((uint)IntegerConversions.uint_uint_checked((uint)value))),
            (nameof(IntegerConversions.uint_long_checked), "uint", value => unchecked((ulong)IntegerConversions.uint_long_checked((uint)value))),
            (nameof(IntegerConversions.uint_ulong_checked), "uint", value => unchecked((ulong)IntegerConversions.uint_ulong_checked((uint)value))),
            (nameof(IntegerConversions.long_sbyte_wrapping), "long", value => unchecked((uint)IntegerConversions.long_sbyte_wrapping((long)value))),
            (nameof(IntegerConversions.long_byte_wrapping), "long", value => unchecked((uint)IntegerConversions.long_byte_wrapping((long)value))),
            (nameof(IntegerConversions.long_short_wrapping), "long", value => unchecked((uint)IntegerConversions.long_short_wrapping((long)value))),
            (nameof(IntegerConversions.long_ushort_wrapping), "long", value => unchecked((uint)IntegerConversions.long_ushort_wrapping((long)value))),
            (nameof(IntegerConversions.long_int_wrapping), "long", value => unchecked((uint)IntegerConversions.long_int_wrapping((long)value))),
            (nameof(IntegerConversions.long_uint_wrapping), "long", value => unchecked((uint)IntegerConversions.long_uint_wrapping((long)value))),
            (nameof(IntegerConversions.long_long_wrapping), "long", value => unchecked((ulong)IntegerConversions.long_long_wrapping((long)value))),
            (nameof(IntegerConversions.long_ulong_wrapping), "long", value => unchecked((ulong)IntegerConversions.long_ulong_wrapping((long)value))),
            (nameof(IntegerConversions.long_sbyte_checked), "long", value => unchecked((uint)IntegerConversions.long_sbyte_checked((long)value))),
            (nameof(IntegerConversions.long_byte_checked), "long", value => unchecked((uint)IntegerConversions.long_byte_checked((long)value))),
            (nameof(IntegerConversions.long_short_checked), "long", value => unchecked((uint)IntegerConversions.long_short_checked((long)value))),
            (nameof(IntegerConversions.long_ushort_checked), "long", value => unchecked((uint)IntegerConversions.long_ushort_checked((long)value))),
            (nameof(IntegerConversions.long_int_checked), "long", value => unchecked((uint)IntegerConversions.long_int_checked((long)value))),
            (nameof(IntegerConversions.long_uint_checked), "long", value => unchecked((uint)IntegerConversions.long_uint_checked((long)value))),
            (nameof(IntegerConversions.long_long_checked), "long", value => unchecked((ulong)IntegerConversions.long_long_checked((long)value))),
            (nameof(IntegerConversions.long_ulong_checked), "long", value => unchecked((ulong)IntegerConversions.long_ulong_checked((long)value))),
            (nameof(IntegerConversions.ulong_sbyte_wrapping), "ulong", value => unchecked((uint)IntegerConversions.ulong_sbyte_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_byte_wrapping), "ulong", value => unchecked((uint)IntegerConversions.ulong_byte_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_short_wrapping), "ulong", value => unchecked((uint)IntegerConversions.ulong_short_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_ushort_wrapping), "ulong", value => unchecked((uint)IntegerConversions.ulong_ushort_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_int_wrapping), "ulong", value => unchecked((uint)IntegerConversions.ulong_int_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_uint_wrapping), "ulong", value => unchecked((uint)IntegerConversions.ulong_uint_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_long_wrapping), "ulong", value => unchecked((ulong)IntegerConversions.ulong_long_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_ulong_wrapping), "ulong", value => unchecked((ulong)IntegerConversions.ulong_ulong_wrapping((ulong)value))),
            (nameof(IntegerConversions.ulong_sbyte_checked), "ulong", value => unchecked((uint)IntegerConversions.ulong_sbyte_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_byte_checked), "ulong", value => unchecked((uint)IntegerConversions.ulong_byte_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_short_checked), "ulong", value => unchecked((uint)IntegerConversions.ulong_short_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_ushort_checked), "ulong", value => unchecked((uint)IntegerConversions.ulong_ushort_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_int_checked), "ulong", value => unchecked((uint)IntegerConversions.ulong_int_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_uint_checked), "ulong", value => unchecked((uint)IntegerConversions.ulong_uint_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_long_checked), "ulong", value => unchecked((ulong)IntegerConversions.ulong_long_checked((ulong)value))),
            (nameof(IntegerConversions.ulong_ulong_checked), "ulong", value => unchecked((ulong)IntegerConversions.ulong_ulong_checked((ulong)value))),
        ];
        var selected = probes.Where(p => p.Source == source).ToArray();
        var programs = new List<IntegerIrProgram>();
        using var resolver = AssemblyResolver.Create([]).Value!;
        var input = resolver.OpenInput(typeof(IntegerConversions).Assembly.Location).Value!;
        foreach (var probe in selected)
        {
            var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(IntegerConversions).GetMethod(probe.Name)!.MetadataToken);
            var result = IntegerTranslator.TranslateProgram(resolver, input, root);
            Assert.True(result.Success, probe.Name + ": " + string.Join("; ", result.Diagnostics)); programs.Add(result.Program!);
        }
        await NativeInteger64Tests.Verify(programs, selected.Select(p => new Func<ulong, ulong, ulong>((a, _) => p.Oracle(a))).ToArray(), optimized,
            BoundaryValues().Select(a => (a, 0UL)).ToArray());
    }

    private static ulong[] BoundaryValues()
    {
        // Each narrow/wide signed and unsigned boundary, including both sides.
        var values = new HashSet<ulong> { 0, 1, ulong.MaxValue };
        foreach (int bits in new[] { 7, 8, 15, 16, 31, 32, 63 })
        {
            ulong boundary = 1UL << bits;
            foreach (long delta in new long[] { -1, 0, 1 })
            {
                values.Add(unchecked(boundary + (ulong)delta));
                values.Add(unchecked(0UL - boundary + (ulong)delta));
            }
        }
        return values.Order().ToArray();
    }
}
