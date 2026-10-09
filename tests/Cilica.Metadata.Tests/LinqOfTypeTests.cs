using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class LinqOfTypeTests
{
    private static readonly string[] Names = ["References", "Subtypes", "Scalars", "Nullable", "Values", "TypedShortcut", "Identity", "Unstarted", "NullAndReset", "Faults", "NullableProjection", "ValueProjection", "ReferenceCurrent", "ArraySource"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LinqOfTypeProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task OfType_iteration_and_terminal_callbacks_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LinqOfTypeProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Reference_projections_match_dotnet(bool optimized)
    {
        string[] names = ["ReferenceProjection", "NongenericProjection"];
        var programs = await CoreLibraryBuildTests.TranslateFixtures(typeof(LinqOfTypeProbe), names);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(LinqOfTypeProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
}
