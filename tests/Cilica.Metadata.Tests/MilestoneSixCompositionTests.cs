using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class MilestoneSixCompositionTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(MilestoneSixCompositionProbe),["CollectionsAndUnwind"]));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Collections_formatting_type_filters_and_finally_survive_forced_collection(bool optimized)
    {
        var program=Assert.Single(await Programs.Value);
        var regions = program.Flow.Flow.Flow.Methods.SelectMany(m => m.ExceptionRegions).ToArray();
        Assert.Contains(regions, r => r.Kind == IrExceptionClause.Filter);
        Assert.Contains(regions, r => r.Kind == IrExceptionClause.Finally);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Composed",program,MilestoneSixCompositionProbe.CollectionsAndUnwind)]);
    }
}
