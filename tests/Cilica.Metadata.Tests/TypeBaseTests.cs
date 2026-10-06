using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class TypeBaseTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Direct_base_reads_agree_with_structural_metadata_and_charge_each_read(bool generic)
    {
        var b = new ResolutionAssemblyBuilder(new("BaseFacts", new Version(1, 0, 0, 0)));
        var parent = b.Type("Test", "Parent", arity: generic ? 1 : 0);
        EntityHandle basis = parent;
        if (generic) { var signature = new BlobBuilder(); signature.WriteByte(0x15); signature.WriteByte(0x12); signature.WriteCompressedInteger(MetadataTokens.GetRowNumber(parent) << 2); signature.WriteByte(1); signature.WriteByte(8); basis = b.Specification(signature.ToArray()); }
        var handle = b.Type("Test", "Child", baseType: basis);
        b.Field("Value", 6, 8);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var type = resolver.ResolveType(image, handle).Value!;
        var budget = new MemberResolutionBudget(2);
        var first = resolver.ReadTypeBase(type, budget); var second = resolver.ReadTypeBase(type, budget);
        Assert.True(first.Success); Assert.Equal(first.Value, second.Value); Assert.Equal(0, budget.Remaining);
        Assert.Equal(resolver.ReadTypeInput(image, handle).Value!.BaseType, first.Value!.BaseType);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadTypeBase(type, budget).Diagnostics).Id);
    }
    [Fact]
    public void Base_only_reads_do_not_validate_unused_fields_and_keep_input_ownership()
    {
        var b = new ResolutionAssemblyBuilder(new("BaseOnly", new Version(1, 0, 0, 0)));
        var handle = b.Type("Test", "Owner"); b.Field("Malformed", 0);
        using var files = new ResolverFiles(); using var first = files.Open(); using var second = files.Open();
        var image = first.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var type = first.ResolveType(image, handle).Value!;
        var read = first.ReadTypeBase(type, new(1)); Assert.True(read.Success); Assert.Null(read.Value!.BaseType);
        Assert.False(first.ReadTypeInput(image, handle).Success);
        var budget = new MemberResolutionBudget(1);
        Assert.Throws<ArgumentException>(() => second.ReadTypeBase(type, budget)); Assert.Equal(1, budget.Remaining);
        first.Dispose(); Assert.Throws<ObjectDisposedException>(() => first.ReadTypeBase(type, new(1)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Malformed_or_excessively_nested_base_signatures_remain_diagnostic(bool deep)
    {
        var b = new ResolutionAssemblyBuilder(new("BadBase", new Version(1, 0, 0, 0)));
        var signature = deep ? Enumerable.Repeat((byte)0x1d, 65).Append((byte)8).ToArray() : new byte[] { 0 };
        var handle = b.Type("Test", "Owner", baseType: b.Specification(signature));
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = resolver.ReadTypeBase(resolver.ResolveType(image, handle).Value!, new(1));
        Assert.False(result.Success); Assert.Single(result.Diagnostics);
    }
    [Theory]
    [InlineData("MulticastDelegate", false)] [InlineData("MulticastDelegate", true)]
    [InlineData("Exception", false)] [InlineData("Exception", true)]
    public void Actual_delegate_and_exception_candidates_still_require_full_structural_validation(string baseName, bool malformed)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("BadCandidate", new Version(1, 0, 0, 0)));
        var parent = b.TypeReference("System", baseName, b.Reference(library.Identity));
        b.Type("Test", "Candidate", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        b.Field("Forbidden", malformed ? new byte[] { 0 } : new byte[] { 6, 8 });
        var called = b.Method("Echo", [0, 1, 8, 8], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Root");
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.Call(called); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(malformed ? "FIELD signature" : "fieldless", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(64, false, true)] [InlineData(65, false, false)] [InlineData(1, true, false)]
    public void Exception_ancestry_keeps_its_cycle_and_depth_bounds(int count, bool cycle, bool success)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("AncestryBound", new Version(1, 0, 0, 0)));
        TypeDefinitionHandle parent = cycle ? MetadataTokens.TypeDefinitionHandle(2) : default;
        for (int i = 0; i < count; i++) parent = b.Type("Test", "Owner" + i, baseType: parent);
        var called = b.Method("Echo", [0, 1, 8, 8], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Root");
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.Call(called); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.True(result.Success == success, string.Join("; ", result.Diagnostics));
        if (!success) Assert.Contains(result.Diagnostics, d => d.Message.Contains("Cyclic or excessive exception class ancestry", StringComparison.Ordinal));
    }
    [Fact]
    public void A_foreign_multicast_delegate_name_does_not_select_runtime_semantics()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("MarkerIdentity", new Version(1, 0, 0, 0)));
        var marker = b.Type("System", "MulticastDelegate");
        b.Type("Test", "Candidate", baseType: marker);
        var called = b.Method("Echo", [0, 1, 8, 8], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Root");
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.Call(called); il.OpCode(ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("selected runtime MulticastDelegate", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Static_owner_classification_does_not_load_unused_layouts(bool optimized)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ManyStaticOwners", new Version(1, 0, 0, 0)));
        var parent = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var methods = new List<MethodDefinitionHandle>();
        for (int i = 0; i < 257; i++)
        {
            b.Type("Test", "Owner" + i, baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            b.Field("UnusedMalformedField", 0);
            methods.Add(b.Method("Echo", [0, 1, 8, 8], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); }));
        }
        methods.Add(methods[^1]); b.Type("Test", "Root", baseType: parent);
        var groups = new List<MethodDefinitionHandle>();
        foreach (var chunk in methods.Chunk(26))
            groups.Add(b.Method("Group" + groups.Count, [0, 1, 8, 8], emit: il => {
                il.LoadConstantI4(0);
                foreach (var method in chunk) { il.LoadArgument(0); il.Call(method); il.OpCode(ILOpCode.Add); }
                il.OpCode(ILOpCode.Ret);
            }));
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(1);
            foreach (var group in groups) { il.LoadArgument(0); il.Call(group); il.OpCode(ILOpCode.Add); }
            il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("ManyStaticOwners", result.Program!, (a, c) => unchecked(a * 258 + c))]);
    }
}
