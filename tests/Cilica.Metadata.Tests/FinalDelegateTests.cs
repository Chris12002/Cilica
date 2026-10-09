using System;
using System.Linq;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class FinalDelegateTests
{
    private static readonly string[] Names = ["Set","Composed","FinalMember","Hidden","List","Null","ValueSet"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(FinalDelegateProbe), Names));
    [Theory] [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)] [InlineData(false,2)] [InlineData(true,2)]
    public async Task Final_virtual_captures_keep_receiver_identity_and_null_fault_order(bool optimized,int group)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(FinalDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*3).Take(3).ToArray());
    }
    [Theory] [InlineData("Overridable")] [InlineData("ThroughInterface")] [InlineData("Sealed")]
    public async Task Runtime_virtual_and_interface_selection_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=resolver.OpenInput(typeof(FinalDelegateProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(FinalDelegateProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success); Assert.Null(result.Program);
    }
    [Theory] [InlineData("lookup")] [InlineData("offset")] [InlineData("final")]
    public async Task Both_ir_stages_require_exact_final_lookup_facts(string mutation)
    {
        var program=(await Programs.Value)[2]; var flow=program.Flow.Flow.Flow;
        var capture=flow.Methods.SelectMany(m=>m.Blocks).SelectMany(b=>b.Values).Select(v=>v.Expression).OfType<IrExpression.NewDelegate>().Single();
        Assert.NotNull(capture.VirtualLookupOffset);
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods)=>[..methods.Select(m=>m with {Blocks=[..m.Blocks.Select(b=>b with {Statements=[..b.Statements.Select(s=>s is IrStatement.Define {Value.Expression: IrExpression.NewDelegate d} v ? v with {Value=v.Value with {Expression=d with {VirtualLookupOffset=mutation=="lookup"?null:d.VirtualLookupOffset+1}}}:s)]})]})];
        ImmutableArray<ObjectMethodBinding> Bindings(ImmutableArray<ObjectMethodBinding> bindings)=>[..bindings.Select(b=>b.Method==capture.Target?b with {Attributes=b.Attributes & ~MethodAttributes.Final}:b)];
        var invalid=program with {Flow=program.Flow with {Bindings=mutation=="final"?Bindings(program.Flow.Bindings):program.Flow.Bindings,Flow=new(flow with {Methods=mutation=="final"?flow.Methods:Alter(flow.Methods)})}};
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(invalid));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        var invalidScalar=scalar with {Flow=scalar.Flow with {Flow=boxes with {Flow=boxes.Flow with {Flow=objects with {Bindings=mutation=="final"?Bindings(objects.Bindings):objects.Bindings,Flow=objects.Flow with {Methods=mutation=="final"?objects.Flow.Methods:Alter(objects.Flow.Methods)}}}}}};
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory] [InlineData("no-dup")] [InlineData("intervening")] [InlineData("branch")]
    [InlineData("static")] [InlineData("nonfinal")]
    public void Raw_final_delegate_patterns_never_escape_the_adjacent_contract(string kind)
    {
        var runtime = typeof(object).Assembly.GetName(); var b = new ResolutionAssemblyBuilder(new("FinalDelegatePattern",new Version(1,0,0,0)));
        var reference = b.Reference(new(runtime.Name!,runtime.Version!,publicKeyToken:Convert.ToHexStringLower(runtime.GetPublicKeyToken()!)));
        var obj = b.TypeReference("System","Object",reference); var multicast = b.TypeReference("System","MulticastDelegate",reference);
        b.Type("Test","Callable",baseType:multicast,attributes:TypeAttributes.Public|TypeAttributes.Sealed);
        var ctor=b.Method(".ctor",[0x20,2,1,0x1c,0x18],attributes:MethodAttributes.Public|MethodAttributes.HideBySig|MethodAttributes.SpecialName|MethodAttributes.RTSpecialName);
        b.Method("Invoke",[0x20,1,8,8],attributes:MethodAttributes.Public|MethodAttributes.HideBySig|MethodAttributes.NewSlot|MethodAttributes.Virtual);
        b.Type("Test","Target",baseType:obj);
        var flags=MethodAttributes.Public|MethodAttributes.HideBySig|MethodAttributes.Virtual|MethodAttributes.NewSlot|MethodAttributes.Final;
        if(kind=="nonfinal") flags &= ~MethodAttributes.Final;
        if(kind=="static") flags=MethodAttributes.Public|MethodAttributes.Static;
        var target=b.Method("Read",[kind=="static"?(byte)0:(byte)0x20,1,8,8],attributes:flags,emit:il=> {il.OpCode(kind=="static"?ILOpCode.Ldarg_0:ILOpCode.Ldarg_1);il.OpCode(ILOpCode.Ret);});
        b.Type("Test","Caller"); var root=b.Method("Run",[0,0,8],emit:il=> {
            il.OpCode(ILOpCode.Ldnull); if(kind!="no-dup") il.OpCode(ILOpCode.Dup);
            if(kind=="intervening") il.OpCode(ILOpCode.Nop);
            il.OpCode(ILOpCode.Ldvirtftn); il.Token(target);
            if(kind=="branch") {var label=il.DefineLabel();il.Branch(ILOpCode.Br_s,label);il.MarkLabel(label);}
            il.OpCode(ILOpCode.Newobj);il.Token(ctor);il.OpCode(ILOpCode.Pop);il.OpCode(ILOpCode.Ldc_i4_0);il.OpCode(ILOpCode.Ret);
        });
        using var files=new ResolverFiles();using var resolver=AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image=resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;var library=resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(resolver,image,root,library);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
