using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NonpublicEnumOperationTests
{
    private static readonly string[] Names = ["Box", "Defined", "TypeTests", "Compare"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Internal = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InternalEnumProbe), Names));
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Nested = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NestedEnumOwner), Names));
    [Theory]
    [InlineData(false, false, 0)] [InlineData(true, false, 0)]
    [InlineData(false, false, 1)] [InlineData(true, false, 1)]
    [InlineData(false, true, 0)] [InlineData(true, true, 0)]
    [InlineData(false, true, 1)] [InlineData(true, true, 1)]
    public async Task Visible_enum_operations_match_dotnet(bool optimized, bool nested, int group)
    {
        var programs = await (nested ? Nested : Internal).Value;
        var fixture = nested ? typeof(NestedEnumOwner) : typeof(InternalEnumProbe);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], fixture.GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 2).Take(2).ToArray());
    }
}
