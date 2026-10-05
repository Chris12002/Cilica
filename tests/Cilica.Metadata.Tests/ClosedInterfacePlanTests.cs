using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedInterfacePlanTests
{
    [Theory]
    [InlineData(typeof(int))] [InlineData(typeof(long))] [InlineData(typeof(string))]
    public void Closed_plans_preserve_exact_mapping_signatures_and_bounded_work(Type argument)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        Type[] definitions = [typeof(ClosedDispatchBase<>), typeof(ClosedDispatchOverride<>), typeof(ClosedDispatchHidden<>),
            typeof(ClosedDispatchReimplemented<>), typeof(ClosedDispatchExplicit<>), typeof(ClosedDispatchExplicitChild<>), typeof(ClosedDispatchReexplicit<>)];
        var receivers = definitions.Select(t => t.MakeGenericType(argument)).ToArray();
        var roots = receivers.Select(t => ClosedInterfaceMethodTargetTests.Closed(resolver, input, t)).ToImmutableArray();
        var contract = typeof(IClosedDispatch<>).MakeGenericType(argument);
        var declarations = contract.GetMethods().Select(m => ClosedInterfaceMethodTargetTests.Declaration(resolver, input, contract, m)).ToImmutableArray();
        var budget = new MemberResolutionBudget();
        var result = ObjectInterfacePlanBuilder.BuildClosed(resolver, roots, declarations, library, budget);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var plan = result.Plan!; Assert.True(plan.Types.Integer64);
        ObjectInterfacePlanValidator.Validate(JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(plan))!);
        foreach (var map in plan.Dispatch)
            foreach (var target in map.Targets)
            {
                var receiver = receivers.Single(t => t.MetadataToken == target.Receiver.Token);
                var expected = receiver.GetInterfaceMap(contract);
                int index = Array.FindIndex(expected.InterfaceMethods, m => m.MetadataToken == map.Declaration.Token);
                Assert.Equal(expected.TargetMethods[index].MetadataToken, target.Implementation.Token);
                Assert.Equal(64, target.Implementation.Specialization.Length);
            }
        Assert.Equal(14, plan.Dispatch.Sum(m => m.Targets.Length));
        Assert.All(plan.Declarations, d => Assert.Equal(d.Owner.Specialization, d.Source.Specialization));
        Assert.Contains(plan.Declarations, d => d.Name == "Echo" && d.ReturnType.Kind ==
            (argument == typeof(long) ? IrTypeKind.Int64 : argument == typeof(int) ? IrTypeKind.Int32 : IrTypeKind.Reference));
        int used = 65536 - budget.Remaining;
        Assert.True(ObjectInterfacePlanBuilder.BuildClosed(resolver, roots, declarations, library, new(used)).Success);
        var shortResult = ObjectInterfacePlanBuilder.BuildClosed(resolver, roots, declarations, library, new(used - 1));
        Assert.False(shortResult.Success); Assert.Contains(shortResult.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }

    [Fact]
    public void Two_instantiations_of_one_definition_keep_distinct_maps()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(IClosedDispatch<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var roots = new[] { typeof(ClosedDispatchBase<int>), typeof(ClosedDispatchBase<uint>) }
            .Select(t => ClosedInterfaceMethodTargetTests.Closed(resolver, input, t)).ToImmutableArray();
        var declarations = new[] { typeof(IClosedDispatch<int>), typeof(IClosedDispatch<uint>) }
            .Select(t => ClosedInterfaceMethodTargetTests.Declaration(resolver, input, t, t.GetMethod("Echo")!)).ToImmutableArray();
        var result = ObjectInterfacePlanBuilder.BuildClosed(resolver, roots, declarations, library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(2, result.Plan!.Dispatch.Length);
        Assert.Equal(2, result.Plan.Dispatch.Select(d => d.Declaration).Distinct().Count());
        Assert.All(result.Plan.Dispatch, d => Assert.Single(d.Targets));
        var forged = declarations[0] with { Signature = declarations[0].Signature with { ReturnType = new TypeSignature.Primitive(PrimitiveTypeCode.Int64) } };
        Assert.False(ObjectInterfacePlanBuilder.BuildClosed(resolver, roots, [forged], library).Success);
        Assert.False(ObjectInterfacePlanBuilder.BuildClosed(resolver,
            [roots[0] with { Arguments = new([new TypeSignature.Variable(false, 0)]) }], declarations, library).Success);
    }
}
