using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ClosedBindingReuseTests
{
    [Fact]
    public void Repeated_operands_reuse_resolution_without_merging_closed_callers()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var builder = new ResolutionAssemblyBuilder(new("BindingReuse", new Version(1,0,0,0)));
        var obj = builder.TypeReference("System", "Object", builder.Reference(library.Identity));
        var target = builder.Type("Test", "Target`1", baseType: obj, arity: 1);
        var stored = builder.FieldWithAttributes("Stored", FieldAttributes.Public | FieldAttributes.Static, 6, 8);
        // Unused members make repeated name-based binding consume meaningful work.
        for (int i = 0; i < 64; i++) builder.Method("Unused" + i, [0,0,8]);
        builder.Method("Read", [0,0,8], emit: il => { il.OpCode(ILOpCode.Ldsfld); il.Token(stored); il.OpCode(ILOpCode.Ret); });
        var caller = builder.Type("Test", "Caller`1", baseType: obj, arity: 1);
        var openTarget = builder.Specification(0x15, 0x12, checked((byte)(MetadataTokens.GetRowNumber(target) << 2)), 1, 0x13, 0);
        var call = builder.Member(openTarget, "Read", 0,0,8);
        var field = builder.Member(openTarget, "Stored", 6,8);
        builder.Method("Run", [0,0,8], emit: il => {
            for (int i = 0; i < 512; i++)
            {
                il.Call(call); il.OpCode(ILOpCode.Pop);
                il.OpCode(ILOpCode.Ldsfld); il.Token(field); il.OpCode(ILOpCode.Pop);
            }
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        builder.Type("Test", "Root", baseType: obj);
        var integers = builder.Specification(0x15,0x12, checked((byte)(MetadataTokens.GetRowNumber(caller) << 2)),1,8);
        var wides = builder.Specification(0x15,0x12, checked((byte)(MetadataTokens.GetRowNumber(caller) << 2)),1,10);
        var first = builder.Member(integers, "Run",0,0,8); var second = builder.Member(wides,"Run",0,0,8);
        var root = builder.Method("Run", [0,0,8], emit: il => { il.Call(first); il.Call(second); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ValueProgramDiscovery.ReadIntegerObjects(resolver, input, root, library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Input!;
        var readTargets = program.Methods.Where(m => m.Input.Source.Name.Contains("::Read", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, readTargets.Length); Assert.Equal(2, readTargets.Select(m => m.Binding.Method.Specialization).Distinct().Count());
        var owners = program.Methods.SelectMany(m => m.StaticFields.Values).Select(f => f.Id.Owner).Distinct().ToArray();
        Assert.Equal(2, owners.Length); Assert.Equal(2, owners.Select(o => o.Specialization).Distinct().Count());
        Assert.Equal(1026, program.Methods.Sum(m => m.Calls.Count));
    }
}
