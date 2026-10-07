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
public sealed class NarrowBorrowTests
{
    private static readonly string[] Names = ["Aliases", "Fault", "Scoped"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(NarrowBorrowProbe), Names));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Aliasing_exception_cleanup_and_scoped_borrows_match_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(NarrowBorrowProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).ToArray());
    }
    [Theory]
    [InlineData("wide")] [InlineData("invalid")] [InlineData("sign")] [InlineData("scalar")]
    public async Task Both_IR_stages_reject_forged_widths(string damage)
    {
        var program = (await Programs.Value)[0]; var scalar = ValueScalarLowering.LowerExceptions(program);
        var objects = scalar.Flow.Flow.Flow.Flow;
        IrStatement Corrupt(IrStatement statement) => statement is IrStatement.Define d
            && d.Value.Expression is IrExpression.AddressLocal && d.Value.Type.Narrow is not null
            ? d with { Value = d.Value with { Type = damage switch {
                "wide" => d.Value.Type with { Narrow = null },
                "invalid" => d.Value.Type with { Narrow = IntegerCastTarget.Int32 },
                "sign" => d.Value.Type with { Narrow = IntegerCastTarget.UInt16 },
                _ => IrType.Int32 with { Narrow = IntegerCastTarget.Int16 }
            } } } : statement;
        var logical = program.Flow.Flow.Flow;
        logical = logical with { Methods = [.. logical.Methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(Corrupt)] })] })] };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { Flow = new(logical) } }));
        objects = objects with { Flow = objects.Flow with { Methods = [.. objects.Flow.Methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(Corrupt)] })] })] } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = objects } } } }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Width_preserving_reborrows_match_authored_IL(bool optimized)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("NarrowBorrowIL", new Version(1,0,0,0))); b.Type("Test", "Root");
        var cases = new List<(string Name, MethodDefinitionHandle Method, Func<int,int,int> Oracle)>();
        foreach (var (code, type, store, load, dotStore, dotLoad) in new[] {
            ((byte)2, typeof(bool), ILOpCode.Stind_i1, ILOpCode.Ldind_u1, OpCodes.Stind_I1, OpCodes.Ldind_U1),
            ((byte)4, typeof(sbyte), ILOpCode.Stind_i1, ILOpCode.Ldind_i1, OpCodes.Stind_I1, OpCodes.Ldind_I1),
            ((byte)5, typeof(byte), ILOpCode.Stind_i1, ILOpCode.Ldind_u1, OpCodes.Stind_I1, OpCodes.Ldind_U1),
            ((byte)6, typeof(short), ILOpCode.Stind_i2, ILOpCode.Ldind_i2, OpCodes.Stind_I2, OpCodes.Ldind_I2),
            ((byte)7, typeof(ushort), ILOpCode.Stind_i2, ILOpCode.Ldind_u2, OpCodes.Stind_I2, OpCodes.Ldind_U2),
            ((byte)3, typeof(char), ILOpCode.Stind_i2, ILOpCode.Ldind_u2, OpCodes.Stind_I2, OpCodes.Ldind_U2) })
        {
            var leaf = b.Method(type.Name + "Leaf", [0,2,8,0x10,code,8], emit: il => {
                il.LoadArgument(0); il.LoadArgument(1); il.OpCode(store);
                il.LoadArgument(0); il.OpCode(load); il.OpCode(ILOpCode.Ret);
            });
            var forward = b.Method(type.Name + "Forward", [0,2,8,0x10,code,8], emit: il => {
                il.LoadArgument(0); il.LoadArgument(1); il.Call(leaf); il.OpCode(ILOpCode.Ret);
            });
            var dl = new DynamicMethod("Leaf",typeof(int),[type.MakeByRefType(),typeof(int)]);
            var li = dl.GetILGenerator(); li.Emit(OpCodes.Ldarg_0); li.Emit(OpCodes.Ldarg_1); li.Emit(dotStore);
            li.Emit(OpCodes.Ldarg_0); li.Emit(dotLoad); li.Emit(OpCodes.Ret);
            var df = new DynamicMethod("Forward",typeof(int),[type.MakeByRefType(),typeof(int)]);
            var fi = df.GetILGenerator(); fi.Emit(OpCodes.Ldarg_0); fi.Emit(OpCodes.Ldarg_1); fi.Emit(OpCodes.Call,dl); fi.Emit(OpCodes.Ret);
            foreach (bool parameter in new[] { false, true })
            {
                var helper = b.Method(type.Name + parameter + "Body", [0,2,8,parameter ? code : (byte)8,8], locals: [7,1,code], emit: il => {
                    if (parameter) { il.OpCode(ILOpCode.Ldarga_s); il.CodeBuilder.WriteByte(0); }
                    else il.LoadLocalAddress(0);
                    il.LoadArgument(1); il.Call(forward);
                    if (parameter) il.LoadArgument(0); else il.LoadLocal(0);
                    il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
                });
                var root = b.Method(type.Name + parameter, [0,2,8,8,8], emit: il => {
                    il.LoadArgument(0); il.LoadArgument(1); il.Call(helper); il.OpCode(ILOpCode.Ret);
                });
                var dh = new DynamicMethod("Body",typeof(int),[parameter ? type : typeof(int),typeof(int)]);
                var hi = dh.GetILGenerator(); hi.DeclareLocal(type);
                if (parameter) hi.Emit(OpCodes.Ldarga_S,(byte)0); else hi.Emit(OpCodes.Ldloca_S,(byte)0);
                hi.Emit(OpCodes.Ldarg_1); hi.Emit(OpCodes.Call,df);
                hi.Emit(parameter ? OpCodes.Ldarg_0 : OpCodes.Ldloc_0); hi.Emit(OpCodes.Add); hi.Emit(OpCodes.Ret);
                var dr = new DynamicMethod("Root",typeof(int),[typeof(int),typeof(int)]);
                var ri = dr.GetILGenerator(); ri.Emit(OpCodes.Ldarg_0); ri.Emit(OpCodes.Ldarg_1); ri.Emit(OpCodes.Call,dh); ri.Emit(OpCodes.Ret);
                cases.Add((type.Name + parameter,root,dr.CreateDelegate<Func<int,int,int>>()));
            }
        }
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var probes = cases.Select(c => {
            var result = ValueTranslator.TranslateIntegerObjects(resolver,input,c.Method,library);
            Assert.True(result.Success,string.Join("; ",result.Diagnostics)); return (c.Name,result.Program!,c.Oracle);
        }).ToArray();
        int[] edges = [0,1,2,127,128,255,256,257,32767,32768,65535,65536,-1,-128,-129,-32768,-32769,int.MinValue,int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,probes,edges.Select(x => (x,x)).ToArray());
    }

    [Theory]
    [InlineData(2, ILOpCode.Stind_i4)] [InlineData(2, ILOpCode.Stind_i2)]
    [InlineData(8, ILOpCode.Stind_i1)] [InlineData(6, ILOpCode.Stind_i1)]
    public void Mismatched_indirect_width_is_rejected(byte code, ILOpCode store)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("WrongNarrowBorrow",new Version(1,0,0,0))); b.Type("Test","Root");
        var root = b.Method("Run",[0,2,8,8,8],locals:[7,1,code],emit: il => {
            il.LoadLocalAddress(0); il.LoadArgument(0); il.OpCode(store); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver,input,root,library);
        Assert.False(result.Success);
    }
}
