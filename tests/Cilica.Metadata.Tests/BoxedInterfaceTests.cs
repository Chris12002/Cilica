using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxedInterfaceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Boxed_interface_ancestry_variance_storage_and_faults_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [("Relations", BoxedInterfaces.Relations), ("Storage", BoxedInterfaces.Storage), ("Faults", BoxedInterfaces.Faults), ("ConfinedWithDispatch", BoxedInterfaces.ConfinedWithDispatch)], typeof(BoxedInterfaces));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Named_boxed_interface_dispatch_matches_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [("UnsupportedDispatch", BoxedInterfaces.UnsupportedDispatch), ("MixedDispatch", BoxedInterfaces.MixedDispatch),
             ("EscapedDispatch", BoxedInterfaces.EscapedDispatch)], typeof(BoxedInterfaces));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Mutable_generic_empty_and_faulted_boxes_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [("Mutable", BoxedInterfaces.Mutable), ("PayloadReturn", BoxedInterfaces.PayloadReturn),
             ("MutationFault", BoxedInterfaces.MutationFault), ("Allocation", BoxedInterfaces.Allocation),
             ("GenericValue", BoxedInterfaces.GenericValue), ("GenericReference", BoxedInterfaces.GenericReference),
             ("Empty", BoxedInterfaces.Empty)], typeof(BoxedInterfaces));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Known_named_receivers_coexist_with_returned_scalar_boxes(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [("NamedWithScalar", BoxedInterfaces.NamedWithScalar), ("ClassWithScalar", BoxedInterfaces.ClassWithScalar)], typeof(BoxedInterfaces));
    [Fact]
    public void Unknown_object_casts_still_require_scalar_confinement()
    {
        var result = IntegerObjectTranslationTests.Translate("UnknownWithScalar", fixture: typeof(BoxedInterfaces));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Scalar box interface casts and dispatch require", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Detached_maps_reject_forged_payloads_and_bindings(bool lowered)
    {
        var result = IntegerObjectTranslationTests.Translate("PayloadReturn", fixture: typeof(BoxedInterfaces));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var plan = lowered ? ValueScalarLowering.LowerExceptions(result.Program!).Flow.Flow.Flow.Flow.InterfaceDispatch!
            : result.Program!.Flow.InterfaceDispatch!;
        ObjectInterfacePlanValidator.Validate(plan);
        var map = plan.Dispatch.First(m => m.Targets.Any());
        var target = map.Targets[0];
        var fact = plan.Methods.Single(m => m.Binding.Method == target.Implementation);
        void Reject(ObjectInterfaceDispatchPlan changed) => Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(changed));
        void BadFact(ObjectDispatchMethod changed) => Reject(plan with { Methods = plan.Methods.Replace(fact, changed) });
        Reject(plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = [] }) });
        Reject(plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = map.Targets.Add(target) }) });
        Reject(plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = [target with { Receiver = plan.Types.ObjectBase }] }) });
        BadFact(fact with { BoxedDeclaration = null });
        BadFact(fact with { BoxedDeclaration = fact.BoxedDeclaration! with { Kind = ObjectMethodKind.Static } });
        BadFact(fact with { BoxedDeclaration = fact.BoxedDeclaration! with { Owner = plan.Types.ObjectBase } });
        BadFact(fact with { ArrayHelper = true });
        BadFact(fact with { Binding = fact.Binding with { Attributes = fact.Binding.Attributes | System.Reflection.MethodAttributes.Abstract } });
        BadFact(fact with { Parameters = fact.Parameters.RemoveAt(0) });
        BadFact(fact with { Parameters = fact.Parameters.SetItem(0, IrType.Reference(plan.Types.ObjectBase)) });
        BadFact(fact with { Parameters = fact.Parameters.SetItem(0, IrType.Location(fact.Parameters[0].Element!, readOnly: true)) });
        Reject(plan with { Values = null, BoxedValues = null });
        if (lowered) Reject(plan with { Values = plan.BoxedValues });
        else Reject(plan with { BoxedValues = plan.Values });
    }
    [Fact]
    public void Scalar_execution_rejects_receiver_layouts_from_a_different_catalog()
    {
        var result = IntegerObjectTranslationTests.Translate("PayloadReturn", fixture: typeof(BoxedInterfaces));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var scalar = ValueScalarLowering.LowerExceptions(result.Program!);
        var program = scalar.Flow.Flow.Flow.Flow;
        var plan = program.InterfaceDispatch!;
        var values = plan.BoxedValues!;
        var box = values.Types.Single(t => t.Source.Name.EndsWith(".BoxState", StringComparison.Ordinal));
        // Visibility does not change native field storage, but is still part of
        // the catalog identity that the dispatch proof must share with the body.
        var changedValues = values with { Types = values.Types.Replace(box, box with { ExternallyVisible = !box.ExternallyVisible }) };
        var changed = program with { InterfaceDispatch = plan with { BoxedValues = changedValues } };
        var boxed = scalar.Flow.Flow with { Flow = new(changed) };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = new(boxed) }));
    }
}
