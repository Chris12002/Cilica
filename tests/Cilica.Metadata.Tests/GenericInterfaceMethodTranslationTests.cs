using System;
using System.Linq;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GenericInterfaceMethodTranslationTests
{
    private static readonly string[] Names = ["Scalars", "ClosedOwners", "Explicit", "Receivers", "Null", "Values", "Overloads", "Factory", "NullOnly", "Faults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(GenericInterfaceMethodProbe), Names));
    [Fact]
    public async Task Closed_methods_have_distinct_authenticated_maps_at_both_ir_stages()
    {
        var programs = await Programs.Value;
        foreach (var program in programs)
        {
            ManagedExceptionIrValidator.Validate(program);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
            var plan = program.Flow.InterfaceDispatch!;
            Assert.Contains(plan.Declarations, d => d.GenericMethod is not null);
            if (!plan.Methods.IsEmpty) Assert.Contains(plan.Methods, m => m.Binding.GenericInterfaceMethod is not null);
        }
        var scalars = programs[0].Flow.InterfaceDispatch!;
        Assert.Equal(4, scalars.Dispatch.Length);
        Assert.Equal(4, scalars.Methods.Select(m => m.Binding.Method).Distinct().Count());
        Assert.Equal(2, programs[6].Flow.InterfaceDispatch!.Dispatch.Length);
    }
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Closed_generic_interface_execution_matches_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(GenericInterfaceMethodProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData("declaration-context")] [InlineData("method-context")] [InlineData("owner-context")]
    [InlineData("method-arguments")] [InlineData("nonfinal")] [InlineData("slot")] [InlineData("cross-map")]
    public async Task Detached_specialization_and_slot_forgeries_fail_at_both_stages(string mutation)
    {
        var logical = (await Programs.Value)[0].Flow.InterfaceDispatch!;
        var scalar = ValueScalarLowering.LowerExceptions((await Programs.Value)[0]).Flow.Flow.Flow.Flow.InterfaceDispatch!;
        foreach (var plan in new[] { logical, scalar })
        {
            var declaration = plan.Declarations.First(d => d.Name == "Stamp"
                && d.GenericMethod!.Methods[0] is ClosedTypeShape.Primitive { Code: PrimitiveTypeCode.Int32 });
            var map = plan.Dispatch.Single(m => m.Declaration == IrMethodId.From(declaration.Source));
            var target = map.Targets[0]; var method = plan.Methods.Single(m => m.Binding.Method == target.Mapping);
            var binding = method.Binding; var context = binding.GenericInterfaceMethod!;
            ObjectInterfaceDispatchPlan invalid = mutation switch {
                "declaration-context" => plan with { Declarations = plan.Declarations.Replace(declaration, declaration with { GenericMethod = null }) },
                "method-context" => plan with { Methods = plan.Methods.Replace(method, method with { Binding = binding with { GenericInterfaceMethod = null } }) },
                "owner-context" => plan with { Methods = plan.Methods.Replace(method, method with { Binding = binding with { GenericInterfaceMethod = context with { Types = [] } } }) },
                "method-arguments" => plan with { Methods = plan.Methods.Replace(method, method with { Binding = binding with { GenericInterfaceMethod = context with { Methods = [new ClosedTypeShape.Primitive(PrimitiveTypeCode.UInt32)] } } }) },
                "nonfinal" => plan with { Methods = plan.Methods.Replace(method, method with { Binding = binding with { Attributes = binding.Attributes & ~MethodAttributes.Final } }) },
                "slot" => plan with { Methods = plan.Methods.Replace(method, method with { Binding = binding with { VirtualSlot = binding.VirtualSlot! with { Method = binding.Method with { Specialization = binding.Owner.Specialization } } } }) },
                _ => SwapMap(plan, map)
            };
            Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(invalid));
        }
    }
    private static ObjectInterfaceDispatchPlan SwapMap(ObjectInterfaceDispatchPlan plan, ObjectInterfaceDispatch map)
    {
        var other = plan.Dispatch.Single(m => m.Declaration.Token == map.Declaration.Token && m.Declaration != map.Declaration);
        return plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = other.Targets }) };
    }
    [Theory]
    [InlineData("UnsupportedOverride")] [InlineData("UnsupportedClass")] [InlineData("UnsupportedMixed")]
    public void Generic_override_and_class_calls_remain_diagnostic(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(GenericInterfaceMethodProbe));
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }

}
