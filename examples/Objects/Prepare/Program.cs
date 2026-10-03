using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cilica.Examples.Objects;

if (args.Length != 1) { Console.Error.WriteLine("Usage: Prepare <output-directory>"); return 2; }
string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
// Only this build-time-referenced, authored kernel runs here. Compilation is a separate process.
string assembly = typeof(Inventory).Assembly.Location;
string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant();
MethodInfo Method(string name) => typeof(Inventory).GetMethod(name)!;
// Assert the exact authored IL shapes before relying on their known fault offsets.
CheckBody(nameof(Inventory.At), [0x02, 0x03, 0x9a, 0x2a]); // ldarg.0; ldarg.1; ldelem.ref; ret
CheckBody(nameof(Inventory.Add), [0x02, 0x03, 0xd6, 0x2a]); // ldarg.0; ldarg.1; add.ovf; ret
byte[] nameBody = Method(nameof(Inventory.Name)).GetMethodBody()!.GetILAsByteArray()!;
if (nameBody.Length != 7 || nameBody[0] != 0x02 || nameBody[1] != 0x6f || nameBody[6] != 0x2a
    || Method(nameof(Inventory.Name)).Module.ResolveMethod(BitConverter.ToInt32(nameBody, 2)) != typeof(IItem).GetMethod(nameof(IItem.Name)))
    throw new InvalidOperationException("Name helper IL changed; review the oracle fault origin.");
var probes = new (string Label, int Turns, int Selected, int Mode)[]
{
    ("empty", 0, 0, 0), ("normal", 6, 1, 0), ("other-item", 7, 2, 0),
    ("null", 2, 1, 1), ("after-null", 2, 1, 0),
    ("bounds", 3, 3, 0), ("after-bounds", 3, 2, 0),
    ("negative-index", 0, -1, 0), ("overflow", 1, 0, 2), ("after-overflow", 1, 0, 0),
    ("maximum-turns", 12, 1, 0), ("invalid-turns", 13, 0, 0),
};
var cases = new StringBuilder("struct Case { const char* label; std::int32_t turns, selected, mode; };\nstatic const Case cases[] = {\n");
var expected = new StringBuilder();
foreach (var probe in probes)
{
    cases.Append(CultureInfo.InvariantCulture, $"    {{\"{probe.Label}\", {probe.Turns}, {probe.Selected}, {probe.Mode}}},\n");
    try
    {
        // Evaluate before appending: an interpolated-string handler may append
        // the success prefix before a later interpolation throws.
        int value = Inventory.Run(probe.Turns, probe.Selected, probe.Mode);
        expected.Append(CultureInfo.InvariantCulture, $"{probe.Label}: success {value}\n");
    }
    catch (Exception exception) when (exception is OverflowException or NullReferenceException or IndexOutOfRangeException)
    {
        var (status, origin, offset) = exception switch
        {
            OverflowException => (2, nameof(Inventory.Add), 2),
            NullReferenceException => (4, nameof(Inventory.Name), 1),
            _ => (10, nameof(Inventory.At), 2),
        };
        expected.Append(CultureInfo.InvariantCulture, $"{probe.Label}: fault {status} method=0x{Method(origin).MetadataToken:x8} il={offset} image={hash}\n");
    }
}
cases.Append("};\n");
Write("assembly.path", assembly); Write("runtime.path", typeof(object).Assembly.Location);
Write("root.token", Method(nameof(Inventory.Run)).MetadataToken.ToString("x8", CultureInfo.InvariantCulture));
Write("cases.hpp", cases.ToString()); Write("expected.txt", expected.ToString());
return 0;

void Write(string name, string text) => File.WriteAllText(Path.Combine(directory, name), text, new UTF8Encoding(false));
void CheckBody(string name, byte[] expectedBody)
{
    if (!Method(name).GetMethodBody()!.GetILAsByteArray()!.AsSpan().SequenceEqual(expectedBody))
        throw new InvalidOperationException(name + " helper IL changed; review the oracle fault origin.");
}
