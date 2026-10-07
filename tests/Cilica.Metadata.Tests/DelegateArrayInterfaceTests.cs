using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DelegateArrayInterfaceTests
{
    private static readonly string[] Names = ["Count", "Read", "Copy", "Clear"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DelegateArrayInterfaceProbe), Names));
    [Fact]
    public async Task Delegate_array_interface_helper_retains_element_metadata() => Assert.Equal(Names.Length, (await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Native_delegate_array_interfaces_preserve_targets_and_ownership(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(DelegateArrayInterfaceProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
}
