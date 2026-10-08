using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace Cilica.Metadata.Tests;
internal static class NativeEvidence
{
    // Opt-in evidence capture must happen before ResolverFiles disposes successful outputs.
    internal static void Record(string source, bool optimized, string? executable = null)
    {
        string? directory = Environment.GetEnvironmentVariable("CILICA_NATIVE_EVIDENCE_DIRECTORY");
        if (string.IsNullOrEmpty(directory)) return;
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Native evidence directory must be absolute.");
        Directory.CreateDirectory(directory);
        string digest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source)));
        string? binary = executable is null ? null : Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable)));
        File.WriteAllText(Path.Combine(directory, digest + (optimized ? "-O2.json" : "-O0.json")),
            JsonSerializer.Serialize(new { source_sha256 = digest, executable_sha256 = binary,
                optimization = optimized ? "O2" : "O0", sanitizers = "address,undefined", source_name = Path.GetFileName(source) }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
