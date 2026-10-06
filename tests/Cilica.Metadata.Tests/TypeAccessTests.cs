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
public sealed class TypeAccessTests
{
    [Theory]
    [InlineData(TypeAttributes.Public, TypeAttributes.NestedPublic, true)]
    [InlineData(TypeAttributes.NotPublic, TypeAttributes.NestedPublic, false)]
    [InlineData(TypeAttributes.Public, TypeAttributes.NestedPrivate, false)]
    public void Lexical_reads_share_owner_facts_with_structural_reads(TypeAttributes outerFlags, TypeAttributes innerFlags, bool visible)
    {
        var b = new ResolutionAssemblyBuilder(new("AccessFacts", new Version(1, 0, 0, 0)));
        var outer = b.Type("Test", "Outer", arity: 1, attributes: outerFlags);
        var inner = b.Type("", "Inner", parent: outer, arity: 2, attributes: innerFlags);
        b.Field("Value", 6, 8);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var type = resolver.ResolveType(image, inner).Value!;
        var budget = new MemberResolutionBudget(2);
        var access = resolver.ReadTypeAccess(type, budget).Value!;
        var structural = resolver.ReadTypeInput(image, inner).Value!;
        Assert.Equal(0, budget.Remaining); Assert.Equal(visible, access.ExternallyVisible);
        Assert.Equal(structural.Source, access.Source); Assert.Equal(structural.OwnerGenericArity, access.OwnerGenericArity);
        Assert.Equal(structural.ExternallyVisible, access.ExternallyVisible);
        Assert.Equal(structural.LexicalOwners, access.LexicalOwners);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadTypeAccess(type, new(1)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadTypeAccess(type, new(2), 1).Diagnostics).Id);
    }
    [Fact]
    public void Lexical_reads_do_not_admit_unused_malformed_fields_and_keep_resolver_ownership()
    {
        var b = new ResolutionAssemblyBuilder(new("AccessOnly", new Version(1, 0, 0, 0)));
        var handle = b.Type("Test", "Owner"); b.Field("Broken", 0);
        using var files = new ResolverFiles(); using var first = files.Open(); using var second = files.Open();
        var image = first.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var type = first.ResolveType(image, handle).Value!;
        Assert.True(first.ReadTypeAccess(type, new(1)).Success);
        Assert.False(first.ReadTypeInput(image, handle).Success);
        var foreign = new MemberResolutionBudget(1);
        Assert.Throws<ArgumentException>(() => second.ReadTypeAccess(type, foreign)); Assert.Equal(1, foreign.Remaining);
        Assert.Throws<ArgumentOutOfRangeException>(() => first.ReadTypeAccess(type, new(1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => first.ReadTypeAccess(type, new(1), 65));
        first.Dispose(); Assert.Throws<ObjectDisposedException>(() => first.ReadTypeAccess(type, new(1)));
    }
    [Theory]
    [InlineData(64, true)] [InlineData(65, false)]
    public void Owner_depth_is_checked_before_name_formatting(int count, bool success)
    {
        var b = new ResolutionAssemblyBuilder(new("AccessDepth", new Version(1, 0, 0, 0)));
        TypeDefinitionHandle handle = default;
        for (int i = 0; i < count; i++) handle = b.Type(i == 0 ? "Test" : "", "Nested", parent: handle);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var type = resolver.ResolveType(image, handle).Value!;
        var budget = new MemberResolutionBudget(64);
        var result = resolver.ReadTypeAccess(type, budget);
        Assert.Equal(success, result.Success); Assert.Equal(0, budget.Remaining);
        if (!success) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Unused_generic_type_arguments_do_not_consume_runtime_layout_snapshots(bool optimized)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ManyGenericIdentities", new Version(1, 0, 0, 0)));
        b.Type("Test", "Bridge");
        var echo = b.Method("Echo", [0x10, 1, 1, 8, 8], arity: 1, emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var methods = new List<EntityHandle>();
        for (int i = 0; i < 257; i++)
        {
            var type = b.Type("Test", "Argument" + i); b.Field("UnusedMalformedField", 0);
            var signature = new BlobBuilder(); signature.WriteByte(0x0a); signature.WriteByte(1);
            signature.WriteByte(0x12); signature.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) << 2);
            methods.Add(b.Instantiate(echo, signature.ToArray()));
        }
        // The 258th call repeats an argument beyond the bounded 256-entry visibility cache.
        methods.Add(methods[^1]); b.Type("Test", "Root");
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
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("ManyGenericIdentities", result.Program!, (a, c) => unchecked(a * 258 + c))]);
    }
}
