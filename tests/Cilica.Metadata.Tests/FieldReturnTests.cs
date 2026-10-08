using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class FieldReturnTests
{
    private static readonly string[] Names = ["OriginalReceiver", "FaultPreservesDestination", "VirtualReturn", "EmptyReturn", "ReadonlyConstruction", "JoinedAllocations", "LoopRetainsReceiver", "InstanceAfterRead", "InstanceAfterStore"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(FieldReturnProbe), Names));
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Returned_values_keep_call_order_ownership_and_complete_publication(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) => (name, programs[i], typeof(FieldReturnProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Callee_fault_after_building_result_preserves_existing_field(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(FieldReturnProbe), "CalleeFaultPreservesField"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("CalleeFaultPreservesField", program, FieldReturnProbe.CalleeFaultPreservesField)]);
    }
    [Fact]
    public async Task Nullable_or_mutable_alias_destinations_remain_diagnostic()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(FieldReturnProbe).Assembly.Location).Value!;
        foreach (string name in new[] { "Order", "NullableEmptyReturn", "NullableJoin", "NullableBackEdge", "AddressedReceiver", "FinallyClearsReceiver", "ExceptionDropsReceiver" })
        {
            var result = ValueTranslator.TranslateIntegerObjects(scope, input,
                (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(FieldReturnProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.False(result.Success); Assert.Null(result.Program);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("destination proven non-null", StringComparison.Ordinal));
        }
    }
}
