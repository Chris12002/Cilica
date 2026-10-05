using System;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class MemberNameIndexTests
{
    [Fact]
    public void Repeated_member_searches_share_names_but_keep_closed_signatures_and_fresh_budgets()
    {
        using var files = new ResolverFiles();
        var b = new ResolutionAssemblyBuilder(new("Indexed", new Version(1,0,0,0)));
        var owner = b.Type("Test", "Box`1", arity: 1);
        for (int i = 0; i < 64; i++) { b.Method("Unused" + i, [0,0,8]); b.Field("Unused" + i, 6,8); }
        var method = b.Method("Read", [0,0,0x13,0]);
        var field = b.Field("Value", 6,0x13,0);
        byte token = checked((byte)(MetadataTokens.GetRowNumber(owner) << 2));
        var first = b.Specification(0x15,0x12,token,1,8);
        var second = b.Specification(0x15,0x12,token,1,10);
        var methods = new[] { b.Member(first,"Read",0,0,0x13,0), b.Member(second,"Read",0,0,0x13,0) };
        var fields = new[] { b.Member(first,"Value",6,0x13,0), b.Member(second,"Value",6,0x13,0) };
        using var resolver = files.Open("refs");
        var input = resolver.OpenInput(files.Write("input.dll",b.Build())).Value!;
        var budget = new MemberResolutionBudget(1500);
        for (int i = 0; i < 200; i++)
        {
            var m = resolver.ResolveDeclaredMethod(input, methods[i % 2], budget);
            var f = resolver.ResolveDeclaredField(input, fields[i % 2], budget);
            Assert.True(m.Success, string.Join("; ",m.Diagnostics));
            Assert.True(f.Success, string.Join("; ",f.Diagnostics));
            Assert.Equal(method,m.Value!.Handle); Assert.Equal(field,f.Value!.Handle);
            var expected = new TypeSignature.Primitive(i % 2 == 0 ? System.Reflection.Metadata.PrimitiveTypeCode.Int32 : System.Reflection.Metadata.PrimitiveTypeCode.Int64);
            Assert.Equal(expected,m.Value.Signature.ReturnType); Assert.Equal(expected,f.Value.Signature);
        }
        // A different caller-owned budget pays for its own scan.
        Assert.Contains(resolver.ResolveDeclaredMethod(input,methods[0],new(10)).Diagnostics,d => d.Id == DiagnosticIds.TranslationLimit);
        Assert.Contains(resolver.ResolveDeclaredField(input,fields[0],new(10)).Diagnostics,d => d.Id == DiagnosticIds.TranslationLimit);
        // Partial failed scans never become reusable entries.
        var shortBudget = new MemberResolutionBudget(10);
        Assert.False(resolver.ResolveDeclaredMethod(input,methods[0],shortBudget).Success);
        Assert.False(resolver.ResolveDeclaredMethod(input,methods[0],shortBudget).Success);
    }
}
