using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cilica.Cli;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeFaultExecutionTests(ITestOutputHelper output)
{
    private const int Sentinel = -123456789;
    private sealed record Case(Type Owner, string Name, Func<int, int, int, int> Reference, bool BoundedCount = false, bool Unsigned = false,
        bool Pure = false, Func<int, int, int, string>? FaultOrigin = null, Func<int, int, int, int>? FaultOffset = null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authored_arithmetic_preserves_results_fault_order_origin_and_context_reset(bool optimized)
    {
        var cases = new Case[]
        {
            new(typeof(IntegerFaults), nameof(IntegerFaults.Divide), IntegerFaults.Divide),
            new(typeof(IntegerFaults), nameof(IntegerFaults.UnsignedDivide), IntegerFaults.UnsignedDivide, Unsigned: true),
            new(typeof(IntegerFaults), nameof(IntegerFaults.TransitiveDivide), IntegerFaults.TransitiveDivide),
            new(typeof(IntegerFaults), nameof(IntegerFaults.FirstFault), IntegerFaults.FirstFault),
            new(typeof(IntegerFaults), nameof(IntegerFaults.ReverseFirstFault), IntegerFaults.ReverseFirstFault),
            new(typeof(IntegerFaults), nameof(IntegerFaults.LoopDivide), IntegerFaults.LoopDivide, BoundedCount: true),
            new(typeof(IntegerFaults), nameof(IntegerFaults.ConditionalDivide), IntegerFaults.ConditionalDivide),
            new(typeof(IntegerFaults), nameof(IntegerFaults.RecursiveDivide), IntegerFaults.RecursiveDivide, BoundedCount: true),
            new(typeof(NativeFaultProbe), nameof(NativeFaultProbe.CrossAssembly), NativeFaultProbe.CrossAssembly),
            new(typeof(NativeFaultProbe), nameof(NativeFaultProbe.Pure), NativeFaultProbe.Pure, Pure: true),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Add), IntegerChecked.Add, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Add)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Subtract), IntegerChecked.Subtract, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Subtract)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Multiply), IntegerChecked.Multiply, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Multiply)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.AddUnsigned), IntegerChecked.AddUnsigned, FaultOrigin: (_, _, _) => nameof(IntegerChecked.AddUnsigned)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.SubtractUnsigned), IntegerChecked.SubtractUnsigned, FaultOrigin: (_, _, _) => nameof(IntegerChecked.SubtractUnsigned)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.MultiplyUnsigned), IntegerChecked.MultiplyUnsigned, FaultOrigin: (_, _, _) => nameof(IntegerChecked.MultiplyUnsigned)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Transitive), IntegerChecked.Transitive, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Add)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Loop), IntegerChecked.Loop, BoundedCount: true, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Add)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Recursive), IntegerChecked.Recursive, BoundedCount: true, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Add)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Conditional), IntegerChecked.Conditional, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Add)),
            new(typeof(NativeFaultProbe), nameof(NativeFaultProbe.CheckedCrossAssembly), NativeFaultProbe.CheckedCrossAssembly, FaultOrigin: (_, _, _) => nameof(IntegerChecked.Add)),
            new(typeof(IntegerChecked), nameof(IntegerChecked.ArithmeticBeforeDivision), IntegerChecked.ArithmeticBeforeDivision,
                FaultOrigin: (a, b, c) => FirstFaultOrigin(() => IntegerChecked.Add(a, b, c), nameof(IntegerChecked.Add), nameof(IntegerFaults.Divide))),
            new(typeof(IntegerChecked), nameof(IntegerChecked.DivisionBeforeArithmetic), IntegerChecked.DivisionBeforeArithmetic,
                FaultOrigin: (a, b, c) => FirstFaultOrigin(() => IntegerFaults.Divide(a, c, b), nameof(IntegerFaults.Divide), nameof(IntegerChecked.Add))),
            new(typeof(IntegerChecked), nameof(IntegerChecked.Compound), IntegerChecked.Compound,
                FaultOrigin: (_, _, _) => nameof(IntegerChecked.Compound),
                FaultOffset: (a, b, c) => FirstFaultOffset(() => IntegerChecked.Subtract(a, b, c))),
            new(typeof(IntegerChecked), nameof(IntegerChecked.CompoundUnsigned), IntegerChecked.CompoundUnsigned,
                FaultOrigin: (_, _, _) => nameof(IntegerChecked.CompoundUnsigned),
                FaultOffset: (a, b, c) => FirstFaultOffset(() => IntegerChecked.AddUnsigned(a, b, c))),
        };
        using var files = new ResolverFiles();
        string references = Path.GetDirectoryName(typeof(IntegerFaults).Assembly.Location)!;
        using var resolver = AssemblyResolver.Create([references]).Value!;
        string directory = files.DirectoryPath("fault-native");
        var source = new StringBuilder(); var artifacts = new List<CppArtifact>();
        foreach (var probe in cases)
        {
            string assemblyPath = probe.Owner.Assembly.Location;
            int token = probe.Owner.GetMethod(probe.Name)!.MetadataToken;
            var input = resolver.OpenInput(assemblyPath).Value!;
            var translated = Int32Translator.TranslateProgram(resolver, input, (MethodDefinitionHandle)MetadataTokens.EntityHandle(token));
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var artifact = CppEmitter.EmitProgram(translated.Program!); artifacts.Add(artifact);
            Assert.Equal(!probe.Pure, artifact.MayThrow);
            Assert.Equal(probe.Pure, artifact.EntryPoint != artifact.FaultEntryPoint);
            using var stdout = new StringWriter(); using var stderr = new StringWriter();
            int exit = CilicaCommand.Run(["emit-cpp", assemblyPath, "--root", token.ToString("x8", CultureInfo.InvariantCulture), "--reference", references], stdout, stderr);
            Assert.Equal(CilicaCommand.Success, exit); Assert.Empty(stderr.ToString()); Assert.Equal(artifact.Text, stdout.ToString());
            source.Append(artifact.Text).Append('\n');
        }
        source.Append("#include <iostream>\nint main() {\n");
        // Every missing output pointer is rejected before execution or any write.
        string entry = artifacts[0].FaultEntryPoint;
        for (int pointer = 0; pointer < 4; pointer++)
        {
            var pointers = new[] { "&value", "&method", "&il", "&image" }; pointers[pointer] = "nullptr";
            source.Append("  { std::int32_t value = 7; std::uint32_t method = 8, il = 9; const char* image = nullptr;\n")
                .Append("    if (").Append(entry).Append('(').Append(string.Join(", ", pointers))
                .Append(", 1, 0, 0) != 3u || value != 7 || method != 8u || il != 9u || image != nullptr) return 4; }\n");
        }
        source.Append("  int operation; std::int32_t a, b, c;\n  while (std::cin >> operation >> a >> b >> c) {\n")
            .Append("    std::int32_t value = -123456789; std::uint32_t method = 0xffffffffu, il = 0xffffffffu; const char* image = \"stale\"; std::uint32_t status;\n")
            .Append("    switch (operation) {\n");
        for (int index = 0; index < artifacts.Count; index++)
            source.Append(CultureInfo.InvariantCulture, $"      case {index}: status = {artifacts[index].FaultEntryPoint}(&value, &method, &il, &image, a, b, c); break;\n");
        source.Append("      default: return 2;\n    }\n")
            .Append("    std::cout << status << ' ' << value << ' ' << method << ' ' << il << ' ' << (image == nullptr ? \"-\" : image) << '\\n';\n")
            .Append("  }\n  return std::cin.eof() ? 0 : 3;\n}\n");
        string cpp = Path.Combine(directory, "faults.cpp"), executable = Path.Combine(directory, "faults");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var flags = new List<string> { "-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0" };
        if (optimized) flags.AddRange(["-fsanitize=undefined", "-fno-sanitize-recover=all"]);
        flags.AddRange([cpp, "-o", executable]);
        var version = await NativeProcess.Run(compiler, ["--version"], directory);
        Assert.Equal(0, version.ExitCode); output.WriteLine(version.Stdout);
        output.WriteLine(compiler + " " + string.Join(" ", flags));
        output.WriteLine(".NET " + Environment.Version + "; input seed=0x2e2026");
        var build = await NativeProcess.Run(compiler, flags, directory);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);

        int[] edges = [int.MinValue, int.MinValue + 1, -65536, -1, 0, 1, 65536, 0x40000000, int.MaxValue - 1, int.MaxValue];
        var vectors = new List<(int A, int B, int C)>
        {
            // Competing faults have opposite kinds; success follows a fault in the same process.
            (int.MinValue, 0, -1), (int.MinValue, -1, 0), (12, 3, 2),
            (0, int.MinValue, 0), (1, int.MinValue, -1), (1, 42, 0), (0, 42, 0),
            // Adjacent signed/unsigned product limits and conflicting fault origins.
            (46340, 46340, 1), (46341, 46341, 1), (-46341, 46341, 1),
            (65535, 65535, 1), (65536, 65536, 1),
            (int.MaxValue, 1, 0), (int.MinValue, -1, 0),
            (int.MinValue, -1, -1), (int.MaxValue, 1, -1), (-1, 0, 1),
        };
        foreach (int a in edges) foreach (int b in edges) vectors.Add((a, b, unchecked(a - b)));
        var random = new Random(0x2e2026);
        for (int index = 0; index < 256; index++) vectors.Add((Next(), Next(), Next()));
        int Next() => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(IntegerFaults).Assembly.Location))).ToLowerInvariant();
        var originTokens = new[] { typeof(IntegerFaults), typeof(IntegerChecked) }
            .SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            .ToDictionary(method => method.Name, method => method.MetadataToken, StringComparer.Ordinal);
        // Batches keep captured output below the existing 262144-character bound.
        foreach (var batch in vectors.Chunk(50))
        {
            var input = new StringBuilder(); var expected = new List<string>();
            foreach (var (a, b, c) in batch)
                for (int operation = 0; operation < cases.Length; operation++)
                {
                    Case probe = cases[operation]; int first = probe.BoundedCount ? a % 33 : a;
                    input.Append(CultureInfo.InvariantCulture, $"{operation} {first} {b} {c}\n");
                    int value = Sentinel; Int32Fault fault = Int32Fault.None;
                    // Only independently authored fixture code executes in the reference process.
                    try { value = probe.Reference(first, b, c); }
                    catch (DivideByZeroException) { fault = Int32Fault.DivideByZero; }
                    catch (OverflowException) { fault = Int32Fault.Overflow; }
                    string origin = probe.FaultOrigin is not null && fault != Int32Fault.None ? probe.FaultOrigin(first, b, c)
                        : probe.Unsigned ? nameof(IntegerFaults.UnsignedDivide) : nameof(IntegerFaults.Divide);
                    int offset = probe.FaultOffset is not null && fault != Int32Fault.None ? probe.FaultOffset(first, b, c) : 2;
                    expected.Add(fault == Int32Fault.None
                        ? FormattableString.Invariant($"0 {value} 0 0 -")
                        : FormattableString.Invariant($"{(int)fault} {Sentinel} {originTokens[origin]} {offset} {hash}"));
                }
            var run = await NativeProcess.Run(executable, [], directory, input.ToString());
            Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
            Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        }
        output.WriteLine($"{cases.Length} entries; {vectors.Count * cases.Length} native results; four null-output checks per process.");
    }

    private static string FirstFaultOrigin(Func<int> first, string firstOrigin, string secondOrigin)
    {
        // Observe an independently authored first operation, not translated IR.
        // This distinguishes even two Overflow faults at different methods.
        try { _ = first(); }
        catch (DivideByZeroException) { return firstOrigin; }
        catch (OverflowException) { return firstOrigin; }
        return secondOrigin;
    }

    private static int FirstFaultOffset(Func<int> first)
    {
        // The authored compounds have arithmetic at IL_0002 and IL_0004.
        // The reference evaluates only the first operation to identify the fault.
        try { _ = first(); }
        catch (OverflowException) { return 2; }
        return 4;
    }
}

internal static class NativeFaultProbe
{
    public static int CrossAssembly(int a, int b, int c) => IntegerFaults.TransitiveDivide(a, b, c);
    public static int CheckedCrossAssembly(int a, int b, int c) => IntegerChecked.Transitive(a, b, c);
    public static int Pure(int a, int b, int c) => unchecked(a + b + c);
}
