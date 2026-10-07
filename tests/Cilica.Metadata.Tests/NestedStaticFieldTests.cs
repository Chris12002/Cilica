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
public sealed class NestedStaticFieldTests
{
    private static readonly string[] Names=["Numbers","References","Values","Readonly","Deep","Fault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(NestedStaticFieldProbe),Names));
    [Fact]
    public async Task Nested_static_probes_translate()=>Assert.Equal(Names.Length,(await Programs.Value).Length);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)] [InlineData(false,5)] [InlineData(true,5)]
    public async Task Nested_private_statics_match_dotnet(bool optimized,int probe)
    {
        var p=(await Programs.Value)[probe];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [(Names[probe],p,typeof(NestedStaticFieldProbe).GetMethod(Names[probe])!.CreateDelegate<Func<int,int,int>>())],
            persistentRoots:probe==5,expectedStaticRoots:probe==3?1:0);
    }
    [Theory]
    [InlineData("family")] [InlineData("family-and-assembly")]
    [InlineData("private-scope")] [InlineData("target-chain")] [InlineData("caller-chain")] [InlineData("readonly")]
    public async Task Both_ir_stages_require_access_facts_and_readonly_ownership(string mutation)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        ObjectStaticField Change(ObjectStaticField f)
        {
            if(mutation=="target-chain")return f with {LexicalOwners=[]};
            if(mutation=="readonly")return f with {Attributes=f.Attributes|FieldAttributes.InitOnly};
            var access=mutation switch {"family"=>FieldAttributes.Family,"family-and-assembly"=>FieldAttributes.FamANDAssem,"private-scope"=>FieldAttributes.PrivateScope,_=>FieldAttributes.Private};
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
    [InlineData("nested",true,false)] [InlineData("sibling",false,false)]
    [InlineData("unrelated",false,false)] [InlineData("parent",false,false)]
    [InlineData("nested",false,true)]
    public void Private_static_access_is_directional_and_profile_scoped(string relation,bool expected,bool legacy)
    {
        var b=new ResolutionAssemblyBuilder(new("PrivateStaticDirection",new Version(1,0,0,0)));
        var outer=b.Type("Test","Outer"); MethodDefinitionHandle root;
        if(relation=="parent")
        {
            root=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldsfld);il.Token(MetadataTokens.FieldDefinitionHandle(1));il.OpCode(ILOpCode.Ret);});
            b.Type("","Inner",parent:outer);b.FieldWithAttributes("Value",FieldAttributes.Private|FieldAttributes.Static,6,8);
        }
        else
        {
            if(relation=="sibling")b.Type("","Left",parent:outer);
            var field=b.FieldWithAttributes("Value",FieldAttributes.Private|FieldAttributes.Static,6,8);
            b.Type("Test","Caller",parent:relation=="unrelated"?default:outer);
            root=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldsfld);il.Token(field);il.OpCode(ILOpCode.Ret);});
        }
        using var files=new ResolverFiles();using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input=resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result=legacy?ValueTranslator.TranslateExceptions(resolver,input,root,library):ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
        Assert.True(result.Success==expected,string.Join("; ",result.Diagnostics));
        if(!expected)Assert.Contains(result.Diagnostics,d=>d.Message.Contains(legacy ? "Instruction 'ldsfld'" : "Static field access",StringComparison.Ordinal));
    }
    [Fact]
    public void Matching_foreign_lexical_tokens_do_not_grant_private_field_access()
    {
        using var files=new ResolverFiles();
        var identity=new AssemblyIdentity("ForeignPrivateStatic",new Version(1,0,0,0));var target=new ResolutionAssemblyBuilder(identity);
        target.Type("Test","Outer");target.FieldWithAttributes("Value",FieldAttributes.Private|FieldAttributes.Static,6,8);
        files.Write("ForeignPrivateStatic.dll",target.Build());
        var b=new ResolutionAssemblyBuilder(new("ForeignPrivateStaticCaller",new Version(1,0,0,0)));
        var foreign=b.TypeReference("Test","Outer",b.Reference(identity));var field=b.Member(foreign,"Value",6,8);
        var outer=b.Type("Test","Outer");b.Type("","Reader",parent:outer);
        var root=b.Method("Run",[0,0,8],emit:il=>{il.OpCode(ILOpCode.Ldsfld);il.Token(field);il.OpCode(ILOpCode.Ret);});
        using var resolver=AssemblyResolver.Create([files.DirectoryPath("refs"),Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input=resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains("Static field access",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UnsupportedGeneric","Static field access")]
    [InlineData("UnsupportedAddress","ldsflda")]
    public void Generic_owner_and_static_address_boundaries_remain_diagnostic(string name,string diagnostic)
    {
        var result=IntegerObjectTranslationTests.Translate(name,fixture:typeof(NestedStaticFieldProbe));
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(diagnostic,StringComparison.Ordinal));
    }
}
