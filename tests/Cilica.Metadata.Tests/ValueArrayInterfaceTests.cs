using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ValueArrayInterfaceTests
{
    private static readonly (string Name, Func<int,int,int> Oracle)[] Probes = [
        ("Copy",ValueArrayInterfaceProbe.Copy), ("Faults",ValueArrayInterfaceProbe.Faults),
        ("Empty",ValueArrayInterfaceProbe.Empty), ("Lists",ValueArrayInterfaceProbe.Lists)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(ValueArrayInterfaceProbe), Probes.Select(p=>p.Name).ToArray()));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Value_array_interfaces_and_list_wrappers_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p,i)=>(p.Name,programs[i],p.Oracle)).ToArray());
    }
    [Fact]
    public async Task Detached_value_array_helpers_reject_wrong_packets_and_empty_shapes()
    {
        var programs = await Programs.Value;
        foreach (var program in programs.Take(3))
        {
            var scalar = ValueScalarLowering.LowerExceptions(program);
            var plan = scalar.Flow.Flow.Flow.Flow.InterfaceDispatch!;
            ObjectInterfacePlanValidator.Validate(plan);
            var get = plan.Methods.First(m=>m.ArrayHelper && m.Name=="Get");
            var changed = get with { Parameters = get.Parameters.Add(IrType.Int32) };
            Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(plan with { Methods=plan.Methods.Replace(get,changed) }));
        }
        var empty = ValueScalarLowering.LowerExceptions(programs[2]).Flow.Flow.Flow.Flow.InterfaceDispatch!;
        var shape = Assert.Single(empty.Types.ValueArrays);
        Assert.True(shape.Empty);
        var forged = empty with { Types=empty.Types with { ValueArrays=[shape with { Empty=false }] } };
        Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(forged));
    }
}
