using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cilica.Examples.Primitive;

if (args.Length != 1) { Console.Error.WriteLine("Usage: Prepare <output-directory>"); return 2; }
string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
// This executable runs only the authored example referenced at build time.
// Cilica runs separately and receives the image as data; no arbitrary image is loaded here.
string assembly = typeof(IntegerKernel).Assembly.Location;
int root = typeof(IntegerKernel).GetMethod(nameof(IntegerKernel.Calculate))!.MetadataToken;
int step = typeof(IntegerKernel).GetMethod(nameof(IntegerKernel.Step))!.MetadataToken;
string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant();
string entry = "cilica_m_" + hash + "_" + root.ToString("x8", CultureInfo.InvariantCulture) + "_try";
var probes = new (string Label, int Initial, int Step, int Count)[]
{
    ("normal", 10, 3, 5), ("overflow", int.MaxValue, 1, 1),
    ("recovery", 10, -3, 2), ("empty", int.MaxValue, 1, 0),
};
var cases = new StringBuilder("struct Case { const char* label; std::int32_t initial, step, count; };\nstatic const Case cases[] = {\n");
var expected = new StringBuilder();
foreach (var probe in probes)
{
    cases.Append(CultureInfo.InvariantCulture, $"    {{\"{probe.Label}\", {probe.Initial}, {probe.Step}, {probe.Count}}},\n");
    try
    {
        int value = IntegerKernel.Calculate(probe.Initial, probe.Step, probe.Count);
        expected.Append(CultureInfo.InvariantCulture, $"{probe.Label}: success {value}\n");
    }
    catch (OverflowException)
    {
        // Step's independently authored addition is at IL_0002.
        expected.Append(CultureInfo.InvariantCulture, $"{probe.Label}: Overflow method=0x{step:x8} il=2 image={hash}\n");
    }
}
cases.Append("};\n");
Write("assembly.path", assembly); Write("root.token", root.ToString("x8", CultureInfo.InvariantCulture));
Write("entry.hpp", "#pragma once\n#include \"generated.cpp\"\n#define CILICA_DEMO_ENTRY " + entry + "\n");
Write("cases.hpp", cases.ToString()); Write("expected.txt", expected.ToString());
return 0;

void Write(string name, string text) => File.WriteAllText(Path.Combine(directory, name), text, new UTF8Encoding(false));
