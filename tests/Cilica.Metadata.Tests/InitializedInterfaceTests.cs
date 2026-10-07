using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InitializedInterfaceTests
{
    private static readonly string[] Names = ["Private", "Explicit", "Boxed", "Null", "Closed", "Failure"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InitializedInterfaceProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Interface_targets_preserve_initialization_ownership_and_failures(bool optimized, int group)
    {
        var programs = await Programs.Value; int start = group < 2 ? group * 2 : group + 2;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(InitializedInterfaceProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(start).Take(group < 2 ? 2 : 1).ToArray(),
            expectedStaticRoots: group == 2 ? 2 : 0, persistentRoots: group == 3);
    }
    [Fact]
    public void Strict_owner_initialization_remains_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("UnsupportedStrict", fixture: typeof(InitializedInterfaceProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("strict type", StringComparison.Ordinal));
    }
    [Fact]
    public void Metadata_plan_admission_keeps_the_closed_profile_and_strict_owner_boundaries()
    {
        using var resolver = Cilica.Metadata.Resolution.AssemblyResolver.Create([System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(InitializedInterfaceProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var declaration = resolver.ResolveDeclaredMethod(input, System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(IInitializedReader).GetMethod("Read")!.MetadataToken)).Value!;
        var owner = ClosedInterfaceMethodTargetTests.Closed(resolver, input, typeof(FailedInitializedReader));
        var plan = ObjectInterfacePlanBuilder.BuildClosed(resolver, [owner], [declaration], library);
        Assert.True(plan.Success, string.Join("; ", plan.Diagnostics));
        Assert.False(ObjectInterfacePlanBuilder.Build(resolver, [owner.Definition], [declaration], library).Success);
        var strict = ClosedInterfaceMethodTargetTests.Closed(resolver, input, typeof(StrictInitializedReader));
        Assert.False(ObjectInterfacePlanBuilder.BuildClosed(resolver, [strict], [declaration], library).Success);
    }

}
