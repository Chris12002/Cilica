using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace Cilica.Metadata.Tests;

internal static class Fixture
{
    public const string Basic = "Cilica.Fixtures.Basic";
    public const string Forwarding = "Cilica.Fixtures.Forwarding";
    public const string ControlFlow = "Cilica.Fixtures.ControlFlow";

    /// <summary>Fixture assemblies are copied next to the tests as data files; they are never loaded.</summary>
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, name + ".dll");

    public static byte[] Bytes(string name) => File.ReadAllBytes(PathOf(name));
}

/// <summary>
/// Compares output with a reviewed golden file under <c>Golden/</c>. Set
/// <c>CILICA_UPDATE_GOLDEN=1</c> to rewrite golden files, then review the diff.
/// </summary>
internal static class Golden
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static void Verify(string fileName, string actual, [CallerFilePath] string callerPath = "")
    {
        string directory = Path.Combine(Path.GetDirectoryName(callerPath)!, "Golden");
        string path = Path.Combine(directory, fileName);
        if (Environment.GetEnvironmentVariable("CILICA_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, actual, Utf8);
            return;
        }

        Assert.True(File.Exists(path), $"Golden file '{fileName}' is missing. Run the tests with CILICA_UPDATE_GOLDEN=1 to create it, then review it.");
        string expected = File.ReadAllText(path, Utf8);
        if (expected == actual)
        {
            return;
        }

        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        int line = 0;
        while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
        {
            line++;
        }
        string expectedLine = line < expectedLines.Length ? expectedLines[line] : "<end of file>";
        string actualLine = line < actualLines.Length ? actualLines[line] : "<end of file>";
        Assert.Fail($"Output differs from golden file '{fileName}' at line {line + 1}.\nExpected: {expectedLine}\nActual:   {actualLine}\n" +
            "If the change is intended, rerun with CILICA_UPDATE_GOLDEN=1 and review the diff.");
    }
}

/// <summary>Corruption tests run 500 iterations by default; set <c>CILICA_FUZZ_ITERATIONS</c> for longer runs.</summary>
internal static class Fuzz
{
    public static int Iterations { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("CILICA_FUZZ_ITERATIONS"), out int iterations) && iterations > 0 ? iterations : 500;
}
