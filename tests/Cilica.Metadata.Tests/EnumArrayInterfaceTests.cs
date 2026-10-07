using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class EnumArrayInterfaceTests
{
    private static readonly string[] Names = ["Reads", "Aliases", "IntegerArray", "Search", "Copy", "GenericBoxes", "IntegerBoxes", "ActualBoxes", "States", "Faults", "Clear", "Empty"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(EnumArrayInterfaceProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    [InlineData(false,3)] [InlineData(true,3)] [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    public async Task Enum_interfaces_preserve_view_actual_identity_aliases_and_faults(bool optimized, int group)
    {
        var programs = await Programs.Value;
        int count = group == 5 ? 1 : 2;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(EnumArrayInterfaceProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(count).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Empty_enumerators_cache_by_requested_enum_identity(bool optimized) =>
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Empty", (await Programs.Value)[11], EnumArrayInterfaceProbe.Empty)], expectedStaticRoots: 2);
    [Theory]
    [InlineData("missing")] [InlineData("identity")] [InlineData("reference")] [InlineData("facts")]
    [InlineData("fact-name")] [InlineData("null-contract")] [InlineData("default-arguments")] [InlineData("null-argument")]
    public async Task Both_stages_reject_forged_enum_interface_arguments(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var catalog = flow.Types.References;
        var contract = catalog.Interfaces.First(i => i.Arguments.Any(a => a.Enum32 is not null));
        var argument = contract.Arguments.Single();
        var changed = mutation switch {
            "missing" => argument with { Enum32 = null },
            "identity" => argument with { Identity = new string('0',64) },
            "reference" => argument with { Reference = catalog.ObjectBase },
            _ => argument
        };
        var bad = catalog with { Interfaces = catalog.Interfaces.Replace(contract, contract with { Arguments = [changed] }),
            EnumArrays = mutation == "facts" ? [] : catalog.EnumArrays };
        if (mutation == "fact-name") bad = catalog with { EnumArrays = [.. catalog.EnumArrays.Select(e => e with { Source = e.Source with { RuntimeName = "Forged.Enum" } })] };
        if (mutation == "null-contract") bad = catalog with { Interfaces = catalog.Interfaces.Replace(contract, null!) };
        if (mutation == "default-arguments") bad = catalog with { Interfaces = catalog.Interfaces.Replace(contract, contract with { Arguments = default }) };
        if (mutation == "null-argument") bad = catalog with { Interfaces = catalog.Interfaces.Replace(contract, contract with { Arguments = [null!] }) };
        var logical = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with {
            Types = flow.Types with { References = bad } } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(logical));
        var scalar = ValueScalarLowering.LowerExceptions(program); var objects = scalar.Flow.Flow.Flow.Flow;
        var detached = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with {
            Flow = objects with { Flow = objects.Flow with { Types = bad } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(detached));
    }
    [Fact]
    public async Task Nongeneric_enumeration_cannot_use_a_compatible_but_different_enum_box()
    {
        var plan = ValueScalarLowering.LowerExceptions((await Programs.Value)[7]).Flow.Flow.Flow.Flow.InterfaceDispatch!;
        var declaration = plan.Declarations.Single(d => d.Name == "GetEnumerator" && plan.Types.Interfaces.Single(i => ObjectTypeId.From(i.Source) == d.Owner).ArrayContract == ObjectArrayInterfaceKind.Enumerable);
        var map = plan.Dispatch.Single(m => m.Declaration == IrMethodId.From(declaration.Source));
        var targets = map.Targets.Where(t => t.Receiver.ArrayElement?.Kind == ObjectArrayElementKind.Enum32).ToArray();
        Assert.True(targets.Length >= 2);
        var first = targets[0]; var second = targets[1];
        var bad = plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = map.Targets.Replace(first,
            first with { Mapping = second.Mapping, Implementation = second.Implementation }) }) };
        var failure = Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(bad));
        Assert.Contains("actual array element boxing identity", failure.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Array_and_scalar_box_runtime_names_cannot_disagree()
    {
        var program = (await Programs.Value)[6]; var catalog = program.Flow.Flow.Flow.Types;
        var fact = catalog.References.EnumArrays.First(e => catalog.Types.Any(t => t.ScalarEnum && ObjectTypeId.From(t.Source) == ObjectTypeId.From(e.Source)));
        var bad = catalog with { References = catalog.References with { EnumArrays = catalog.References.EnumArrays.Replace(fact,
            fact with { Source = fact.Source with { RuntimeName = "Forged.Enum" } }) } };
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(bad));
    }
    [Fact]
    public async Task Object_aliases_still_require_scalar_box_confinement()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(EnumArrayInterfaceProbe).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input,
            (System.Reflection.Metadata.MethodDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(typeof(EnumArrayInterfaceProbe).GetMethod("UnsupportedObjectAlias")!.MetadataToken), implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Scalar box interface casts", StringComparison.Ordinal));
    }

}
