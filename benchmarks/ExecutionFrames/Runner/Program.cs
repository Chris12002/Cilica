using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;

namespace Cilica.Benchmarks.ExecutionFrames;

internal static class Program
{
    private const int Samples = 5, Warmup = 2_000;
    private static readonly (string Name, int Iterations, int Count)[] Cases =
        [(nameof(Workloads.Arithmetic), 2_000_000, 1), (nameof(Workloads.Loop), 100_000, 128), (nameof(Workloads.Calls), 30_000, 128)];

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["reference"]) { Reference(); return 0; }
            if (args is not ["run", var output])
            {
                Console.Error.WriteLine("Usage: dotnet Runner.dll run <output-directory>"); return 2;
            }
            string directory = Path.GetFullPath(output);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new InvalidOperationException("Use a new or empty output directory; existing results must not be overwritten or mistaken for this run.");
            Directory.CreateDirectory(directory);
            // Compilation and execution of authored .NET inputs occur in separate
            // processes. Emission never calls a method in the input assembly.
            await Run(directory);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }

    private static async Task Run(string directory)
    {
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string version = await ProcessOutput(compiler, ["--version"], directory);
        var metadata = new StringBuilder().AppendLine(RuntimeInformation.OSDescription)
            .AppendLine(RuntimeInformation.ProcessArchitecture.ToString()).AppendLine(RuntimeInformation.FrameworkDescription)
            .AppendLine(version).AppendLine("Native: -std=c++20 -O2 -fno-exceptions -fno-rtti; no LTO or sanitizers")
            .AppendLine(".NET reference: DOTNET_TieredCompilation=0, DOTNET_ReadyToRun=0; warmed authored methods")
            .AppendLine(CultureInfo.InvariantCulture, $"Samples={Samples}; warmup calls={Warmup}; input image={typeof(Workloads).Assembly.Location}")
            .AppendLine("Input SHA256=" + Hash(typeof(Workloads).Assembly.Location))
            .AppendLine("Compiler SHA256=" + Hash(typeof(ObjectCppEmitter).Assembly.Location))
            .AppendLine("Profile=" + ObjectCppEmitter.Profile);
        var records = new List<Record>();
        var sizes = new StringBuilder("backend\tsource_bytes\texecutable_bytes\tcompile_ms\n");
        foreach (bool objects in new[] { false, true })
        {
            string name = objects ? "object" : "primitive", cpp = Path.Combine(directory, name + ".cpp"), executable = Path.Combine(directory, name);
            Console.WriteLine("Emitting and compiling " + name + " workloads...");
            File.WriteAllText(cpp, Emit(objects), new UTF8Encoding(false));
            var clock = Stopwatch.StartNew();
            await ProcessOutput(compiler, ["-std=c++20", "-O2", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", cpp, "-o", executable], directory);
            sizes.Append(CultureInfo.InvariantCulture, $"{name}\t{new FileInfo(cpp).Length}\t{new FileInfo(executable).Length}\t{clock.Elapsed.TotalMilliseconds:F1}\n");
            Console.WriteLine("Measuring " + name + " workloads...");
            records.AddRange(Parse(await ProcessOutput(executable, [], directory)));
        }
        // Explicitly disable tier changes in the timed .NET process; warmup is
        // performed there, outside timing, and does not include JIT startup.
        Console.WriteLine("Measuring authored .NET workloads...");
        records.AddRange(Parse(await ProcessOutput("dotnet", [typeof(Program).Assembly.Location, "reference"], directory, reference: true)));
        foreach (var item in Cases)
        {
            var rows = records.Where(r => r.Name == item.Name).ToArray();
            if (rows.Length != Samples * 4 || rows.Any(r => r.Iterations != item.Iterations || !double.IsFinite(r.Nanoseconds) || r.Nanoseconds <= 0))
                throw new InvalidOperationException("Incomplete or invalid measurements for " + item.Name);
            int expected = rows.First(r => r.Backend == "dotnet").Checksum;
            if (rows.Any(r => r.Checksum != expected)) throw new InvalidOperationException("Differential checksum mismatch for " + item.Name);
            foreach (string backend in new[] { "primitive", "object-boundary", "object-warm", "dotnet" })
                if (!rows.Where(r => r.Backend == backend).Select(r => r.Sample).Order().SequenceEqual(Enumerable.Range(0, Samples)))
                    throw new InvalidOperationException("Missing or duplicate samples for " + backend);
        }
        if (records.Count != Cases.Length * Samples * 4) throw new InvalidOperationException("Unexpected result records.");
        File.WriteAllText(Path.Combine(directory, "environment.txt"), metadata.ToString());
        File.WriteAllText(Path.Combine(directory, "sizes.tsv"), sizes.ToString());
        File.WriteAllText(Path.Combine(directory, "samples.tsv"), "backend\tworkload\tsample\titerations\tns_per_call\tchecksum\n" +
            string.Concat(records.Select(r => FormattableString.Invariant($"{r.Backend}\t{r.Name}\t{r.Sample}\t{r.Iterations}\t{r.Nanoseconds:F3}\t{r.Checksum}\n"))));
        Console.WriteLine("backend\tworkload\tmedian_ns/call\tmin\tmax");
        foreach (var group in records.GroupBy(r => (r.Backend, r.Name)))
        {
            double[] values = group.Select(r => r.Nanoseconds).Order().ToArray();
            Console.WriteLine(FormattableString.Invariant($"{group.Key.Backend}\t{group.Key.Name}\t{values[Samples / 2]:F2}\t{values[0]:F2}\t{values[^1]:F2}"));
        }
        Console.WriteLine("All timed checksums match .NET. Raw samples and environment: " + directory);
    }

    private static string Emit(bool objects)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(Workloads).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var source = new StringBuilder(); var wrappers = new StringBuilder(); var run = new StringBuilder();
        foreach (var item in Cases)
        {
            var token = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(Workloads).GetMethod(item.Name)!.MetadataToken);
            CppArtifact artifact;
            if (objects)
            {
                var translated = ObjectTranslator.Translate(resolver, input, token, runtime);
                if (!translated.Success) throw new InvalidOperationException(string.Join("; ", translated.Diagnostics));
                artifact = ObjectCppEmitter.EmitProgram(translated.Program!);
            }
            else
            {
                var translated = Int32Translator.TranslateProgram(resolver, input, token);
                if (!translated.Success) throw new InvalidOperationException(string.Join("; ", translated.Diagnostics));
                artifact = CppEmitter.EmitProgram(translated.Program!);
            }
            source.Append(artifact.Text);
            wrappers.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static std::int32_t invoke_{{item.Name}}(std::int32_t seed, std::int32_t count) {
                  std::int32_t result = 0; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                """);
            if (objects) wrappers.Append("const std::int32_t args[] = {seed, count};\n");
            wrappers.Append("if (").Append(artifact.FaultEntryPoint).Append("(&result, &method, &il, &image, ")
                .Append(objects ? "args, 2" : "seed, count").Append(") != 0u) std::abort(); return result; }\n");
            string backend = objects ? "object-boundary" : "primitive";
            run.Append(CultureInfo.InvariantCulture, $"measure(\"{backend}\", \"{item.Name}\", {item.Iterations}, {item.Count}, invoke_{item.Name});\n");
            if (objects)
            {
                string scope = artifact.EntryPoint[..^4] + "_closure";
                string root = "cilica_m_" + artifact.Source.ContentHash.ToLowerInvariant() + "_" + artifact.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
                run.Append(CultureInfo.InvariantCulture, $$"""
                    { using namespace {{scope}}; Instance heap; Context context(heap);
                      Storage<std::array<Argument, 2>> arguments(context); if (!arguments.value) std::abort();
                      measure("object-warm", "{{item.Name}}", {{item.Iterations}}, {{item.Count}}, [&](std::int32_t seed, std::int32_t count) {
                        (*arguments.value)[0].bits = std::bit_cast<std::uint32_t>(seed); (*arguments.value)[1].bits = std::bit_cast<std::uint32_t>(count);
                        auto result = {{root}}(context, arguments.value->data());
                        if (context.pending != 0u || context.used != sizeof(*arguments.value) || context.depth != 0 || context.top != nullptr) std::abort();
                        return result;
                      });
                    }
                    """);
            }
        }
        source.Append("#include <chrono>\n#include <cstdio>\n#include <cstdlib>\n").Append(wrappers);
        source.Append(CultureInfo.InvariantCulture, $$"""
            template<class Invoke> [[gnu::noinline]] static void measure(const char* backend, const char* name, int iterations, int count, Invoke invoke) {
              std::int32_t warm = 19088743;
              for (int i = 0; i < {{Warmup}}; ++i) warm = invoke(std::bit_cast<std::int32_t>(std::bit_cast<std::uint32_t>(warm) + static_cast<std::uint32_t>(i)), count);
              // Observable warmup prevents removal without adding volatile accesses to the timed loop.
              std::fprintf(stderr, "%s %s warmup=%d\n", backend, name, warm);
              for (int sample = 0; sample < {{Samples}}; ++sample) {
                std::int32_t value = 19088743;
                auto start = std::chrono::steady_clock::now();
                for (int i = 0; i < iterations; ++i) value = invoke(std::bit_cast<std::int32_t>(std::bit_cast<std::uint32_t>(value) + static_cast<std::uint32_t>(i)), count);
                auto stop = std::chrono::steady_clock::now();
                auto ns = std::chrono::duration<double, std::nano>(stop - start).count() / iterations;
                std::printf("%s\t%s\t%d\t%d\t%.3f\t%d\n", backend, name, sample, iterations, ns, value);
              }
            }
            int main() {
            {{run}}
            }
            """);
        return source.ToString();
    }

    private static void Reference()
    {
        Func<int, int, int>[] functions = [Workloads.Arithmetic, Workloads.Loop, Workloads.Calls];
        for (int index = 0; index < Cases.Length; index++)
        {
            var item = Cases[index]; var invoke = functions[index]; int warm = 19088743;
            for (int i = 0; i < Warmup; i++) warm = invoke(unchecked(warm + i), item.Count);
            Console.Error.WriteLine(FormattableString.Invariant($"dotnet {item.Name} warmup={warm}"));
            for (int sample = 0; sample < Samples; sample++)
            {
                int value = 19088743; long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < item.Iterations; i++) value = invoke(unchecked(value + i), item.Count);
                double ns = Stopwatch.GetElapsedTime(start).TotalNanoseconds / item.Iterations;
                Console.WriteLine(FormattableString.Invariant($"dotnet\t{item.Name}\t{sample}\t{item.Iterations}\t{ns:F3}\t{value}"));
            }
        }
    }

    private sealed record Record(string Backend, string Name, int Sample, int Iterations, double Nanoseconds, int Checksum);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static IEnumerable<Record> Parse(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
    {
        string[] fields = line.Split('\t');
        if (fields.Length != 6) throw new InvalidOperationException("Invalid native/reference measurement record.");
        return new Record(fields[0], fields[1], int.Parse(fields[2], CultureInfo.InvariantCulture), int.Parse(fields[3], CultureInfo.InvariantCulture),
            double.Parse(fields[4], CultureInfo.InvariantCulture), int.Parse(fields[5], CultureInfo.InvariantCulture));
    });

    private static async Task<string> ProcessOutput(string command, string[] arguments, string directory, bool reference = false)
    {
        var start = new ProcessStartInfo(command) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        if (reference) { start.Environment["DOTNET_TieredCompilation"] = "0"; start.Environment["DOTNET_ReadyToRun"] = "0"; }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot launch " + command);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
        try { await Task.WhenAll(process.WaitForExitAsync(), stdout, stderr).WaitAsync(TimeSpan.FromSeconds(60)); }
        catch (TimeoutException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            throw new TimeoutException("Benchmark subprocess exceeded 60 seconds: " + command);
        }
        if (process.ExitCode != 0) throw new InvalidOperationException(command + " failed: " + await stderr);
        return await stdout;
    }
}
