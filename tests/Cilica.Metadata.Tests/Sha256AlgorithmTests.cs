using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Cilica.Library;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class Sha256AlgorithmTests
{
    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1")]
    public void Published_known_answers(string input, string expected) =>
        Assert.Equal(expected, Convert.ToHexStringLower(ManagedSha256.Digest(Encoding.ASCII.GetBytes(input))));
    [Fact]
    public void Million_byte_answer_and_randomized_inputs_match_dotnet()
    {
        var million = Enumerable.Repeat((byte)'a', 1_000_000).ToArray();
        Assert.Equal("cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0", Convert.ToHexStringLower(ManagedSha256.Digest(million)));
        var random = new Random(25607);
        for (int i = 0; i < 128; i++)
        {
            var bytes = new byte[random.Next(8193)]; random.NextBytes(bytes);
            Assert.Equal(SHA256.HashData(bytes), ManagedSha256.Digest(bytes));
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Every_digest_byte_at_padding_boundaries_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(Sha256AlgorithmProbe), ["Byte"]))[0];
        int[] lengths = [-2, -1, 0, 1, 2, 3, 31, 55, 56, 57, 63, 64, 65, 119, 120, 127, 128, 129, 255, 256, 257];
        var pairs = (from length in lengths from position in Enumerable.Range(-1, 33) select (length, position)).ToArray();
        int Oracle(int length, int position)
        {
            var digest = SHA256.HashData(Sha256AlgorithmProbe.Input(length));
            return position < 0 ? digest.Length : digest[position];
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Byte", program, Oracle)], pairs);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Repeated_results_are_independent_and_null_is_rejected(bool optimized)
    {
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(Sha256AlgorithmProbe), ["Repeated", "Null"]);
        int Oracle(int a, int b)
        {
            int value = SHA256.HashData(Sha256AlgorithmProbe.Input(a & 127))[b & 31];
            return value * (1 + 257 + 65537);
        }
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Repeated", programs[0], Oracle), ("Null", programs[1], Sha256AlgorithmProbe.Null)]);
    }
}
