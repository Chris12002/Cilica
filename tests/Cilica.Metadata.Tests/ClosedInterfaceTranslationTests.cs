using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedInterfaceTranslationTests
{
    [Theory]
    [InlineData("Wide")] [InlineData("References")] [InlineData("Faults")] [InlineData("SameWidth")]
    public void Closed_interface_calls_discover_exact_executable_closures(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ClosedDispatchProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Program!.Flow.InterfaceDispatch);
        var flow = result.Program.Flow.Flow.Flow;
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values),
            v => v.Expression is IrExpression.Call { VirtualDispatch: true, NullCheckReceiver: true });
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ClosedDispatchProbe)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Closed_dispatch_values_and_caught_faults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(ClosedDispatchProbe.Wide), ClosedDispatchProbe.Wide), (nameof(ClosedDispatchProbe.References), ClosedDispatchProbe.References),
            (nameof(ClosedDispatchProbe.Faults), ClosedDispatchProbe.Faults), (nameof(ClosedDispatchProbe.SameWidth), ClosedDispatchProbe.SameWidth)], typeof(ClosedDispatchProbe));

    [Fact]
    public void Closed_interface_value_signature_uses_checked_value_plan()
    {
        var result = IntegerObjectTranslationTests.Translate("ValueSignature", fixture: typeof(ClosedDispatchProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
    }

    [Theory]
    [InlineData("VariantCall")]
    public void Unimplemented_dispatch_extensions_remain_actionable(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ClosedDispatchProbe));
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }
}
