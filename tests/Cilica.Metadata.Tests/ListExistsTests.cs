using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ListExistsTests
{
    private static readonly string[] Names = ["Integers", "References", "Values", "NullEmpty"];
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Exists_preserves_predicate_mutation_faults_and_short_circuiting(bool optimized, int probe)
    {
        string name=Names[probe];
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(ListExistsProbe),name))[0];
        var pairs=(from a in Enumerable.Range(0,9) from b in new[]{-1,0,1,3,5,7,73,79,83,89} select (a,b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(name,program,typeof(ListExistsProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())],pairs);
    }
}
