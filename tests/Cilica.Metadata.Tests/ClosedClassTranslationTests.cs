using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedClassTranslationTests
{
    [Theory]
    [InlineData("Wide")] [InlineData("Slots")] [InlineData("References")] [InlineData("Faults")] [InlineData("SameWidth")]
    public void Closed_class_calls_preserve_exact_maps_through_lowering(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ClosedClassDispatchProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.NotEmpty(result.Program!.Flow.Dispatch);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ClosedClassDispatchProbe)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Closed_slots_and_composed_faults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(ClosedClassDispatchProbe.Wide), ClosedClassDispatchProbe.Wide), (nameof(ClosedClassDispatchProbe.Slots), ClosedClassDispatchProbe.Slots),
            (nameof(ClosedClassDispatchProbe.References), ClosedClassDispatchProbe.References), (nameof(ClosedClassDispatchProbe.Faults), ClosedClassDispatchProbe.Faults),
            (nameof(ClosedClassDispatchProbe.SameWidth), ClosedClassDispatchProbe.SameWidth)], typeof(ClosedClassDispatchProbe));

    [Theory]
    [InlineData("missing")] [InlineData("wrong-body")] [InlineData("slot-specialization")]
    public void Detached_dispatch_mutations_are_rejected(string invalid)
    {
        var result = IntegerObjectTranslationTests.Translate("Wide", fixture: typeof(ClosedClassDispatchProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow;
        if (invalid == "missing") flow = flow with { Dispatch = [] };
        if (invalid == "wrong-body")
        {
            var map = flow.Dispatch[0];
            var wrong = map.Targets[0] with { Implementation = program.Flow.Flow.Flow.Entry };
            flow = flow with { Dispatch = flow.Dispatch.SetItem(0, map with { Targets = map.Targets.SetItem(0, wrong) }) };
        }
        if (invalid == "slot-specialization")
        {
            var binding = flow.Bindings.First(b => b.VirtualSlot is not null);
            var slot = binding.VirtualSlot!;
            flow = flow with { Bindings = flow.Bindings.Replace(binding, binding with { VirtualSlot = slot with {
                Method = slot.Method with { Specialization = "forged" } } }) };
        }
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = flow }));
    }

    [Fact]
    public void Struct_virtual_signature_remains_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("ValueSignature", fixture: typeof(ClosedClassDispatchProbe));
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
