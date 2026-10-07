using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ReadOnlyEnumerationTests
{
    private static readonly string[] Names = ["Lists", "Arrays", "Mutation", "References", "Values", "Empty", "Forwarding", "Faults", "LegacyEmpty"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ReadOnlyEnumerationProbe), Names));

    [Fact]
    public async Task Readonly_cursor_probes_translate() => Assert.Equal(Names.Length, (await Programs.Value).Length);

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Readonly_cursors_match_dotnet(bool optimized, int batch)
    {
        var programs = await Programs.Value;
        var probes = Names.Select((name, i) => (name, programs[i], typeof(ReadOnlyEnumerationProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray();
        foreach (var probe in probes.Skip(batch * 3).Take(3))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized, [probe], expectedStaticRoots: probe.name is "Empty" or "LegacyEmpty" ? 1 : 0);
    }
}
