using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Cli;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeExecutionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authored_csharp_compiles_through_cilica_and_matches_dotnet(bool optimized)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        string directory = files.DirectoryPath("native");
        string inputAssembly = typeof(NativeIntegerProbe).Assembly.Location;
        var assembly = resolver.OpenInput(inputAssembly).Value!;
        string[] names = [nameof(NativeIntegerProbe.Add), nameof(NativeIntegerProbe.Subtract), nameof(NativeIntegerProbe.Multiply),
            nameof(NativeIntegerProbe.Calculate), nameof(NativeIntegerProbe.Minimum), nameof(NativeIntegerProbe.Maximum),
            nameof(NativeIntegerProbe.MinusOne), nameof(NativeIntegerProbe.Ignore)];
        var source = new StringBuilder(); var artifacts = new List<CppArtifact>();
        foreach (string name in names)
        {
            int token = typeof(NativeIntegerProbe).GetMethod(name)!.MetadataToken;
            var translated = Int32Translator.Translate(resolver, assembly, (MethodDefinitionHandle)MetadataTokens.EntityHandle(token));
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            CppArtifact artifact = CppEmitter.Emit(translated.Method!); artifacts.Add(artifact);
            // Include the CLI in the complete source-generation path as well.
            using var stdout = new StringWriter(); using var stderr = new StringWriter();
            int exit = CilicaCommand.Run(["emit-cpp", inputAssembly, "--root", token.ToString("x8", CultureInfo.InvariantCulture)], stdout, stderr);
            Assert.Equal(CilicaCommand.Success, exit); Assert.Empty(stderr.ToString()); Assert.Equal(artifact.Text, stdout.ToString());
            source.Append(stdout.ToString()).Append('\n');
        }
        source.Append("#include <iostream>\n#include <string>\n#include <chrono>\n#include <thread>\n");
        source.Append("int main(int argc, char** argv) {\n")
            .Append("  if (argc == 2 && std::string(argv[1]) == \"hang\") { std::this_thread::sleep_for(std::chrono::minutes(1)); return 0; }\n")
            .Append("  if (argc == 2 && std::string(argv[1]) == \"flood\") { for (;;) std::cout << std::string(4096, 'x'); }\n")
            .Append("  if (argc == 2 && std::string(argv[1]) == \"burst\") { std::cout << std::string(524288, 'x'); return 0; }\n")
            .Append("  int operation; std::int32_t a, b, c;\n  while (std::cin >> operation >> a >> b >> c) {\n    switch (operation) {\n");
        for (int index = 0; index < artifacts.Count; index++)
        {
            string arguments = index switch { <= 2 => "a, b", 3 => "a, b, c", 7 => "a", _ => "" };
            source.Append(CultureInfo.InvariantCulture, $"      case {index}: std::cout << {artifacts[index].EntryPoint}({arguments}) << '\\n'; break;\n");
        }
        source.Append("      default: return 2;\n    }\n  }\n  return std::cin.eof() ? 0 : 3;\n}\n");
        string cpp = Path.Combine(directory, "authored.cpp"), executable = Path.Combine(directory, "authored");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var flags = new List<string> { "-std=c++20", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0" };
        if (optimized) flags.AddRange(["-fsanitize=undefined", "-fno-sanitize-recover=all"]);
        flags.AddRange([cpp, "-o", executable]);
        var version = await NativeProcess.Run(compiler, ["--version"], directory);
        Assert.Equal(0, version.ExitCode);
        output.WriteLine(version.Stdout);
        output.WriteLine(compiler + " " + string.Join(" ", flags));
        output.WriteLine(".NET " + Environment.Version + "; input seed=0x2b2026");
        var build = await NativeProcess.Run(compiler, flags, directory);
        Assert.True(build.ExitCode == 0, version.Stdout + "\n" + string.Join(" ", flags) + "\n" + build.Stdout + build.Stderr);

        int[] edges = [int.MinValue, int.MinValue + 1, -65536, -1, 0, 1, 65536, 0x40000000, int.MaxValue - 1, int.MaxValue];
        var vectors = new List<(int A, int B, int C)>();
        foreach (int a in edges) foreach (int b in edges) vectors.Add((a, b, unchecked(a - b)));
        var random = new Random(0x2b2026);
        for (int index = 0; index < 256; index++) vectors.Add((Next(), Next(), Next()));
        int Next() => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        var input = new StringBuilder(); var expected = new List<string>();
        foreach (var (a, b, c) in vectors)
            for (int operation = 0; operation < artifacts.Count; operation++)
            {
                input.Append(CultureInfo.InvariantCulture, $"{operation} {a} {b} {c}\n");
                int result = operation switch
                {
                    0 => NativeIntegerProbe.Add(a, b), 1 => NativeIntegerProbe.Subtract(a, b),
                    2 => NativeIntegerProbe.Multiply(a, b), 3 => NativeIntegerProbe.Calculate(a, b, c),
                    4 => NativeIntegerProbe.Minimum(), 5 => NativeIntegerProbe.Maximum(),
                    6 => NativeIntegerProbe.MinusOne(), _ => NativeIntegerProbe.Ignore(a),
                };
                expected.Add(result.ToString(CultureInfo.InvariantCulture));
            }
        var run = await NativeProcess.Run(executable, [], directory, input.ToString());
        Assert.True(run.ExitCode == 0, version.Stdout + "\n" + run.Stderr);
        Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        if (!optimized)
        {
            await Assert.ThrowsAsync<TimeoutException>(() => NativeProcess.Run(executable, ["hang"], directory, timeout: TimeSpan.FromMilliseconds(200)));
            var flood = await Assert.ThrowsAsync<InvalidOperationException>(() => NativeProcess.Run(executable, ["flood"], directory));
            Assert.Contains("captured-output bound", flood.Message, StringComparison.Ordinal);
            // A finite producer must still fail if exit and overflow complete together.
            await Assert.ThrowsAsync<InvalidOperationException>(() => NativeProcess.Run(executable, ["burst"], directory));
        }
    }
}

// Authored reference code runs only in this test process. Compiler inputs remain data.
internal static class NativeIntegerProbe
{
    public static int Add(int a, int b) => unchecked(a + b);
    public static int Subtract(int a, int b) => unchecked(a - b);
    public static int Multiply(int a, int b) => unchecked(a * b);
    public static int Calculate(int a, int b, int c) => unchecked((a - b) * c + a);
    public static int Minimum() => int.MinValue;
    public static int Maximum() => int.MaxValue;
    public static int MinusOne() => -1;
    public static int Ignore(int unused) => 0;
}
