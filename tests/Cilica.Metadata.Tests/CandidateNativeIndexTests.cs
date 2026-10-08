using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class CandidateNativeIndexTests
{
    private static readonly string[] Names=["SignedLoad","SignedStore","UnsignedLoad","UnsignedStore","ReferenceStore","ValueAddress","FillTable"];
    private static readonly ConcurrentDictionary<string,Lazy<Task<ManagedExceptionIrProgram[]>>> Programs=new();
    private static async Task<ManagedExceptionIrProgram> Program(string name)=>(await Programs.GetOrAdd(name,n=>new(()=>CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateNativeIndexProbe),[n]))).Value)[0];
    public static IEnumerable<object[]> Cases()=>from name in Names from optimized in new[]{false,true} select new object[]{name,optimized};
    [Theory] [MemberData(nameof(Cases))]
    public async Task Checked_native_indices_preserve_width_fault_order_and_roots(string name,bool optimized)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[(name,await Program(name),typeof(CandidateNativeIndexProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())],
            (from a in Enumerable.Range(0,16) from b in new[]{-1,0,1,2} select(a,b)).ToArray());
    }
    [Theory] [InlineData("type")] [InlineData("operand")] [InlineData("fault")]
    public async Task Both_ir_stages_reject_malformed_checked_index_packets(string mutation)
    {
        var program=await Program("SignedLoad");var flow=program.Flow.Flow.Flow;
        System.Collections.Immutable.ImmutableArray<IrMethod> Alter(System.Collections.Immutable.ImmutableArray<IrMethod> methods)=>
            [..methods.Select(m=>m with{Blocks=[..m.Blocks.Select(b=>b with{Statements=[..b.Statements.Select(s=>s is IrStatement.Define{Value.Expression:IrExpression.CheckedNativeIndex index} d?mutation=="fault"?d with{Exceptional=null}:d with{Value=mutation=="type"?d.Value with{Type=IrType.Int64}:d.Value with{Expression=index with{Operand=d.Value.Id}}}:s)]})]})];
        Assert.Contains(flow.Methods.SelectMany(m=>m.Blocks).SelectMany(b=>b.Values),v=>v.Expression is IrExpression.CheckedNativeIndex);
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with{Flow=program.Flow with{Flow=program.Flow.Flow with{Flow=flow with{Methods=Alter(flow.Methods)}}}}));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with{Flow=scalar.Flow with{Flow=boxes with{Flow=boxes.Flow with{Flow=objects with{Flow=objects.Flow with{Methods=Alter(objects.Flow.Methods)}}}}}}));
    }
    [Theory] [InlineData("pop")] [InlineData("dup")] [InlineData("convert")] [InlineData("branch")] [InlineData("newarr")]
    public async Task Native_index_results_cannot_escape_into_general_integer_flow(string use)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var builder=new ResolutionAssemblyBuilder(new("IndexEscape",new Version(1,0,0,0)));
        var integer=builder.TypeReference("System","Int32",builder.Reference(implementation.Identity));builder.Type("Test","Root");
        var root=builder.Method("Run",[0,2,8,8,8],emit:il=>{
            il.LoadArgument(0);il.OpCode(ILOpCode.Conv_ovf_i);
            if(use=="branch"){var label=il.DefineLabel();il.Branch(ILOpCode.Br_s,label);il.MarkLabel(label);il.OpCode(ILOpCode.Conv_i4);}
            else if(use=="newarr"){il.OpCode(ILOpCode.Newarr);il.Token(integer);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(0);}
            else if(use=="pop"){il.OpCode(ILOpCode.Pop);il.LoadConstantI4(0);}
            else if(use=="dup"){il.OpCode(ILOpCode.Dup);il.OpCode(ILOpCode.Pop);il.OpCode(ILOpCode.Conv_i4);}
            else il.OpCode(ILOpCode.Conv_i4);
            il.OpCode(ILOpCode.Ret);
        });
        var input=scope.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL2001"&&d.Message.Contains("Checked native indices",StringComparison.Ordinal));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Direct_32bit_signed_and_unsigned_native_conversions_match_dotnet(bool optimized)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var builder=new ResolutionAssemblyBuilder(new("NativeIndex32",new Version(1,0,0,0)));var core=builder.Reference(implementation.Identity);
        var integer=builder.TypeReference("System","Int32",core);var overflow=builder.TypeReference("System","OverflowException",core);var range=builder.TypeReference("System","IndexOutOfRangeException",core);builder.Type("Test","Root");
        MethodDefinitionHandle Root(bool unsigned)=>builder.Method(unsigned?"Unsigned":"Signed",[0,2,8,8,8],locals:[7,1,8],emit:il=>{
            var start=il.DefineLabel();var overflowHandler=il.DefineLabel();var rangeHandler=il.DefineLabel();var done=il.DefineLabel();
            il.MarkLabel(start);il.LoadConstantI4(2);il.OpCode(ILOpCode.Newarr);il.Token(integer);il.LoadArgument(0);il.OpCode(unsigned?ILOpCode.Conv_ovf_i_un:ILOpCode.Conv_ovf_i);il.OpCode(ILOpCode.Ldelem_i4);il.StoreLocal(0);il.Branch(ILOpCode.Leave,done);
            il.MarkLabel(overflowHandler);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(11);il.StoreLocal(0);il.Branch(ILOpCode.Leave,done);
            il.MarkLabel(rangeHandler);il.OpCode(ILOpCode.Pop);il.LoadConstantI4(12);il.StoreLocal(0);il.Branch(ILOpCode.Leave,done);
            il.MarkLabel(done);il.LoadLocal(0);il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start,overflowHandler,overflowHandler,rangeHandler,overflow);il.ControlFlowBuilder.AddCatchRegion(start,overflowHandler,rangeHandler,done,range);
        });
        var signed=Root(false);var unsignedRoot=Root(true);var input=scope.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        Func<int,int,int> Oracle(bool unsigned)
        {
            var method=new DynamicMethod("IndexOracle",typeof(int),[typeof(int),typeof(int)]);var il=method.GetILGenerator();var result=il.DeclareLocal(typeof(int));
            il.BeginExceptionBlock();il.Emit(OpCodes.Ldc_I4_2);il.Emit(OpCodes.Newarr,typeof(int));il.Emit(OpCodes.Ldarg_0);il.Emit(unsigned?OpCodes.Conv_Ovf_I_Un:OpCodes.Conv_Ovf_I);il.Emit(OpCodes.Ldelem_I4);il.Emit(OpCodes.Stloc,result);
            il.BeginCatchBlock(typeof(OverflowException));il.Emit(OpCodes.Pop);il.Emit(OpCodes.Ldc_I4,11);il.Emit(OpCodes.Stloc,result);
            il.BeginCatchBlock(typeof(IndexOutOfRangeException));il.Emit(OpCodes.Pop);il.Emit(OpCodes.Ldc_I4,12);il.Emit(OpCodes.Stloc,result);
            il.EndExceptionBlock();il.Emit(OpCodes.Ldloc,result);il.Emit(OpCodes.Ret);return method.CreateDelegate<Func<int,int,int>>();
        }
        ManagedExceptionIrProgram Translate(MethodDefinitionHandle root){var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);Assert.True(result.Success,string.Join("; ",result.Diagnostics));return result.Program!;}
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("Signed32",Translate(signed),Oracle(false)),("Unsigned32",Translate(unsignedRoot),Oracle(true))],new[]{int.MinValue,-1,0,1,2,int.MaxValue}.Select(a=>(a,0)).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Hash_algorithm_owner_static_root_is_independently_mapped(bool optimized)
    {
        var program=(await CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateHashAlgorithmOwner),["StaticRoot"]))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,[("HashOwner",program,CandidateHashAlgorithmOwner.StaticRoot)],expectedStaticRoots:1);
    }
    [Fact]
    public async Task Native_integer_locals_remain_outside_the_profile()
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CandidateNativeIndexProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateNativeIndexProbe).GetMethod("NativeLocal")!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Id=="CIL2002");
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Emulated_32bit_target_preserves_conversion_before_store_faults(bool optimized)
    {
        // Exercise target-width selection on this 64-bit host; this is not a 32-bit ABI run.
        long[] edges=[0,1,2,-1,int.MinValue,int.MaxValue,(long)int.MaxValue+1,uint.MaxValue,1L<<32,(1L<<32)+1,long.MaxValue,long.MinValue,-4294967295L,3,255,4095];
        int Oracle(int a,int b,bool unsigned)
        {
            int order=1;int[]? array=b<0?null:new int[2];
            try {order=12;long wide=edges[a];int index=unsigned?checked((int)unchecked((ulong)wide)):checked((int)wide);order=123;if(b==0)throw new ArgumentException();array![index]=b;return order*100;}
            catch(OverflowException){return order*100+1;}catch(NullReferenceException){return order*100+2;}catch(IndexOutOfRangeException){return order*100+3;}catch(ArgumentException){return order*100+4;}
        }
        var source=new System.Text.StringBuilder();var calls=new System.Text.StringBuilder();
        foreach(var name in new[]{"SignedStore","UnsignedStore"})
        {
            var program=await Program(name);var flow=program.Flow.Flow.Flow;var artifact=ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
            Assert.Contains("sizeof(std::intptr_t) * 8u",artifact.Text,StringComparison.Ordinal);
            source.Append(artifact.Text.Replace("sizeof(std::intptr_t) * 8u","32u",StringComparison.Ordinal));
            source.Append(System.Globalization.CultureInfo.InvariantCulture,$$"""
                #include <cassert>
                [[gnu::noinline]] static void run_{{name}}(int a,int b,int expected) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap;heap.collect_before_allocation=true;Context c(heap);
                    LocationArgument args[2];args[0].bits=std::bit_cast<std::uint32_t>(a);args[1].bits=std::bit_cast<std::uint32_t>(b);
                    for(int repeat=0;repeat<3;repeat++) {
                        assert(cilica_m_{{flow.Entry.ContentHash}}_{{flow.Entry.Token:x8}}(c,args)==expected);
                        assert(c.pending==0&&c.exception==nullptr&&c.top==nullptr&&c.used==0&&c.borrows==nullptr&&c.depth==0);
                        assert(collect(c).status==MarkStatus::complete);
                    }
                }
                """);
            for(int a=0;a<16;a++)foreach(int b in new[]{-1,0,1,2})calls.Append(System.Globalization.CultureInfo.InvariantCulture,$"run_{name}({a},{b},{Oracle(a,b,name=="UnsignedStore")});\n");
        }
        source.Append("int main(){\n").Append(calls).Append("}\n");
        using var files=new ResolverFiles();string directory=files.DirectoryPath("native-index-width"),cpp=Path.Combine(directory,"index.cpp"),exe=Path.Combine(directory,"index");
        await File.WriteAllTextAsync(cpp,source.ToString(),TestContext.Current.CancellationToken);
        var build=await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX")??"clang++",["-std=c++20","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror",optimized?"-O2":"-O0","-Wframe-larger-than=2048","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],directory);
        Assert.True(build.ExitCode==0,build.Stderr);var run=await NativeProcess.Run(exe,[],directory);Assert.True(run.ExitCode==0,run.Stdout+run.Stderr);Assert.Empty(run.Stderr);
    }

}
