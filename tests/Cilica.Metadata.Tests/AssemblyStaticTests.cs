using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class AssemblyStaticTests
{
    private static readonly string[] Names=["Numbers","References","Values","Closed","Readonly","FamilyAssembly","Nested","FailedInitialization"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(AssemblyStaticProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    [InlineData(false,6)] [InlineData(true,6)] [InlineData(false,7)] [InlineData(true,7)]
    public async Task Same_assembly_statics_match_dotnet(bool optimized,int probe)
    {
        var p=(await Programs.Value)[probe];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe],p,typeof(AssemblyStaticProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int,int,int>>())],
            persistentRoots:probe==7,expectedStaticRoots:probe==4?1:0);
    }
    [Theory]
    [InlineData(FieldAttributes.Assembly,true)] [InlineData(FieldAttributes.FamORAssem,true)]
    [InlineData(FieldAttributes.Private,false)] [InlineData(FieldAttributes.Family,false)]
    [InlineData(FieldAttributes.FamANDAssem,false)] [InlineData(FieldAttributes.PrivateScope,false)]
    public void Metadata_access_flags_keep_their_boundaries(FieldAttributes access,bool expected)
    {
        var b=new ResolutionAssemblyBuilder(new("AssemblyStaticFlags",new Version(1,0,0,0)));
        b.Type("Test","Holder",attributes:TypeAttributes.NotPublic);
        var field=b.FieldWithAttributes("Value",access|FieldAttributes.Static,6,8);
        b.Type("Test","Caller");var root=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldsfld);il.Token(field);il.OpCode(ILOpCode.Ret);});
        using var files=new ResolverFiles();using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image=resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(resolver,image,root,library);
        Assert.Equal(expected,result.Success);
        if(expected) ValueScalarLowering.LowerExceptions(result.Program!);
        else Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Static field access",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(FieldAttributes.Assembly)] [InlineData(FieldAttributes.FamORAssem)]
    public void Internal_fields_are_not_accessible_across_images(FieldAttributes access)
    {
        var identity=new AssemblyIdentity("ForeignStaticFields",new Version(1,0,0,0));var target=new ResolutionAssemblyBuilder(identity);
        target.Type("Test","Holder");target.FieldWithAttributes("Value",access|FieldAttributes.Static,6,8);
        var caller=new ResolutionAssemblyBuilder(new("ForeignStaticCaller",new Version(1,0,0,0)));
        var owner=caller.TypeReference("Test","Holder",caller.Reference(identity));var field=caller.Member(owner,"Value",6,8);
        caller.Type("Test","Caller");var root=caller.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldsfld);il.Token(field);il.OpCode(ILOpCode.Ret);});
        using var files=new ResolverFiles();var targetPath=files.Write("ForeignStaticFields.dll",target.Build());
        using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(targetPath)!,Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image=resolver.OpenInput(files.Write("input.dll",caller.Build())).Value!;var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(resolver,image,root,library);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Static field access",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("private")] [InlineData("family")] [InlineData("family-and-assembly")]
    [InlineData("private-scope")] [InlineData("target-chain")] [InlineData("caller-chain")] [InlineData("readonly")]
    public async Task Both_ir_stages_require_access_facts_and_readonly_ownership(string mutation)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        ObjectStaticField Change(ObjectStaticField f)
        {
            if(mutation=="target-chain")return f with {LexicalOwners=[]};
            if(mutation=="readonly")return f with {Attributes=f.Attributes|FieldAttributes.InitOnly};
            var access=mutation switch {"private"=>FieldAttributes.Private,"family"=>FieldAttributes.Family,"family-and-assembly"=>FieldAttributes.FamANDAssem,"private-scope"=>FieldAttributes.PrivateScope,_=>FieldAttributes.Assembly};
            return f with {Attributes=(f.Attributes&~FieldAttributes.FieldAccessMask)|access};
        }
        var invalid=program with {Flow=program.Flow with {Flow=program.Flow.Flow with {Flow=flow with {
            Types=flow.Types with {References=flow.Types.References with {Statics=[..flow.Types.References.Statics.Select(Change)]}}}}}};
        if(mutation=="caller-chain")invalid=invalid with {Flow=invalid.Flow with {Bindings=[..invalid.Flow.Bindings.Select(b=>b with {LexicalOwners=[]})]}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(invalid));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;var scalarFlow=objects.Flow;
        var badObjects=objects with {Flow=scalarFlow with {Types=scalarFlow.Types with {Statics=[..scalarFlow.Types.Statics.Select(Change)]}}};
        if(mutation=="caller-chain")badObjects=badObjects with {Bindings=[..badObjects.Bindings.Select(b=>b with {LexicalOwners=[]})]};
        var badScalar=scalar with {Flow=scalar.Flow with {Flow=boxes with {Flow=boxes.Flow with {Flow=badObjects}}}};
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(badScalar));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Internal_access_does_not_bypass_readonly_or_declaring_type_visibility(bool write)
    {
        var b=new ResolutionAssemblyBuilder(new("StaticOwnership",new Version(1,0,0,0)));
        var outer=b.Type("Test","Outer");
        b.Type("","Holder",parent:outer,attributes:write?TypeAttributes.NestedPublic:TypeAttributes.NestedPrivate);
        var field=b.FieldWithAttributes("Value",FieldAttributes.Assembly|FieldAttributes.Static|(write?FieldAttributes.InitOnly:0),6,8);
        b.Type("Test","Unrelated");var root=b.Method("Run",[0,0,8],emit:il=>{
            if(write){il.LoadConstantI4(1);il.OpCode(ILOpCode.Stsfld);il.Token(field);}
            il.OpCode(ILOpCode.Ldsfld);il.Token(field);il.OpCode(ILOpCode.Ret);
        });
        using var files=new ResolverFiles();using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image=resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(resolver,image,root,library);
        Assert.False(result.Success);Assert.NotEmpty(result.Diagnostics);
    }

}
