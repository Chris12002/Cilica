using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqPredicateTests
{
    private static readonly string[] Names = ["ArraysAny", "ArraysAll", "ArraysCount", "Empty", "Mutations", "References", "Covariance", "Fallback", "Faults", "Null", "IgnoreCounts", "ArrayFaults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqPredicateProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Predicates_mutation_short_circuit_and_fault_order_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var inputs = new[] { int.MinValue, -3, -1, 0, 1, 2, 3, 4, int.MaxValue };
        var pairs = (from a in inputs from b in new[] { -4, -3, -2, -1, 0, 1, 2, 3 } select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqPredicateProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), pairs);
    }
}
