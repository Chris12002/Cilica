using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Library;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryFormattingTests
{
    [Fact]
    public void Managed_port_matches_invariant_decimal_text_across_width_boundaries()
    {
        var random = new Random(73031);
        var bytes = new byte[8];
        for (int index = 0; index < 2048; index++)
        {
            random.NextBytes(bytes);
            ulong unsigned = index < 17 ? LibraryFormattingProbe.UnsignedCase(index) : BitConverter.ToUInt64(bytes);
            long signed = index < 17 ? LibraryFormattingProbe.SignedCase(index) : unchecked((long)unsigned);
            Assert.Equal(unsigned.ToString(CultureInfo.InvariantCulture), IntegerFormatting.UInt64Invariant(unsigned));
            Assert.Equal(signed.ToString(CultureInfo.InvariantCulture), IntegerFormatting.Int64Invariant(signed));
            Assert.Equal(unchecked((uint)unsigned).ToString(CultureInfo.InvariantCulture), IntegerFormatting.UInt32Invariant(unchecked((uint)unsigned)));
            Assert.Equal(unchecked((int)signed).ToString(CultureInfo.InvariantCulture), IntegerFormatting.Int32Invariant(unchecked((int)signed)));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Native_managed_library_output_matches_every_dotnet_code_unit(bool optimized)
    {
        string[] names = ["Signed64", "Unsigned64", "Signed32", "Unsigned32"];
        var probes = names.Select(name => {
            var translated = IntegerObjectTranslationTests.Translate(name, fixture: typeof(LibraryFormattingProbe),
                referenceDirectories: [Path.GetDirectoryName(typeof(IntegerFormatting).Assembly.Location)!]);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            Func<int, int, int> oracle = (index, position) => {
                string text = name switch {
                    "Signed64" => LibraryFormattingProbe.SignedCase(index).ToString(CultureInfo.InvariantCulture),
                    "Unsigned64" => LibraryFormattingProbe.UnsignedCase(index).ToString(CultureInfo.InvariantCulture),
                    "Signed32" => unchecked((int)LibraryFormattingProbe.SignedCase(index)).ToString(CultureInfo.InvariantCulture),
                    _ => unchecked((uint)LibraryFormattingProbe.UnsignedCase(index)).ToString(CultureInfo.InvariantCulture) };
                return position < 0 ? text.Length : position < text.Length ? text[position] : -1;
            };
            return (name, translated.Program!, oracle);
        }).ToArray();
        return IntegerObjectTranslationTests.VerifyTranslated(optimized, probes,
            (from index in Enumerable.Range(0, 17) from position in Enumerable.Range(-1, 22) select (index, position)).ToArray());
    }
}
