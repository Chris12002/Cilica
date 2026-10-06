using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class AppleAdapterTests
{
    public static bool Enabled => OperatingSystem.IsMacOS() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CILICA_APPLE_SIMULATOR"));

    [Fact(SkipUnless = nameof(Enabled), Skip = "Requires explicit CILICA_APPLE_SIMULATOR and the pinned local Apple toolchain.")]
    public async Task Swift_adapter_serializes_native_calls_and_owns_copied_values_and_handles()
    {
        using var files = new ResolverFiles();
        string root = Path.GetFullPath(CoreLibraryBuildTests.CorePath("../.."));
        string directory = Environment.GetEnvironmentVariable("CILICA_APPLE_OUTPUT") ?? files.DirectoryPath("apple-adapter");
        Directory.CreateDirectory(directory);
        var artifact = await EmbeddingSessionTests.Emit();
        string source = Path.Combine(directory, "engine.cpp"), header = Path.Combine(directory, "engine.h"), manifest = Path.Combine(directory, "engine.json");
        // Never overwrite evidence or another invocation's input files.
        WriteNew(source, artifact.Text); WriteNew(header, artifact.Header); WriteNew(manifest, artifact.ManifestJson);
        string package = Path.Combine(directory, "package");
        await Check(["-B", "tools/Apple/build_xcframework.py", "--source", source, "--header", header, "--manifest", manifest,
            "--module", "CilicaSession", "--output", package], root);
        await Check(["-B", "examples/Apple/check_package.py", "--package", package, "--output", Path.Combine(directory, "swift"),
            "--adapter", "--consumer", "tests/Apple/Session.swift.in", "--simulator", Environment.GetEnvironmentVariable("CILICA_APPLE_SIMULATOR")!], root);
        Assert.Contains("PASS Swift serialized sessions", File.ReadAllText(Path.Combine(directory, "swift", "simulator.txt")), StringComparison.Ordinal);
    }

    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream); writer.Write(text);
    }
    private static async Task Check(string[] arguments, string root)
    {
        var result = await NativeProcess.Run("python3", arguments, root, timeout: TimeSpan.FromMinutes(4));
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr);
    }
}
