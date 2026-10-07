using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ProtectedStaticTests
{
    private static readonly string[] Names=["Constructor","Deep","Intersection","Generic","Hidden","Void"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ProtectedStaticProbe),Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Proven_base_static_calls_preserve_values_faults_and_roots(bool optimized)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(ProtectedStaticProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
    [Theory]
    [InlineData("ancestry")] [InlineData("private")]
    public async Task Detached_IR_rechecks_base_ancestry_and_member_access(string mutation)
    {
        var program=(await Programs.Value)[1];
        ObjectIrProgram Mutate(ObjectIrProgram objects)
        {
            if(mutation=="private") {
                var binding=objects.Bindings.Single(b=>objects.Flow.Methods.Any(m=>m.Source.Name.Contains("StaticFamilyRoot::Compute",StringComparison.Ordinal)&&IrMethodId.From(m.Source)==b.Method));
                return objects with {Bindings=objects.Bindings.Replace(binding,binding with{Attributes=(binding.Attributes&~MethodAttributes.MemberAccessMask)|MethodAttributes.Private})};
            }
            var types=objects.Flow.Types;var leaf=types.Types.Single(t=>t.Source.Name.Contains("StaticFamilyLeaf",StringComparison.Ordinal));
            return objects with {Flow=objects.Flow with {Types=types with {Types=types.Types.Replace(leaf,leaf with {BaseType=types.ObjectBase})}}};
        }
        var valueFlow=program.Flow.Flow.Flow;
        var objects=new ObjectIrProgram(new(valueFlow.Entry,valueFlow.Methods,valueFlow.Types.References),program.Flow.Bindings){InterfaceDispatch=program.Flow.InterfaceDispatch,Dispatch=program.Flow.Dispatch,AbstractMethods=program.Flow.AbstractMethods};
        var changed=Mutate(objects);
        var invalid=program with {Flow=program.Flow with {Flow=program.Flow.Flow with {Flow=valueFlow with {Types=valueFlow.Types with {References=changed.Flow.Types}}},Bindings=changed.Bindings}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(invalid));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var scalarObjects=boxes.Flow.Flow;
        var broken=scalar with {Flow=scalar.Flow with {Flow=boxes with {Flow=boxes.Flow with {Flow=Mutate(scalarObjects)}}}};
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(broken));
    }
    [Fact]
    public async Task Nested_family_scope_and_protected_instance_calls_remain_diagnostic()
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(ProtectedStaticProbe).Assembly.Location).Value!;
        foreach(string name in new[]{"UnsupportedNested","UnsupportedInstance"}) {
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ProtectedStaticProbe).GetMethod(name)!.MetadataToken),implementation);
            Assert.False(result.Success,name);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Calls require",StringComparison.Ordinal));
        }
    }
    [Theory]
    [InlineData(MethodAttributes.Family,true,true,true)]
    [InlineData(MethodAttributes.FamORAssem,true,true,true)]
    [InlineData(MethodAttributes.FamANDAssem,true,true,false)]
    [InlineData(MethodAttributes.FamANDAssem,true,false,true)]
    [InlineData(MethodAttributes.Family,false,false,false)]
    [InlineData(MethodAttributes.Family,false,true,false)]
    [InlineData(MethodAttributes.FamANDAssem,false,false,false)]
    [InlineData(MethodAttributes.FamORAssem,false,false,true)]
    [InlineData(MethodAttributes.FamORAssem,false,true,false)]
    [InlineData(MethodAttributes.Private,true,false,false)]
    [InlineData(MethodAttributes.Assembly,true,true,false)]
    public async Task Family_and_assembly_access_require_actual_ancestry(MethodAttributes access,bool derived,bool foreign,bool expected)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        var image=new ResolutionAssemblyBuilder(new("StaticBase",new Version(1,0,0,0)));
        var obj=image.TypeReference("System","Object",image.Reference(implementation.Identity));
        var baseType=image.Type("Test","Base",baseType:obj);
        var helper=image.Method("Compute",[0,2,8,8,8],attributes:access|MethodAttributes.Static|MethodAttributes.HideBySig,
            emit:il=>{il.LoadArgument(0);il.LoadArgument(1);il.OpCode(ILOpCode.Add);il.OpCode(ILOpCode.Ret);});
        var source=image;EntityHandle parent=baseType;EntityHandle call=helper;EntityHandle objectType=obj;
        if(foreign) {
            await File.WriteAllBytesAsync(Path.Combine(Path.GetDirectoryName(built.AssemblyPath)!,"StaticBase.dll"),image.Build(),TestContext.Current.CancellationToken);
            source=new ResolutionAssemblyBuilder(new("StaticDerived",new Version(1,0,0,0)));
            parent=source.TypeReference("Test","Base",source.Reference(new("StaticBase",new Version(1,0,0,0))));
            objectType=source.TypeReference("System","Object",source.Reference(implementation.Identity));
            call=source.Member(parent,"Compute",0,2,8,8,8);
        }
        source.Type("Test","Derived",baseType:derived?parent:objectType);
        var entry=source.Method("Execute",[0,2,8,8,8],emit:il=>{il.LoadArgument(0);il.LoadArgument(1);il.Call(call);il.OpCode(ILOpCode.Ret);});
        string path=files.Write("StaticInput.dll",source.Build());
        string reference=LibraryBuildProducerTests.Request(files,CoreLibraryBuildTests.CorePath("CoreTypes.cs")).References.Single();
        using var fresh=AssemblyResolver.Create([Path.GetDirectoryName(built.AssemblyPath)!,Path.GetDirectoryName(reference)!]).Value!;
        ResolvedType Resolve(LibraryTypeIdentity identity)=>fresh.ResolveType(fresh.ResolveAssembly(identity.Assembly).Value!,(TypeDefinitionHandle)MetadataTokens.EntityHandle(identity.Token)).Value!;
        var freshPlan=fresh.CreateLibraryBindingPlan(plan.Bindings.Select(b=>new LibraryTypeMapping(Resolve(b.Contract),Resolve(b.Implementation)))).Value!;
        using var scope=fresh.CreateTrustedLibraryCompilationScope(freshPlan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=fresh.OpenInput(path).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,entry,fresh.ResolveAssembly(implementation.Identity).Value!);
        Assert.True(result.Success==expected,string.Join("; ",result.Diagnostics));
        if(expected) {
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
            if(access==MethodAttributes.Family&&foreign) foreach(bool optimized in new[]{false,true})
                await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Foreign",result.Program!,(a,b)=>unchecked(a+b))]);
        }
        else Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Calls require",StringComparison.Ordinal));
    }

}
