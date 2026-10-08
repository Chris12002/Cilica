using System;
using System.Linq;
using System.IO;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateUInt32VectorTests
{
    private static readonly string[] Names=["Values","Generic","Widen","Heap","Types","Aliases","Interfaces","Copy","Search","Enumerate","Boxes","ActualBoxes","Empty","Static","Clear","Faults","CastMessage","RequestedBoxes","AllocationFaults","NullLoad"];
    private static readonly ConcurrentDictionary<string,Lazy<Task<ManagedExceptionIrProgram[]>>> Programs=new();
    private static async Task<ManagedExceptionIrProgram> Program(string name)=>(await Programs.GetOrAdd(name,n=>new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateUInt32VectorProbe),[n]))).Value)[0];
    public static IEnumerable<object[]> Cases()=>from name in Names from optimized in new[]{false,true} select new object[]{name,optimized};
    [Theory] [MemberData(nameof(Cases))]
    public async Task Unsigned_vectors_match_dotnet(string name,bool optimized)
    {
        int[] edges=[int.MinValue,-1,0,1,255,65535,int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,await Program(name),typeof(CandidateUInt32VectorProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())],
            (from a in edges from b in new[]{-1,0,1,2,int.MaxValue} select(a,b)).ToArray());
    }
    [Theory] [InlineData("UnsupportedBorrow","Element addresses")] [InlineData("WideIndex","conv.ovf.i")]
    public async Task Remaining_crc_index_and_borrow_boundaries_are_explicit(string name,string diagnostic)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CandidateUInt32VectorProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateUInt32VectorProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(diagnostic,StringComparison.Ordinal));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Model_rejects_wrong_unsigned_identity_or_profile(bool wrongProfile)
    {
        var model=(await Program("Values")).Flow.Flow.Flow.Types.References;
        var array=model.Arrays.Single(a=>a.ArrayElement?.Kind==ObjectArrayElementKind.UInt32);
        var invalid=wrongProfile?model with{Integer64=false}:model with{Arrays=model.Arrays.Replace(array,ObjectTypeId.Vector(model.ObjectBase,new(ObjectArrayElementKind.UInt32,model.ObjectBase)))};
        Assert.Throws<ArgumentException>(()=>ObjectModelValidator.Validate(invalid));
    }
    [Theory] [InlineData("element")] [InlineData("fault")]
    public async Task Both_stages_reject_wrong_storage_and_fault_packets(string mutation)
    {
        var program=await Program("Values");var flow=program.Flow.Flow.Flow;
        System.Collections.Immutable.ImmutableArray<IrMethod> Alter(System.Collections.Immutable.ImmutableArray<IrMethod> methods)=>
            [..methods.Select(m=>m with{Blocks=[..m.Blocks.Select(b=>b with{Statements=[..b.Statements.Select(s=>s is IrStatement.StoreElement store?mutation=="element"?store with{ArrayType=ObjectTypeId.Vector(flow.Types.References.ObjectBase,ObjectArrayElement.Byte)}:store with{Exceptional=(IrExceptionTarget)99}:s)]})]})];
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with{Flow=program.Flow with{Flow=program.Flow.Flow with{Flow=flow with{Methods=Alter(flow.Methods)}}}}));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with{Flow=scalar.Flow with{Flow=boxes with{Flow=boxes.Flow with{Flow=objects with{Flow=objects.Flow with{Methods=Alter(objects.Flow.Methods)}}}}}}));
    }
    [Fact]
    public async Task Generic_integer_helper_cannot_change_requested_box_identity()
    {
        var plan=ValueScalarLowering.LowerExceptions(await Program("Interfaces")).Flow.Flow.Flow.Flow.InterfaceDispatch!;
        var method=plan.Methods.First(m=>m.ArrayHelper&&m.Name=="Get"&&m.Parameters[0].Class?.ArrayElement?.Kind==ObjectArrayElementKind.UInt32);
        var changed=method with{Parameters=method.Parameters.SetItem(0,IrType.Reference(ObjectTypeId.Vector(plan.Types.ObjectBase,ObjectArrayElement.Int32)))};
        var failure=Assert.Throws<ArgumentException>(()=>ObjectInterfacePlanValidator.Validate(plan with{Methods=plan.Methods.Replace(method,changed)}));
        Assert.Contains("requested signed/unsigned element identity",failure.Message,StringComparison.Ordinal);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Raw_signed_unsigned_and_typed_loads_preserve_all_bits(bool optimized)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var builder=new ResolutionAssemblyBuilder(new("UnsignedLoads",new Version(1,0,0,0)));
        var unsigned=builder.TypeReference("System","UInt32",builder.Reference(implementation.Identity));builder.Type("Test","Root");
        var kinds=new[]{ILOpCode.Ldelem_i4,ILOpCode.Ldelem_u4,ILOpCode.Ldelem};
        var roots=kinds.Select((kind,n)=>builder.Method("Load"+n,[0,2,8,8,8],emit:il=>{
            il.LoadConstantI4(1);il.OpCode(ILOpCode.Newarr);il.Token(unsigned);il.OpCode(ILOpCode.Dup);il.LoadConstantI4(0);il.LoadArgument(0);il.OpCode(ILOpCode.Stelem_i4);
            il.LoadConstantI4(0);il.OpCode(kind);if(kind==ILOpCode.Ldelem)il.Token(unsigned);il.OpCode(ILOpCode.Conv_u8);il.LoadConstantI4(31);il.OpCode(ILOpCode.Shr_un);il.OpCode(ILOpCode.Conv_i4);il.OpCode(ILOpCode.Ret);
        })).ToArray();
        var input=scope.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        Func<int,int,int> Oracle(ILOpCode kind)
        {
            var method=new DynamicMethod("UnsignedOracle",typeof(int),[typeof(int),typeof(int)]);var il=method.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Newarr,typeof(uint));il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Stelem_I4);il.Emit(OpCodes.Ldc_I4_0);
            if(kind==ILOpCode.Ldelem)il.Emit(OpCodes.Ldelem,typeof(uint));else il.Emit(kind==ILOpCode.Ldelem_i4?OpCodes.Ldelem_I4:OpCodes.Ldelem_U4);
            il.Emit(OpCodes.Conv_U8);il.Emit(OpCodes.Ldc_I4,31);il.Emit(OpCodes.Shr_Un);il.Emit(OpCodes.Conv_I4);il.Emit(OpCodes.Ret);return method.CreateDelegate<Func<int,int,int>>();
        }
        var probes=roots.Select((root,i)=>{
            var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);Assert.True(result.Success,string.Join("; ",result.Diagnostics));return("Load"+i,result.Program!,Oracle(kinds[i]));
        }).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes,new[]{int.MinValue,int.MinValue+1,-1,0,1,int.MaxValue}.Select(a=>(a,0)).ToArray());
    }

}
