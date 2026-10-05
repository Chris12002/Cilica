using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeInteger64Tests
{
    private sealed record Probe(IrMethod Method, Func<ulong, ulong, ulong> Oracle, IrMethod[]? Helpers = null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Int64_bit_patterns_faults_conversions_and_comparisons_match_dotnet(bool optimized)
    {
        var probes = new List<Probe>();
        foreach (var op in Enum.GetValues<Int64Operation>()) probes.Add(new(IntegerIrTests.Binary(op), (a, b) => Calculate(op, a, b)));
        probes.Add(new(IntegerIrTests.Conversion(IntegerConversion.SignExtend32, IrType.Int32, IrType.Int64), (a, _) => unchecked((ulong)(long)(int)a)));
        probes.Add(new(IntegerIrTests.Conversion(IntegerConversion.ZeroExtend32, IrType.Int32, IrType.Int64), (a, _) => (uint)a));
        probes.Add(new(IntegerIrTests.Conversion(IntegerConversion.Truncate64, IrType.Int64, IrType.Int32), (a, _) => (uint)a));
        foreach (var comparison in Enum.GetValues<Int32Comparison>().Where(c => c is not (Int32Comparison.Zero or Int32Comparison.Nonzero)))
            probes.Add(new(IntegerIrTests.Compare(comparison), (a, b) => Compare(comparison, a, b) ? 1UL : 0UL));
        probes.Add(new(Branches(), (a, b) => unchecked((long)a) < unchecked((long)b) ? ~a : b ^ 0x8000000000000000));
        var helper = IntegerIrTests.Binary(Int64Operation.AddCheckedSigned);
        var caller = new IrMethod(IntegerIrTests.Source(0x06000302), [IrType.Int64, IrType.Int64], IrType.Int64,
            [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int64, 0, new IrExpression.Parameter(0))),
                new IrStatement.Define(new(1, IrType.Int64, 1, new IrExpression.Parameter(1))),
                new IrStatement.Define(new(2, IrType.Int64, 2, new IrExpression.Call(IrMethodId.From(helper.Source), [0, 1])), IrExceptionTarget.MethodExit)], new IrReturn(3, 2))], []);
        probes.Add(new(caller, (a, b) => Calculate(Int64Operation.AddCheckedSigned, a, b), [helper]));
        var programs = probes.Select(p => new IntegerIrProgram(new(IrMethodId.From(p.Method.Source), [p.Method, .. p.Helpers ?? Array.Empty<IrMethod>()]))).ToArray();
        await Verify(programs, probes.Select(p => p.Oracle).ToArray(), optimized);
    }

    internal static async Task Verify(IReadOnlyList<IntegerIrProgram> programs, IReadOnlyList<Func<ulong, ulong, ulong>> oracles, bool optimized,
        IReadOnlyList<(ulong a, ulong b)>? vectors = null)
    {
        Assert.Equal(programs.Count, oracles.Count);
        var methods = programs.Select(p => p.Flow.Methods.Single(m => IrMethodId.From(m.Source) == p.Flow.Entry)).ToArray();
        var source = new StringBuilder(); var artifacts = programs.Select(CppEmitter.EmitIntegers).ToArray();
        foreach (var artifact in artifacts) source.Append(artifact.Text);
        source.Append("\n#include <iostream>\nint main() { unsigned op; std::uint64_t a, b; while (std::cin >> op >> a >> b) { switch(op) {\n");
        for (int i = 0; i < programs.Count; ++i)
        {
            var method = methods[i]; bool wide = method.ReturnType == IrType.Int64;
            string signed = wide ? "std::int64_t" : "std::int32_t", unsigned = wide ? "std::uint64_t" : "std::uint32_t";
            string Argument(int index) => method.Parameters[index] == IrType.Int64 ? "std::bit_cast<std::int64_t>(" + (index == 0 ? "a" : "b") + ")"
                : "std::bit_cast<std::int32_t>(static_cast<std::uint32_t>(" + (index == 0 ? "a" : "b") + "))";
            source.Append(CultureInfo.InvariantCulture, $"case {i}: {{ {signed} value = 12345; std::uint32_t token = 0, il = 0; const char* image = nullptr; auto status = {artifacts[i].FaultEntryPoint}(&value, &token, &il, &image, ")
                .Append(string.Join(", ", Enumerable.Range(0, method.Parameters.Length).Select(Argument)))
                .Append("); std::cout << status << ' ' << std::bit_cast<").Append(unsigned).Append(">(value) << '\\n'; break; }\n");
        }
        source.Append("default: return 2; } } return 0; }\n");
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("wide"), cpp = Path.Combine(dir, "wide.cpp"), exe = Path.Combine(dir, "wide");
        File.WriteAllText(cpp, source.ToString());
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-fsanitize=undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], dir, timeout: TimeSpan.FromSeconds(60));
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        ulong[] edges = [0, 1, 2, 31, 32, 63, 64, 65, 0x7fffffff, 0x80000000, 0xffffffff, 0x100000000, 0x7fffffffffffffff, 0x8000000000000000, 0x8000000000000001, ulong.MaxValue];
        var pairs = edges.SelectMany(a => edges.Select(b => (a, b))).ToList();
        var random = new Random(20261005);
        for (int i = 0; i < 32; ++i) pairs.Add((unchecked((ulong)random.NextInt64()) ^ 0x8000000000000000, unchecked((ulong)random.NextInt64())));
        if (vectors is not null) pairs = vectors.ToList();
        var input = new StringBuilder(); var expected = new List<string>();
        for (int op = 0; op < programs.Count; ++op) foreach (var (a, b) in pairs)
        {
            input.Append(CultureInfo.InvariantCulture, $"{op} {a} {b}\n");
            uint status = 0; ulong result = 12345;
            try { result = oracles[op](a, b); }
            catch (DivideByZeroException) { status = 1; }
            catch (OverflowException) { status = 2; }
            expected.Add(status.ToString(CultureInfo.InvariantCulture) + " " + result.ToString(CultureInfo.InvariantCulture));
        }
        var run = await NativeProcess.Run(exe, [], dir, input.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        var actual = run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected.Count, actual.Length);
        for (int i = 0; i < actual.Length; ++i) Assert.True(expected[i] == actual[i], "Probe/vector " + i.ToString(CultureInfo.InvariantCulture) + ": expected " + expected[i] + "; actual " + actual[i]);
    }

    private static IrMethod Branches() => new(IntegerIrTests.Source(0x06000301), [IrType.Int64, IrType.Int64], IrType.Int64,
        [new(0, 0, [], [new IrStatement.Define(new(0, IrType.Int64, 0, new IrExpression.Parameter(0))),
            new IrStatement.Define(new(1, IrType.Int64, 1, new IrExpression.Parameter(1)))], new IrBranch(2, Int32Comparison.LessSigned, 0, 1, new(1, [0]), new(2, [1]))),
         new(1, 3, [IrType.Int64], [new IrStatement.Define(new(0, IrType.Int64, 3, new IrExpression.StackInput(0))),
             new IrStatement.Define(new(1, IrType.Int64, 4, new IrExpression.Not64(0))), new IrStatement.StoreLocal(5, 0, 1),
             new IrStatement.Define(new(2, IrType.Int64, 6, new IrExpression.Local(0)))], new IrReturn(7, 2)),
         new(2, 8, [IrType.Int64], [new IrStatement.Define(new(0, IrType.Int64, 8, new IrExpression.StackInput(0))),
             new IrStatement.Define(new(1, IrType.Int64, 9, new IrExpression.Constant64(long.MinValue))),
             new IrStatement.Define(new(2, IrType.Int64, 10, new IrExpression.Binary64(Int64Operation.BitwiseXor, 0, 1)))], new IrReturn(11, 2))], [IrType.Int64]);

    private static ulong Calculate(Int64Operation op, ulong left, ulong right)
    {
        long a = unchecked((long)left), b = unchecked((long)right);
        return op switch
        {
            Int64Operation.AddWrapping => unchecked(left + right), Int64Operation.SubtractWrapping => unchecked(left - right),
            Int64Operation.MultiplyWrapping => unchecked(left * right), Int64Operation.BitwiseAnd => left & right,
            Int64Operation.BitwiseOr => left | right, Int64Operation.BitwiseXor => left ^ right,
            Int64Operation.ShiftLeft => left << unchecked((int)right), Int64Operation.ShiftRightSigned => unchecked((ulong)(a >> unchecked((int)right))),
            Int64Operation.ShiftRightUnsigned => left >> unchecked((int)right), Int64Operation.DivideSigned => unchecked((ulong)(a / b)),
            Int64Operation.DivideUnsigned => left / right, Int64Operation.RemainderSigned => unchecked((ulong)(a % b)),
            Int64Operation.RemainderUnsigned => left % right, Int64Operation.AddCheckedSigned => unchecked((ulong)checked(a + b)),
            Int64Operation.SubtractCheckedSigned => unchecked((ulong)checked(a - b)), Int64Operation.MultiplyCheckedSigned => unchecked((ulong)checked(a * b)),
            Int64Operation.AddCheckedUnsigned => checked(left + right), Int64Operation.SubtractCheckedUnsigned => checked(left - right),
            Int64Operation.MultiplyCheckedUnsigned => checked(left * right), _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    private static bool Compare(Int32Comparison op, ulong a, ulong b) => op switch
    {
        Int32Comparison.Equal => a == b, Int32Comparison.NotEqual => a != b,
        Int32Comparison.LessSigned => unchecked((long)a) < unchecked((long)b), Int32Comparison.GreaterSigned => unchecked((long)a) > unchecked((long)b),
        Int32Comparison.LessOrEqualSigned => unchecked((long)a) <= unchecked((long)b), Int32Comparison.GreaterOrEqualSigned => unchecked((long)a) >= unchecked((long)b),
        Int32Comparison.LessUnsigned => a < b, Int32Comparison.GreaterUnsigned => a > b,
        Int32Comparison.LessOrEqualUnsigned => a <= b, Int32Comparison.GreaterOrEqualUnsigned => a >= b,
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}
