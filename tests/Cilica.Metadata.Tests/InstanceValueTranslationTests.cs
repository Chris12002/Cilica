using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InstanceValueTranslationTests
{
    [Theory]
    [InlineData("Flow")] [InlineData("NullOrder")]
    public void Direct_class_instance_calls_retain_value_and_borrow_signatures(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(InstanceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        Assert.Contains(program.Flow.Bindings, b => b.Kind == ObjectMethodKind.Instance);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(InstanceValues)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Native_class_instance_values_borrows_and_null_order_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [(nameof(InstanceValues.Flow), InstanceValues.Flow), (nameof(InstanceValues.NullOrder), InstanceValues.NullOrder)], typeof(InstanceValues));

    [Fact]
    public void Struct_return_packet_invocation_preserves_callvirt_null_check()
    {
        var result = IntegerObjectTranslationTests.Translate(nameof(InstanceValues.NullOrder), fixture: typeof(InstanceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        var target = program.Flow.Bindings.Single(b => b.Kind == ObjectMethodKind.Instance).Method;
        var lowered = ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow;
        Assert.Contains(lowered.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements),
            s => s is IrStatement.Invoke call && call.Target == target && call.NullCheckReceiver && !call.VirtualDispatch);
    }
}
