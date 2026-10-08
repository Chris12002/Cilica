using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxedVariantTests
{
    private static readonly string[] Names=["Exact","Covariant","Contravariant","Explicit","Inherited","ExactWins","AllocatingFaults","Null","PreparedListCopy","PreparedReadOnlyCopy"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(BoxedVariantGapProbe),Names));
    [Fact]
    public async Task Boxed_variant_probes_validate_at_both_stages()
    {
        foreach(var program in await Programs.Value)
        {
            ManagedExceptionIrValidator.Validate(program);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
        }
    }
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)]
    public async Task Boxed_variance_mutation_allocations_and_faults_match_dotnet(bool optimized,int group)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(BoxedVariantGapProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("UnsupportedAmbiguous","exactly one")] [InlineData("UnsupportedGeneric","exactly one")]
    [InlineData("ListCopy","compatible reference/scalar packets")] [InlineData("ReadOnlyCopy","compatible reference/scalar packets")]
    public async Task Ambiguity_generic_members_and_new_return_catalog_gaps_remain_diagnostic(string name,string diagnostic)
    {
        using var files=new ResolverFiles(); var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(BoxedVariantGapProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BoxedVariantGapProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Message.Contains(diagnostic,StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("missing")] [InlineData("owner")] [InlineData("token")] [InlineData("return")]
    [InlineData("parameters")] [InlineData("visibility")] [InlineData("name")]
    public async Task Detached_boxed_variant_contracts_cannot_be_forged(string mutation)
    {
        var program=(await Programs.Value)[1];
        var resolver=new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info=>{if(info.Type==typeof(ValueTypeLayout)) info.Properties.Single(p=>p.Name==nameof(ValueTypeLayout.EnumConstants)).ShouldSerialize=(owner,value)=>!((ValueTypeLayout)owner).EnumConstants.IsDefault; if(info.Type==typeof(ObjectStorage)) info.PolymorphismOptions=new(){DerivedTypes={new(typeof(ObjectStorage.Int32),"i32"),new(typeof(ObjectStorage.Int64),"i64"),new(typeof(ObjectStorage.Reference),"reference"),new(typeof(ObjectStorage.Value),"value")}};});
        var options=new JsonSerializerOptions{TypeInfoResolver=resolver};
        foreach(var original in new[]{program.Flow.InterfaceDispatch!,ValueScalarLowering.LowerExceptions(program).Flow.Flow.Flow.Flow.InterfaceDispatch!})
        {
            var plan=JsonSerializer.Deserialize<ObjectInterfaceDispatchPlan>(JsonSerializer.Serialize(original,options),options)!;
            ObjectInterfacePlanValidator.Validate(plan);
            var map=plan.Dispatch.First(m=>m.Targets.Any(t=>t.VariantDeclaration is not null));
            var target=map.Targets.First(t=>t.VariantDeclaration is not null);var variant=target.VariantDeclaration!;
            Assert.NotNull(plan.Methods.Single(m=>m.Binding.Method==target.Implementation).BoxedDeclaration);
            var declaration=plan.Declarations.Single(d=>IrMethodId.From(d.Source)==map.Declaration);
            var changed=mutation switch{
                "owner"=>variant with{Owner=declaration.Owner},
                "token"=>variant with{Source=variant.Source with{Token=variant.Source.Token+1}},
                "return"=>variant with{ReturnType=IrType.Int32},
                "parameters"=>variant with{Parameters=default},
                "visibility"=>variant with{OwnerExternallyVisible=!variant.OwnerExternallyVisible},
                "name"=>variant with{Name="Forged"},_=>null};
            Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(plan with{Dispatch=plan.Dispatch.Replace(map,map with{Targets=map.Targets.Replace(target,target with{VariantDeclaration=changed})})}));
        }
    }
}
