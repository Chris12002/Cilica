using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class StackReceiverTests
{
    private static readonly string[] Names = ["Fingerprint", "Duplicate", "ReturnedAndNull", "RepeatedCopies"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(BuildPrograms);
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Duplicated_receivers_preserve_identity_null_checks_and_fault_order(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(StackReceiverProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("wrong-slot")] [InlineData("branch-entry")] [InlineData("scan-limit")] [InlineData("invalid-indirection")]
    public async Task Stack_candidates_cannot_cross_identity_control_flow_or_scan_bounds(string mode)
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var b = new ResolutionAssemblyBuilder(new("StackReceiverBoundary", new Version(1,0,0,0)));
        var reference = b.Reference(implementation.Identity);
        var obj = b.TypeReference("System", "Object", reference);
        var builder = b.TypeReference("System.Text", "StringBuilder", reference);
        var objectConstructor = b.Member(obj, ".ctor", 0x20, 0, 1);
        var builderConstructor = b.Member(builder, ".ctor", 0x20, 0, 1);
        var toString = b.Member(obj, "ToString", 0x20, 0, 0x0e);
        b.Type("Test", "Caller", baseType: obj);
        var consume = b.Method("Consume", [0, 1, 1, 0x12, checked((byte)((MetadataTokens.GetRowNumber(builder) << 2) | 1))], emit: il => il.OpCode(ILOpCode.Ret));
        var root = b.Method("Run", [0,0,8], emit: il => {
            if (mode == "wrong-slot") { il.OpCode(ILOpCode.Newobj); il.Token(objectConstructor); }
            il.OpCode(ILOpCode.Newobj); il.Token(builderConstructor);
            il.OpCode(ILOpCode.Dup); il.Call(consume);
            if (mode == "wrong-slot") il.OpCode(ILOpCode.Pop);
            if (mode == "invalid-indirection") il.OpCode(ILOpCode.Ldind_ref);
            if (mode == "scan-limit") for (int i = 0; i < 260; ++i) il.OpCode(ILOpCode.Nop);
            if (mode == "branch-entry") { var join = il.DefineLabel(); il.Branch(ILOpCode.Br, join); il.MarkLabel(join); }
            il.OpCode(ILOpCode.Callvirt); il.Token(toString); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("stack-boundary.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, root, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("no managed IL body", StringComparison.Ordinal));
    }
    private static async Task<ManagedExceptionIrProgram[]> BuildPrograms()
    {
        using var files = new ResolverFiles(); var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var bytes = await File.ReadAllBytesAsync(typeof(StackReceiverProbe).Assembly.Location, TestContext.Current.CancellationToken);
        // This is an authored test assembly, never a frozen application input.
        // Replace local-zero storage with the equivalent retained-stack form.
        // The ordinary authored C# remains the independently executed .NET oracle.
        using (var pe = new PEReader(new MemoryStream(bytes)))
        {
            var reader = pe.GetMetadataReader();
            using var inspected = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
            var original = inspected.OpenInput(typeof(StackReceiverProbe).Assembly.Location).Value!;
            foreach (string name in new[] { "FingerprintText", "Duplicated", "Returned", "Repeated" })
            {
                var token = typeof(StackReceiverProbe).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.MetadataToken;
                var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(token);
                var input = inspected.ReadMethodInput(original, handle, new()).Value!;
                Assert.Empty(input.Instructions.SelectMany(i => i.Targets));
                var store = Assert.Single(input.Instructions.Where(i => i.OpCode == "stloc.0"));
                var loads = input.Instructions.Where(i => i.OpCode == "ldloc.0").ToArray();
                Assert.True(loads.Length >= 2);
                Assert.Equal("callvirt", input.Instructions[input.Instructions.IndexOf(loads[^1]) + 1].OpCode);
                Assert.True(store.Offset < loads[0].Offset);
                var rva = reader.GetMethodDefinition(handle).RelativeVirtualAddress;
                var section = pe.PEHeaders.SectionHeaders.Single(h => rva >= h.VirtualAddress && rva < h.VirtualAddress + h.VirtualSize);
                int header = section.PointerToRawData + rva - section.VirtualAddress;
                Assert.Equal(3, bytes[header] & 3); // Fat header with the retained original local signature.
                int code = header + (bytes[header + 1] >> 4) * 4;
                bytes[header + 2] = 8; bytes[header + 3] = 0;
                bytes[code + store.Offset] = (byte)ILOpCode.Nop;
                foreach (var load in loads) bytes[code + load.Offset] = (byte)ILOpCode.Dup;
                bytes[code + loads[^1].Offset] = (byte)ILOpCode.Nop;
            }
        }
        var inputAssembly = scope.OpenInput(files.Write("stack-fixture.dll", bytes)).Value!;
        return Names.Select(name => {
            var result = ValueTranslator.TranslateIntegerObjects(scope, inputAssembly,
                (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StackReceiverProbe).GetMethod(name)!.MetadataToken), implementation);
            Assert.True(result.Success, name + ": " + string.Join("; ", result.Diagnostics));
            return result.Program!;
        }).ToArray();
    }
}
