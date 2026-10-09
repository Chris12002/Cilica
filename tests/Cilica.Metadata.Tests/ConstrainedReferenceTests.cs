using System;
using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ConstrainedReferenceTests
{
    private static readonly string[] Names=["Class","Virtual","Interface","Locations","NullOrder","ValueAndReference","HeapReplacement","Replacement","NullToObject","ObjectToNull"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs=new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(ConstrainedReferenceProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    [InlineData(false,4)] [InlineData(true,4)]
    public async Task Closed_reference_calls_preserve_live_receivers_dispatch_faults_and_rooting(bool optimized,int group)
    {
        var programs=await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],typeof(ConstrainedReferenceProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("reference-stack")] [InlineData("wrong-prefix")] [InlineData("branch-call")] [InlineData("tail")]
    public async Task Malformed_reference_locations_and_prefix_entry_are_rejected(string shape)
    {
        using var files=new ResolverFiles();var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var fixture=scope.OpenInput(typeof(ConstrainedReferenceProbe).Assembly.Location).Value!;
        var b=new ResolutionAssemblyBuilder(new("MalformedReferencePrefix",new Version(1,0,0,0)));
        var source=b.Reference(fixture.Identity);var owner=b.TypeReference("Cilica.Fixtures.Execution","ReferenceCallBase",source);
        var derived=b.TypeReference("Cilica.Fixtures.Execution","ReferenceCallDerived",source);
        var contract=b.TypeReference("Cilica.Fixtures.Execution","IReferenceCall",source);
        var obj=b.TypeReference("System","Object",b.Reference(implementation.Identity));
        var call=b.Member(contract,"Call",0x20,1,8,8);
        b.Type("Test","Program",baseType:obj);
        var root=b.Method("Run",[0,2,8,8,8],locals:[7,1,0x12,checked((byte)((MetadataTokens.GetRowNumber(owner)<<2)|1))],emit:il=>{
            if(shape=="reference-stack")il.LoadLocal(0);else il.LoadLocalAddress(0);
            il.LoadArgument(0);var target=il.DefineLabel();
            if(shape=="branch-call")il.Branch(ILOpCode.Br_s,target);
            if(shape=="tail")il.OpCode(ILOpCode.Tail);
            il.OpCode(ILOpCode.Constrained);il.Token(shape=="wrong-prefix"?derived:owner);
            il.MarkLabel(target);il.OpCode(ILOpCode.Callvirt);il.Token(call);il.OpCode(ILOpCode.Ret);
        });
        var input=scope.OpenInput(files.Write("malformed.dll",b.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(shape is "reference-stack" or "wrong-prefix"?"exact managed receiver location":shape=="branch-call"?"not the start of an instruction":"prefix",StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public async Task Reference_constrained_calls_do_not_allocate_receiver_boxes()
    {
        foreach(var program in await Programs.Value)
        {
            Assert.DoesNotContain(program.Flow.Flow.Flow.Methods.SelectMany(m=>m.Blocks).SelectMany(b=>b.Values),v=>v.Expression is IrExpression.BoxValue);
            ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
        }
    }
}
