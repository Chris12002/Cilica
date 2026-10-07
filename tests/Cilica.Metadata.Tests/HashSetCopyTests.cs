using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HashSetCopyTests
{
    private static readonly string[] Names = ["Whole", "Offset", "Prefix", "Faults", "OffsetFaults", "Covariance", "ValuesAndCallbacks", "NullAndIdentity", "Cleared", "AddRange", "Construction"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(HashSetCopyProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Set_copy_overloads_and_collection_composition_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(HashSetCopyProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Count_capacity_and_negative_precedence_match_dotnet(bool optimized)
    {
        var program = (await Programs.Value)[3];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Faults", program, HashSetCopyProbe.Faults)],
            (from index in new[] { -1,0,1,4,5,6,int.MinValue,int.MaxValue } from count in new[] { -1,0,1,2,3,5,6,int.MinValue,int.MaxValue } select (index,count)).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Direct_null_receiver_preserves_overload_validation_order(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new AssemblyIdentity("DirectNullSetCopy", new Version(1,0,0,0)));
        var core = b.Reference(implementation.Identity); var obj = b.TypeReference("System","Object",core);
        var integer = b.TypeReference("System","Int32",core); var set = b.TypeReference("System.Collections.Generic","HashSet`1",core);
        var faults = new[] { "ArgumentNullException", "ArgumentOutOfRangeException", "ArgumentException", "NullReferenceException" }.Select(n => b.TypeReference("System",n,core)).ToArray();
        var closed = b.Specification(0x15,0x12,checked((byte)((MetadataTokens.GetRowNumber(set)<<2)|1)),1,8);
        b.Type("Example","DirectNullSetCopy",baseType:obj);
        var cases = new List<(string Name, MethodDefinitionHandle Method, Func<int,int,int> Oracle)>();
        foreach (var (arity, nullArray) in new[] { (1,true), (2,true), (3,true), (3,false) })
        {
            string name = "Copy" + arity + nullArray;
            byte[] signature = [0x20,(byte)arity,1,0x1d,0x13,0,..Enumerable.Repeat((byte)8,arity-1)];
            var call = b.Member(closed,"CopyTo",signature);
            var method = b.Method(name,[0,2,8,8,8],locals:[7,1,8],emit: il => {
                var start = il.DefineLabel(); var handlers = Enumerable.Range(0,4).Select(_ => il.DefineLabel()).ToArray(); var done = il.DefineLabel();
                il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull);
                if (nullArray) il.OpCode(ILOpCode.Ldnull); else { il.LoadConstantI4(2); il.OpCode(ILOpCode.Newarr); il.Token(integer); }
                if (arity >= 2) il.LoadArgument(0); if (arity == 3) il.LoadArgument(1); il.Call(call);
                il.LoadConstantI4(-100); il.StoreLocal(0); il.Branch(ILOpCode.Leave,done);
                for (int i = 0; i < 4; i++) {
                    il.MarkLabel(handlers[i]); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(11 + i*2); il.StoreLocal(0); il.Branch(ILOpCode.Leave,done);
                }
                il.MarkLabel(done); il.LoadLocal(0); il.OpCode(ILOpCode.Ret);
                for (int i = 0; i < 4; i++) il.ControlFlowBuilder!.AddCatchRegion(start,handlers[0],handlers[i],i == 3 ? done : handlers[i+1],faults[i]);
            });
            var dynamic = new DynamicMethod(name,typeof(void),[typeof(int),typeof(int)]); var di = dynamic.GetILGenerator();
            di.Emit(OpCodes.Ldnull); if (nullArray) di.Emit(OpCodes.Ldnull); else { di.Emit(OpCodes.Ldc_I4_2); di.Emit(OpCodes.Newarr,typeof(int)); }
            if (arity >= 2) di.Emit(OpCodes.Ldarg_0); if (arity == 3) di.Emit(OpCodes.Ldarg_1);
            Type[] parameters = [typeof(int[]),..Enumerable.Repeat(typeof(int),arity-1)];
            di.Emit(OpCodes.Call,typeof(HashSet<int>).GetMethod("CopyTo",parameters)!); di.Emit(OpCodes.Ret);
            var oracle = dynamic.CreateDelegate<Action<int,int>>();
            int Run(int a,int c) {
                try { oracle(a,c); return -100; }
                catch (ArgumentNullException) { return 11; }
                catch (ArgumentOutOfRangeException) { return 13; }
                catch (ArgumentException) { return 15; }
                catch (NullReferenceException) { return 17; }
            }
            cases.Add((name,method,Run));
        }
        var input = resolver.OpenInput(files.Write("direct-null-copy.dll",b.Build())).Value!;
        var probes = cases.Select(c => {
            var result = ValueTranslator.TranslateIntegerObjects(scope,input,c.Method,implementation);
            Assert.True(result.Success,string.Join("; ",result.Diagnostics)); return (c.Name,result.Program!,c.Oracle);
        }).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes);
    }

}
