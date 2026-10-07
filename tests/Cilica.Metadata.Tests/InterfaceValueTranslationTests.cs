using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InterfaceValueTranslationTests
{
    [Theory]
    [InlineData("Flow")] [InlineData("Aliasing")] [InlineData("NullOrder")] [InlineData("Empty")]
    public void Value_interface_plans_preserve_closed_signatures_and_lowered_destinations(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(InterfaceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        Assert.NotNull(program.Flow.InterfaceDispatch!.Values);
        Assert.Contains(program.Flow.InterfaceDispatch.Declarations, d => d.ReturnType.Kind == IrTypeKind.Value);
        var lowered = ValueScalarLowering.LowerExceptions(program);
        ScalarManagedExceptionIrValidator.Validate(lowered);
        var objects = lowered.Flow.Flow.Flow.Flow;
        Assert.Null(objects.InterfaceDispatch!.Values);
        Assert.DoesNotContain(objects.InterfaceDispatch.Declarations, d => d.ReturnType.Kind == IrTypeKind.Value);
        Assert.Contains(objects.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements),
            s => s is IrStatement.Invoke { VirtualDispatch: true, NullCheckReceiver: true });
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(InterfaceValues)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Interface_copies_borrows_faults_and_empty_returns_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(InterfaceValues.Flow), InterfaceValues.Flow), (nameof(InterfaceValues.Aliasing), InterfaceValues.Aliasing),
            (nameof(InterfaceValues.NullOrder), InterfaceValues.NullOrder), (nameof(InterfaceValues.Empty), InterfaceValues.Empty)], typeof(InterfaceValues));

    [Fact]
    public void Detached_value_plan_round_trip_keeps_the_same_layout_contract()
    {
        var result = IntegerObjectTranslationTests.Translate("Flow", fixture: typeof(InterfaceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        // Supply explicit tags for abstract field-storage variants; this tests
        // detached model equivalence without defining a new public wire format.
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            // Optional enum constants use default to mean absent on ordinary values.
            if (info.Type == typeof(ValueTypeLayout))
                info.Properties.Single(p => p.Name == nameof(ValueTypeLayout.EnumConstants)).ShouldSerialize =
                    (owner, _) => !((ValueTypeLayout)owner).EnumConstants.IsDefault;
            if (info.Type == typeof(ObjectStorage)) info.PolymorphismOptions = new() {
                DerivedTypes = { new(typeof(ObjectStorage.Int32), "i32"), new(typeof(ObjectStorage.Int64), "i64"),
                    new(typeof(ObjectStorage.Reference), "reference"), new(typeof(ObjectStorage.Value), "value") } };
        });
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        var plan = JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(program.Flow.InterfaceDispatch, options), options)!;
        var detached = program with { Flow = program.Flow with { InterfaceDispatch = plan } };
        ManagedExceptionIrValidator.Validate(detached);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(detached));
    }

    [Theory]
    [InlineData("missing-layouts")] [InlineData("different-layout")] [InlineData("missing-destination")]
    public void Detached_value_plan_mismatches_are_rejected(string invalid)
    {
        var result = IntegerObjectTranslationTests.Translate("Flow", fixture: typeof(InterfaceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        var plan = program.Flow.InterfaceDispatch!;
        if (invalid == "missing-destination")
        {
            var lowered = ValueScalarLowering.LowerExceptions(program);
            var boxes = lowered.Flow.Flow; var objects = boxes.Flow.Flow;
            var map = objects.InterfaceDispatch!;
            var declaration = map.Declarations.First(d => d.Parameters.Any(p => p.Kind == IrTypeKind.ManagedLocation));
            map = map with { Declarations = map.Declarations.Replace(declaration, declaration with { Parameters = declaration.Parameters.RemoveAt(declaration.Parameters.Length - 1) }) };
            Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(lowered with { Flow = lowered.Flow with {
                Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { InterfaceDispatch = map } } } } }));
            return;
        }
        if (invalid == "missing-layouts") plan = plan with { Values = null };
        else
        {
            var values = plan.Values!;
            var layout = values.Types.First(t => t.Fields.Length > 0);
            var field = layout.Fields[0];
            var changed = layout with { Fields = layout.Fields.SetItem(0, field with { Storage = field.Storage is ObjectStorage.Int32 ? new ObjectStorage.Int64() : new ObjectStorage.Int32() }) };
            plan = plan with { Values = values with { Types = values.Types.Replace(layout, changed) } };
        }
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { InterfaceDispatch = plan } }));
    }
}
