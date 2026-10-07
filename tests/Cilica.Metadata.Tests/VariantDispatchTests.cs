using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class VariantDispatchTests
{
    private static readonly string[] Names = ["Covariant", "Contravariant", "Override", "Explicit", "Exact", "Null", "Iterator"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(VariantDispatchProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Variant_dispatch_preserves_arguments_results_slots_and_exact_precedence(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(VariantDispatchProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedAmbiguous")] [InlineData("UnsupportedGeneric")]
    public void Multiple_candidates_and_generic_variant_members_remain_diagnostic(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(VariantDispatchProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("exactly one", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("missing")] [InlineData("owner")] [InlineData("token")] [InlineData("return")]
    [InlineData("parameters")] [InlineData("visibility")] [InlineData("name")]
    public async Task Detached_variant_evidence_is_validated_before_and_after_lowering(string mutation)
    {
        var program = (await Programs.Value)[0];
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Type == typeof(ObjectStorage)) info.PolymorphismOptions = new() {
                DerivedTypes = { new(typeof(ObjectStorage.Int32), "i32"), new(typeof(ObjectStorage.Int64), "i64"),
                    new(typeof(ObjectStorage.Reference), "reference"), new(typeof(ObjectStorage.Value), "value") } };
        });
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        foreach (var original in new[] { program.Flow.InterfaceDispatch!, ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch! })
        {
            var plan = JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(original, options), options)!;
            ObjectInterfacePlanValidator.Validate(plan);
            var map = plan.Dispatch.First(m => m.Targets.Any(t => t.VariantDeclaration is not null));
            var target = map.Targets.First(t => t.VariantDeclaration is not null); var variant = target.VariantDeclaration!;
            var declaration = plan.Declarations.Single(d => IrMethodId.From(d.Source) == map.Declaration);
            var changed = mutation switch {
                "owner" => variant with { Owner = declaration.Owner },
                "token" => variant with { Source = variant.Source with { Token = variant.Source.Token + 1 } },
                "return" => variant with { ReturnType = IrType.Int32 },
                "parameters" => variant with { Parameters = default },
                "visibility" => variant with { OwnerExternallyVisible = !variant.OwnerExternallyVisible },
                "name" => variant with { Name = "Forged" },
                _ => null
            };
            var bad = plan with { Dispatch = plan.Dispatch.Replace(map, map with { Targets = map.Targets.Replace(target, target with { VariantDeclaration = changed }) }) };
            Assert.Throws<ArgumentException>(() => ObjectInterfacePlanValidator.Validate(bad));
        }
    }
}
