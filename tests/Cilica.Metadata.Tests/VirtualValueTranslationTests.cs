using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class VirtualValueTranslationTests
{
    [Theory]
    [InlineData("Flow")] [InlineData("Aliasing")] [InlineData("NullOrder")] [InlineData("Empty")]
    public void Virtual_values_use_exact_maps_and_caller_owned_return_packets(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(VirtualValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.NotEmpty(result.Program!.Flow.Dispatch);
        var lowered = ValueScalarLowering.LowerExceptions(result.Program);
        ScalarManagedExceptionIrValidator.Validate(lowered);
        Assert.Contains(lowered.Flow.Flow.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements),
            s => s is IrStatement.Invoke { VirtualDispatch: true, NullCheckReceiver: true });
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(VirtualValues)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Class_virtual_values_borrows_and_faults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(VirtualValues.Flow), VirtualValues.Flow), (nameof(VirtualValues.Aliasing), VirtualValues.Aliasing),
            (nameof(VirtualValues.NullOrder), VirtualValues.NullOrder), (nameof(VirtualValues.Empty), VirtualValues.Empty), (nameof(VirtualValues.Narrow), VirtualValues.Narrow)], typeof(VirtualValues));
    [Fact]
    public void Borrowed_parameter_rebinding_remains_actionable()
    {
        var result = IntegerObjectTranslationTests.Translate("BorrowRebind", fixture: typeof(VirtualValues));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Rebinding", System.StringComparison.Ordinal));
    }

}
