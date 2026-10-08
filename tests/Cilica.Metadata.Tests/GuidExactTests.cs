using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GuidExactTests
{
    private static readonly string[] Names = ["RoundTrip", "Storage", "Repeated", "Whitespace", "Mutation", "Corpus"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(GuidExactProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Exact_formats_preserve_selection_validation_and_output_reset(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name, i) => (name, programs[i], typeof(GuidExactProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>()));
        int[] units = [0, 9, 13, 32, 0x85, 0xa0, 0x1680, 0x2000, 0x200a, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000, 0x200b, 0xfeff, 0xd800, 0xffff];
        var pairs = group == 0 ? (from a in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, int.MaxValue } from b in Enumerable.Range(0, 17) select (a, b)).ToArray()
            : group == 1 ? (from a in units from b in Enumerable.Range(0, 5) select (a, b)).ToArray()
            : group == 2 ? (from a in Enumerable.Range(0, 70) from b in new[] { 0, 32, 43, 45, 48, 65, 70, 97, 102, 120, 0xd800, 0xffff } select (a, b)).ToArray()
            : (from a in Enumerable.Range(0, 51) from b in Enumerable.Range(0, 17) select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, (group == 0 ? probes.Take(3) : probes.Skip(group + 2).Take(1)).ToArray(), pairs);
    }
}
