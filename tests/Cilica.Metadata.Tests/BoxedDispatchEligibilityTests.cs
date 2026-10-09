using System;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxedDispatchEligibilityTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Unrelated_boxes_cannot_hide_incomplete_dispatch_maps(bool lowered)
    {
        var result = IntegerObjectTranslationTests.Translate("NamedWithScalar", fixture: typeof(BoxedInterfaces));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        if (!lowered)
        {
            var plan = program.Flow.InterfaceDispatch!; Assert.NotEmpty(plan.Dispatch);
            var invalid = program with { Flow = program.Flow with { InterfaceDispatch = plan with { Dispatch = plan.Dispatch.RemoveAt(0) } } };
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        }
        else
        {
            var scalar = ValueScalarLowering.LowerExceptions(program);
            var objects = scalar.Flow.Flow.Flow.Flow;
            var plan = objects.InterfaceDispatch!; Assert.NotEmpty(plan.Dispatch);
            var invalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with {
                Flow = new(objects with { InterfaceDispatch = plan with { Dispatch = plan.Dispatch.RemoveAt(0) } }) } } };
            Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
        }
    }
}
