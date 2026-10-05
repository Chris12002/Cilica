using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NarrowScalarTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Narrow_calls_locals_returns_and_loops_match_dotnet(bool optimized)
    {
        var probes = new Dictionary<string, Func<int, int, int>>
        {
            [nameof(NarrowScalars.SignedByte)] = NarrowScalars.SignedByte,
            [nameof(NarrowScalars.UnsignedByte)] = NarrowScalars.UnsignedByte,
            [nameof(NarrowScalars.SignedShort)] = NarrowScalars.SignedShort,
            [nameof(NarrowScalars.UnsignedShort)] = NarrowScalars.UnsignedShort,
            [nameof(NarrowScalars.Character)] = NarrowScalars.Character,
            [nameof(NarrowScalars.Boolean)] = NarrowScalars.Boolean,
            [nameof(NarrowScalars.CheckedReturn)] = NarrowScalars.CheckedReturn,
        };
        using var resolver = AssemblyResolver.Create([]).Value!;
        var input = resolver.OpenInput(typeof(NarrowScalars).Assembly.Location).Value!;
        var programs = probes.Keys.Select(name =>
        {
            var root = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(NarrowScalars).GetMethod(name)!.MetadataToken);
            var result = IntegerTranslator.TranslateProgram(resolver, input, root);
            Assert.True(result.Success, name + ": " + string.Join("; ", result.Diagnostics)); return result.Program!;
        }).ToArray();
        await NativeInteger64Tests.Verify(programs, probes.Values.Select(p => new Func<ulong, ulong, ulong>((a, b) => unchecked((uint)p((int)a, (int)b)))).ToArray(), optimized);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Narrow_local_storage_truncates_and_extends_without_explicit_conv(bool optimized)
    {
        (byte Code, Type Type)[] kinds = [(4, typeof(sbyte)), (5, typeof(byte)), (6, typeof(short)),
            (7, typeof(ushort)), (3, typeof(char)), (2, typeof(bool))];
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("NarrowStores", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var roots = new List<MethodDefinitionHandle>(); var oracles = new List<Func<ulong, ulong, ulong>>();
        foreach (var (code, type) in kinds)
        {
            roots.Add(b.Method(type.Name, [0, 1, 8, 8], locals: [7, 1, code], emit: il =>
            { il.LoadArgument(0); il.StoreLocal(0); il.LoadLocal(0); il.OpCode(ILOpCode.Ret); }));
            var dynamic = new System.Reflection.Emit.DynamicMethod(type.Name, typeof(int), [typeof(int)]);
            var il = dynamic.GetILGenerator(); il.DeclareLocal(type);
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Stloc_0);
            il.Emit(System.Reflection.Emit.OpCodes.Ldloc_0); il.Emit(System.Reflection.Emit.OpCodes.Ret);
            var oracle = dynamic.CreateDelegate<Func<int, int>>(); oracles.Add((a, _) => unchecked((uint)oracle((int)a)));
        }
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var programs = roots.Select(root =>
        {
            var result = IntegerTranslator.TranslateProgram(resolver, input, root);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
        }).ToArray();
        ulong[] edges = [0, 1, 2, 127, 128, 129, 255, 256, 257, 32767, 32768, 32769, 65535, 65536, 65537,
            0xd800, 0xdfff, 0x7fffffff, 0x80000000, 0xffff7fff, 0xffff8000, 0xffffff7f, 0xffffff80, 0xffffffff];
        await NativeInteger64Tests.Verify(programs, oracles, optimized, edges.Select(a => (a, 0UL)).ToArray());
    }

    [Fact]
    public async Task Narrow_call_boundaries_match_authored_IL_without_caller_conversions()
    {
        (byte Code, Type Type)[] kinds = [(4, typeof(sbyte)), (5, typeof(byte)), (6, typeof(short)),
            (7, typeof(ushort)), (3, typeof(char)), (2, typeof(bool))];
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("NarrowCalls", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var roots = new List<MethodDefinitionHandle>(); var oracles = new List<Func<ulong, ulong, ulong>>();
        foreach (var (code, type) in kinds) foreach (bool parameter in new[] { false, true })
        {
            var helper = b.Method(type.Name + parameter, [0, 1, parameter ? (byte)8 : code, parameter ? code : (byte)8],
                emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
            roots.Add(b.Method("Run" + type.Name + parameter, [0, 1, 8, 8], emit: il =>
            { il.LoadArgument(0); il.Call(helper); il.OpCode(ILOpCode.Ret); }));
            var callee = new System.Reflection.Emit.DynamicMethod("Callee", parameter ? typeof(int) : type, [parameter ? type : typeof(int)]);
            var ci = callee.GetILGenerator(); ci.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); ci.Emit(System.Reflection.Emit.OpCodes.Ret);
            var caller = new System.Reflection.Emit.DynamicMethod("Caller", typeof(int), [typeof(int)]);
            var il = caller.GetILGenerator(); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Call, callee); il.Emit(System.Reflection.Emit.OpCodes.Ret);
            var run = caller.CreateDelegate<Func<int, int>>(); oracles.Add((a, _) => unchecked((uint)run((int)a)));
        }
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var programs = roots.Select(root => { var result = IntegerTranslator.TranslateProgram(resolver, input, root);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!; }).ToArray();
        await NativeInteger64Tests.Verify(programs, oracles, optimized: true,
            new ulong[] { 0, 2, 127, 128, 255, 256, 32767, 32768, 65535, 65536, 0xffffff80, 0xffffffff }.Select(a => (a, 0UL)).ToArray());
    }

    [Fact]
    public void Narrow_signatures_do_not_expand_the_legacy_Int32_profile()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("NarrowLegacy", new Version(1, 0, 0, 0))); b.Type("", "Probe");
        var root = b.Method("Run", [0, 1, 2, 2], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        Assert.True(IntegerTranslator.TranslateProgram(resolver, input, root).Success);
        Assert.Equal(DiagnosticIds.ExecutionSignatureUnsupported, Assert.Single(Int32Translator.TranslateProgram(resolver, input, root).Diagnostics).Id);
    }
}
