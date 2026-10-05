using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NarrowFieldTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Field_stores_without_conversion_opcodes_match_dotnet(bool optimized)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!,Path.GetDirectoryName(typeof(NarrowFieldBox).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var fixture = resolver.OpenInput(typeof(NarrowFieldBox).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("NarrowFieldIL",new Version(1,0,0,0)));
        var reference = b.Reference(fixture.Identity);
        var instance = b.TypeReference("Cilica.Fixtures.Execution","NarrowFieldBox",reference);
        var statics = b.TypeReference("Cilica.Fixtures.Execution","NarrowFieldStatics",reference);
        var ctor = b.Member(instance,".ctor",0x20,0,1);
        b.Type("Test","Root");
        var cases = new List<(string Name, MethodDefinitionHandle Method,Func<int,int,int> Oracle)>();
        foreach (var (name,code) in new (string,byte)[] { ("SignedByte",4),("Byte",5),("Short",6),("UnsignedShort",7),("Char",3),("Bool",2) })
        foreach (bool isStatic in new[] { false,true })
        {
            string title = name + (isStatic ? "Static" : "Heap");
            var field = b.Member(isStatic ? statics : instance,name,6,code);
            var method = b.Method(title,[0,2,8,8,8],emit: il => {
                if (!isStatic) { il.OpCode(ILOpCode.Newobj); il.Token(ctor); il.OpCode(ILOpCode.Dup); }
                il.LoadArgument(0); il.OpCode(isStatic ? ILOpCode.Stsfld : ILOpCode.Stfld); il.Token(field);
                il.OpCode(isStatic ? ILOpCode.Ldsfld : ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Ret);
            });
            var dynamic = new DynamicMethod(title,typeof(int),[typeof(int)]);
            var oracleIl = dynamic.GetILGenerator();
            var oracleField = (isStatic ? typeof(NarrowFieldStatics) : typeof(NarrowFieldBox)).GetField(name)!;
            if (!isStatic) { oracleIl.Emit(OpCodes.Newobj,typeof(NarrowFieldBox).GetConstructor(Type.EmptyTypes)!); oracleIl.Emit(OpCodes.Dup); }
            oracleIl.Emit(OpCodes.Ldarg_0); oracleIl.Emit(isStatic ? OpCodes.Stsfld : OpCodes.Stfld,oracleField);
            oracleIl.Emit(isStatic ? OpCodes.Ldsfld : OpCodes.Ldfld,oracleField); oracleIl.Emit(OpCodes.Ret);
            var oracle = dynamic.CreateDelegate<Func<int,int>>();
            cases.Add((title,method,(a,_) => oracle(a)));
        }
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var probes = cases.Select(c => {
            var result = ValueTranslator.TranslateIntegerObjects(resolver,input,c.Method,library);
            Assert.True(result.Success,string.Join("; ",result.Diagnostics)); return (c.Name,result.Program!,c.Oracle);
        }).ToArray();
        int[] edges = [0,1,2,127,128,255,256,257,32767,32768,65535,65536,-1,-128,-129,-32768,-32769,int.MinValue,int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes,edges.Select(x => (x,0)).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Nested_value_copies_and_zeroing_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,[("Copy",NarrowFieldProbe.Copy)],typeof(NarrowFieldProbe));
    [Fact]
    public void Field_addresses_do_not_escape_as_full_width_borrows()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!,Path.GetDirectoryName(typeof(NarrowFieldBox).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var fixture = resolver.OpenInput(typeof(NarrowFieldBox).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("NarrowFieldAddress",new Version(1,0,0,0)));
        var owner = b.TypeReference("Cilica.Fixtures.Execution","NarrowFieldBox",b.Reference(fixture.Identity));
        var ctor = b.Member(owner,".ctor",0x20,0,1); var field = b.Member(owner,"Byte",6,5);
        b.Type("Test","Root");
        var root = b.Method("Run",[0,0,8],emit:il => {
            il.OpCode(ILOpCode.Newobj); il.Token(ctor); il.OpCode(ILOpCode.Ldflda); il.Token(field);
            il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d => d.Message.Contains("storage-width-aware",StringComparison.Ordinal));
    }
}
