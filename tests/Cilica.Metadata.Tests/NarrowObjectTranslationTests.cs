using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NarrowObjectTranslationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Narrow_constructor_parameters_generics_loops_and_faults_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [(nameof(NarrowObjects.Composed), NarrowObjects.Composed), (nameof(NarrowObjects.Checked), NarrowObjects.Checked)], typeof(NarrowObjects));

    [Fact]
    public void Narrow_borrows_remain_explicitly_unsupported()
    {
        var result = IntegerObjectTranslationTests.Translate(nameof(NarrowObjects.Borrow), fixture: typeof(NarrowObjects));
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("storage-width-aware", StringComparison.Ordinal));
        Assert.False(IntegerObjectTranslationTests.Translate(nameof(NarrowObjects.Composed), enabled: false, fixture: typeof(NarrowObjects)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Narrow_parameter_and_local_addresses_cannot_become_Int32_borrows(bool parameter)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("NarrowAddresses", new Version(1, 0, 0, 0))); b.Type("Probe", "Narrow");
        var helper = b.Method("Address", [0, 1, 8, 5], locals: [7, 1, 5], emit: il => {
            if (parameter) il.LoadArgumentAddress(0); else il.LoadLocalAddress(0);
            il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var entry = b.Method("Run", [0, 0, 8], emit: il => { il.LoadConstantI4(0); il.Call(helper); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("narrow.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, entry, library);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("width-preserving", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Authored_CIL_storage_boundaries_normalize_without_conversion_opcodes(bool optimized)
    {
        (byte Code, Type Type)[] kinds = [(4, typeof(sbyte)), (5, typeof(byte)), (6, typeof(short)),
            (7, typeof(ushort)), (3, typeof(char)), (2, typeof(bool))];
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("NarrowObjectIL", new Version(1, 0, 0, 0))); b.Type("Probe", "Narrow");
        var cases = new List<(string Name, MethodDefinitionHandle Root, Func<int, int, int> Oracle)>();
        foreach (var (code, type) in kinds) foreach (string boundary in new[] { "Parameter", "Return", "Local" })
        {
            bool parameter = boundary == "Parameter", local = boundary == "Local";
            string name = type.Name + boundary;
            var helper = b.Method(name, [0, 1, parameter || local ? (byte)8 : code, parameter ? code : (byte)8],
                locals: local ? [7, 1, code] : null, emit: il => {
                    il.LoadArgument(0); if (local) { il.StoreLocal(0); il.LoadLocal(0); } il.OpCode(ILOpCode.Ret); });
            var root = b.Method("Run" + name, [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.Call(helper); il.OpCode(ILOpCode.Ret); });
            var callee = new DynamicMethod(name, parameter || local ? typeof(int) : type, [parameter ? type : typeof(int)]);
            var body = callee.GetILGenerator(); body.Emit(OpCodes.Ldarg_0);
            if (local) { body.DeclareLocal(type); body.Emit(OpCodes.Stloc_0); body.Emit(OpCodes.Ldloc_0); }
            body.Emit(OpCodes.Ret);
            var caller = new DynamicMethod("Caller" + name, typeof(int), [typeof(int)]);
            var il = caller.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, callee); il.Emit(OpCodes.Ret);
            var oracle = caller.CreateDelegate<Func<int, int>>(); cases.Add((name, root, (a, _) => oracle(a)));
        }
        var input = resolver.OpenInput(files.Write("narrow.dll", b.Build())).Value!;
        var programs = cases.Select(probe => {
            var result = ValueTranslator.TranslateIntegerObjects(resolver, input, probe.Root, library);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return (probe.Name, result.Program!, probe.Oracle);
        }).ToArray();
        int[] edges = [0, 1, 2, 127, 128, 255, 256, 257, 32767, 32768, 65535, 65536, -1, -128, -129, -32768, -32769, int.MinValue, int.MaxValue];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, programs, edges.Select(a => (a, 0)).ToArray());
    }
}
