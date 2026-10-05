using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.Metadata.Resolution;

namespace Cilica.LibraryBuild;

public sealed record LibraryBuildRequest(string CompilerDirectory, ImmutableArray<string> Sources,
    ImmutableArray<string> References, string OutputDirectory, string AssemblyName = "Cilica.Library",
    bool CoreLibrary = false, ImmutableArray<LibraryIntrinsicRequest> Intrinsics = default);
public sealed record LibraryBuildResult(string AssemblyPath, string ManifestPath, string ManifestSha256);

/// <summary>Build-host tool for trusted source; never part of input-assembly compilation or automatic approval.</summary>
public static class LibraryBuilder
{
    private const long MaximumInputBytes = 512L * 1024 * 1024;
    private const int MaximumFileBytes = 128 * 1024 * 1024;
    private const int MaximumLogCharacters = 262_144;

    public static async Task<LibraryBuildResult> Build(LibraryBuildRequest request)
    {
        if (request.Sources.IsDefaultOrEmpty || request.Sources.Length > 256 || request.References.IsDefault
            || (request.CoreLibrary ? request.References.Length != 0 : request.References.Length == 0)
            || request.References.Length > 64 || string.IsNullOrEmpty(request.AssemblyName) || request.AssemblyName.Length > 128
            || !char.IsAsciiLetter(request.AssemblyName[0])
            || request.AssemblyName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')))
            throw new ArgumentException("Supply 1..256 source files, 1..64 references (zero for the explicit core profile) and a simple assembly name.");
        var intrinsicRequests = request.Intrinsics.IsDefault ? ImmutableArray<LibraryIntrinsicRequest>.Empty : request.Intrinsics;
        LibraryIntrinsicRequests.Validate(intrinsicRequests);
        string output = Path.GetFullPath(request.OutputDirectory);
        if (Path.Exists(output)) throw new IOException("Output directory already exists; build evidence is never overwritten.");
        string parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Output must have a parent directory.");
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".cilica-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var inputs = new List<LibraryBuildInput>(); long totalBytes = 0;
            var paths = new HashSet<string>(StringComparer.Ordinal);
            string runtime = Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());
            string runtimeVersion = Path.GetFileName(runtime);
            string root = Path.GetFullPath(Path.Combine(runtime, "../../.."));
            string host = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            // Pin the host/runtime used by the tool, with roll-forward disabled for the compiler.
            // Installed host bytes are observed before and after; this is not a hostile-host sandbox.
            Record("compiler", host, host);
            foreach (string file in Directory.EnumerateFiles(runtime).Order(StringComparer.Ordinal)) Record("compiler", file, file);
            foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "host", "fxr"), "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)) Record("compiler", file, file);
            Record("compiler", typeof(LibraryBuilder).Assembly.Location, typeof(LibraryBuilder).Assembly.Location);
            Record("compiler", typeof(AssemblyResolver).Assembly.Location, typeof(AssemblyResolver).Assembly.Location);
            foreach (string name in new[] { "csc.dll", "csc.deps.json", "csc.runtimeconfig.json", "Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll" })
                Snapshot("compiler", Path.Combine(request.CompilerDirectory, name), "compiler/" + name);
            var sources = request.Sources.Select((file, i) => Snapshot("source", file, "source/" + i + ".cs")).ToArray();
            var references = request.References.Select((file, i) => Snapshot("reference", file, "reference/" + i + ".dll")).ToArray();
            string assembly = request.AssemblyName + ".dll";
            string[] arguments = ["exec", "--fx-version", runtimeVersion, "compiler/csc.dll", "-noconfig", "-nostdlib+",
                "-nologo", "-target:library", "-deterministic+", "-optimize+", "-debug-", "-langversion:9.0", "-nullable:disable",
                "-unsafe-", "-warnaserror+", .. request.CoreLibrary ? new[] { "-runtimemetadataversion:v4.0.30319" } : [],
                "-out:" + assembly, "-pathmap:" + stage + "=/_/cilica-library",
                .. references.Select(p => "-reference:" + p), .. sources];
            if (arguments.Length > 1024 || arguments.Any(a => a.Length > 4096))
                throw new ArgumentException("Effective compiler arguments exceed admission bounds.");
            var environment = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_ROOT"] = root, ["DOTNET_ROLL_FORWARD"] = "Disable", ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US", ["DOTNET_EnableDiagnostics"] = "0",
            };
            string options = Path.Combine(stage, "build-options.json");
            await File.WriteAllBytesAsync(options, JsonSerializer.SerializeToUtf8Bytes(new {
                Host = host, RuntimeVersion = runtimeVersion, Platform = RuntimeInformation.RuntimeIdentifier,
                WorkingDirectory = stage, Environment = environment, Arguments = arguments,
                SourceOrigins = request.Sources.Select(Path.GetFullPath), ReferenceOrigins = request.References.Select(Path.GetFullPath),
                request.CoreLibrary, IntrinsicRequests = intrinsicRequests,
                Scope = "Fixed Roslyn invocation; no analyzers, generators, response files, MSBuild targets or automatic intrinsic approval."
            }));
            Record("options", options, "build-options.json");
            var start = new ProcessStartInfo(host) { WorkingDirectory = stage, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            start.Environment.Clear();
            foreach (var pair in environment) start.Environment.Add(pair.Key, pair.Value);
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            var log = await Compile(start);
            await File.WriteAllTextAsync(Path.Combine(stage, "compiler.log"), log);
            foreach (var input in inputs)
                if (HashFile(Path.IsPathRooted(input.Path) ? input.Path : Path.Combine(stage, input.Path)) != input.Sha256)
                    throw new IOException("A build input changed during compilation: " + input.Path);
            using var resolver = AssemblyResolver.Create([]).Value!;
            var image = resolver.OpenInput(Path.Combine(stage, assembly));
            if (!image.Success) throw new InvalidOperationException("Compiler output is not a valid assembly: " + string.Join("; ", image.Diagnostics));
            var manifest = new LibraryBuildManifest(LibraryBuildManifest.FormatName, 1, LibraryBuildManifest.CurrentRuntimeProfile,
                [new(image.Value!.Identity.DisplayName, image.Value.ContentHash, [.. inputs], [.. arguments],
                    LibraryIntrinsicRequests.Describe(resolver, image.Value, intrinsicRequests))]);
            byte[] evidence = AssemblyResolver.SerializeLibraryBuildManifest(manifest);
            if (evidence.Length > AssemblyResolver.MaximumLibraryManifestBytes) throw new InvalidOperationException("Build evidence exceeds admission bounds.");
            await File.WriteAllBytesAsync(Path.Combine(stage, "library-build.json"), evidence);
            string digest = Convert.ToHexStringLower(SHA256.HashData(evidence));
            resolver.Dispose();
            // Readers see a complete new directory, or no published build at all.
            Directory.Move(stage, output);
            return new(Path.Combine(output, assembly), Path.Combine(output, "library-build.json"), digest);

            string Snapshot(string role, string source, string relative)
            {
                string target = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = File.OpenRead(source);
                if (input.Length > MaximumFileBytes) throw new IOException("Build input exceeds the per-file byte bound.");
                using (var destination = File.Create(target))
                {
                    var buffer = new byte[81920]; int read; long copied = 0;
                    while ((read = input.Read(buffer)) != 0)
                    {
                        copied += read;
                        if (copied > MaximumFileBytes) throw new IOException("Build input grew beyond the per-file byte bound.");
                        destination.Write(buffer, 0, read);
                    }
                }
                Record(role, target, relative);
                return relative;
            }
            void Record(string role, string file, string path)
            {
                if (inputs.Count >= 4096 || !paths.Add(path) || path.Length > 4096)
                    throw new IOException("Build input paths exceed admission bounds or repeat.");
                long length = new FileInfo(file).Length; totalBytes += length;
                if (length > MaximumFileBytes || totalBytes > MaximumInputBytes) throw new IOException("Build inputs exceed the byte bound.");
                inputs.Add(new(role, path, HashFile(file)));
            }
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); }
    }

    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920]; int read; long total = 0;
        while ((read = file.Read(buffer)) != 0)
        {
            total += read;
            if (total > MaximumFileBytes) throw new IOException("Build input exceeds the per-file byte bound.");
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static async Task<string> Compile(ProcessStartInfo start)
    {
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("Could not start the selected compiler.");
        process.StandardInput.Close();
        var overflow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string> stdout = Read(process.StandardOutput), stderr = Read(process.StandardError);
        Task completion = Task.WhenAll(process.WaitForExitAsync(), stdout, stderr);
        try
        {
            Task first = await Task.WhenAny(completion, overflow.Task).WaitAsync(TimeSpan.FromSeconds(60));
            await first;
            if (overflow.Task.IsCompleted) await overflow.Task;
            string log = await stdout + await stderr;
            if (process.ExitCode != 0) throw new InvalidOperationException("Compiler failed (" + process.ExitCode + "): " + log);
            return log;
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            try { await completion.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception error) when (error is IOException or TimeoutException) { }
            throw;
        }
        async Task<string> Read(StreamReader reader)
        {
            var result = new StringBuilder(); var buffer = new char[4096]; int read;
            while ((read = await reader.ReadAsync(buffer)) != 0)
            {
                if (result.Length + read > MaximumLogCharacters)
                { overflow.TrySetException(new InvalidOperationException("Compiler output exceeded the capture bound.")); continue; }
                result.Append(buffer, 0, read);
            }
            return result.ToString();
        }
    }
}
