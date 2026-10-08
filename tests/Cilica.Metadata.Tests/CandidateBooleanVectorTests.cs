using System;
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
public sealed class CandidateBooleanVectorTests
{
    private static readonly string[] Names = ["Values","Generic","Heap","Empty","Types","CastMessages","FaultOrder","Metadata","Static","AllocationFaults"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(CandidateBooleanVectorProbe),Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Boolean_vector_values_identity_roots_metadata_and_faults_match_dotnet(bool optimized,int group)
    {
        var programs = await Programs.Value;
        int[] edges=[int.MinValue,-257,-256,-1,0,1,2,127,128,255,256,257,65535,65536,int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,Names.Select((name,i)=>(name,programs[i],
            typeof(CandidateBooleanVectorProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*3).Take(3).ToArray(),
            (from a in edges from b in new[]{-1,0,1,2,int.MaxValue} select(a,b)).ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Raw_boolean_stores_preserve_the_low_byte_without_canonicalizing(bool optimized)
    {
        using var files=new ResolverFiles();
        var (built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var builder=new ResolutionAssemblyBuilder(new("BooleanStoreIL",new Version(1,0,0,0)));
        var boolean=builder.TypeReference("System","Boolean",builder.Reference(implementation.Identity)); builder.Type("Test","Root");
        MethodDefinitionHandle Root(bool typed) => builder.Method(typed ? "Typed" : "Byte",[0,2,8,8,8],emit:il=>{
            il.LoadConstantI4(1);il.OpCode(ILOpCode.Newarr);il.Token(boolean);il.OpCode(ILOpCode.Dup);
            il.LoadConstantI4(0);il.LoadArgument(0);il.OpCode(typed?ILOpCode.Stelem:ILOpCode.Stelem_i1);if(typed)il.Token(boolean);
            il.LoadConstantI4(0);il.OpCode(typed?ILOpCode.Ldelem:ILOpCode.Ldelem_u1);if(typed)il.Token(boolean);
            il.OpCode(ILOpCode.Ret);
        });
        var byteRoot=Root(false);var typedRoot=Root(true);
        var input=scope.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        Func<int,int,int> Oracle(bool typed)
        {
            var method=new DynamicMethod("BooleanOracle",typeof(int),[typeof(int),typeof(int)]);var il=method.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Newarr,typeof(bool));il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ldarg_0);
            if(typed)il.Emit(OpCodes.Stelem,typeof(bool));else il.Emit(OpCodes.Stelem_I1);
            il.Emit(OpCodes.Ldc_I4_0);if(typed)il.Emit(OpCodes.Ldelem,typeof(bool));else il.Emit(OpCodes.Ldelem_U1);il.Emit(OpCodes.Ret);
            return method.CreateDelegate<Func<int,int,int>>();
        }
        ManagedExceptionIrProgram Translate(MethodDefinitionHandle method)
        {var result=ValueTranslator.TranslateIntegerObjects(scope,input,method,implementation);Assert.True(result.Success,string.Join("; ",result.Diagnostics));return result.Program!;}
        var pairs=Enumerable.Range(-256,768).Concat(new[]{int.MinValue,int.MaxValue,65535,65536}).Select(a=>(a,0)).ToArray();
        var probes = new[] { ("ByteStore",Translate(byteRoot),Oracle(false)), ("TypedStore",Translate(typedRoot),Oracle(true)) };
        // Keep the generated O0 main within the existing 2 KiB stack-frame cap.
        foreach (var chunk in pairs.Chunk(128))
            await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes,chunk);
    }
    [Theory]
    [InlineData("UnsupportedBorrow")] [InlineData("UnsupportedInterface")] [InlineData("UnsupportedClear")] [InlineData("UnsupportedCopy")]
    public async Task Element_borrows_and_interface_elements_remain_diagnostic(string name)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var input=scope.OpenInput(typeof(CandidateBooleanVectorProbe).Assembly.Location).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,(MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(CandidateBooleanVectorProbe).GetMethod(name)!.MetadataToken),implementation);
        Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Message.Contains(name=="UnsupportedBorrow"?"Element addresses":"Narrow vector interfaces",StringComparison.Ordinal));
    }
    [Theory] [InlineData("element")] [InlineData("fault")]
    public async Task Both_ir_stages_reject_wrong_boolean_identity_and_fault_edges(string mutation)
    {
        var program=(await Programs.Value)[0];var flow=program.Flow.Flow.Flow;
        System.Collections.Immutable.ImmutableArray<IrMethod> Alter(System.Collections.Immutable.ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m=>m with { Blocks=[..m.Blocks.Select(b=>b with { Statements=[..b.Statements.Select(statement=>
                statement is IrStatement.StoreElement store ? mutation=="element" ? store with {
                    ArrayType=ObjectTypeId.Vector(flow.Types.References.ObjectBase,ObjectArrayElement.Byte) } : store with {
                    Exceptional=(IrExceptionTarget)99 } : statement)] })] })];
        Assert.Contains(flow.Methods.SelectMany(m=>m.Blocks).SelectMany(b=>b.Statements),s=>s is IrStatement.StoreElement { ArrayType.ArrayElement.Kind: ObjectArrayElementKind.Boolean });
        Assert.Throws<ArgumentException>(()=>ManagedExceptionIrValidator.Validate(program with {Flow=program.Flow with {
            Flow=program.Flow.Flow with {Flow=flow with {Methods=Alter(flow.Methods)}}}}));
        var scalar=ValueScalarLowering.LowerExceptions(program);var boxes=scalar.Flow.Flow;var objects=boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(()=>ScalarManagedExceptionIrValidator.Validate(scalar with {Flow=scalar.Flow with {
            Flow=boxes with {Flow=boxes.Flow with {Flow=objects with {Flow=objects.Flow with {Methods=Alter(objects.Flow.Methods)}}}}}}));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Boolean_model_requires_exact_scalar_identity_and_integer_profile(bool wrongProfile)
    {
        var model=(await Programs.Value)[0].Flow.Flow.Flow.Types.References;
        var array=model.Arrays.Single(a=>a.ArrayElement?.Kind==ObjectArrayElementKind.Boolean);
        var invalid=wrongProfile ? model with {Integer64=false} : model with {
            Arrays=model.Arrays.Replace(array,ObjectTypeId.Vector(model.ObjectBase,new ObjectArrayElement(ObjectArrayElementKind.Boolean,model.ObjectBase))) };
        Assert.Throws<ArgumentException>(()=>ObjectModelValidator.Validate(invalid));
    }
    [Theory] [InlineData(ILOpCode.Ldelem_i1)] [InlineData(ILOpCode.Ldelem_i4)] [InlineData(ILOpCode.Stelem_i4)]
    public async Task Unadmitted_boolean_element_opcodes_remain_diagnostic(ILOpCode opcode)
    {
        using var files=new ResolverFiles();var(built,_,resolver,implementation,plan)=await CoreLibraryBuildTests.BuildAndMap(files);using var lifetime=resolver;
        using var scope=resolver.CreateTrustedLibraryCompilationScope(plan,await File.ReadAllBytesAsync(built.ManifestPath,TestContext.Current.CancellationToken),built.ManifestSha256).Value!;
        var builder=new ResolutionAssemblyBuilder(new("BooleanWrongWidth",new Version(1,0,0,0)));
        var boolean=builder.TypeReference("System","Boolean",builder.Reference(implementation.Identity));builder.Type("Test","Root");
        var root=builder.Method("Run",[0,2,8,8,8],emit:il=>{
            il.LoadConstantI4(1);il.OpCode(ILOpCode.Newarr);il.Token(boolean);il.LoadConstantI4(0);
            if(opcode==ILOpCode.Stelem_i4)il.LoadArgument(0);il.OpCode(opcode);
            if(opcode==ILOpCode.Stelem_i4)il.LoadConstantI4(0);il.OpCode(ILOpCode.Ret);
        });
        var input=scope.OpenInput(files.Write("input.dll",builder.Build())).Value!;
        var result=ValueTranslator.TranslateIntegerObjects(scope,input,root,implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>opcode==ILOpCode.Ldelem_i1
            ? d.Id=="CIL2001" && d.Message.Contains("ldelem.i1",StringComparison.Ordinal)
            : d.Id=="CIL1013" && d.Message.Contains("storage width/kind",StringComparison.Ordinal));
    }

}
