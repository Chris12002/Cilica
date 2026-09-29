using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class RefFieldResolutionTests
{
    [Fact]
    public void Generic_ref_field_matches_open_member_signature_then_substitutes_owner_arguments()
    {
        var builder = new ResolutionAssemblyBuilder(new("RefFields", new Version(1, 0, 0, 0)));
        builder.Type("", "RefBox`1", arity: 1);
        var definition = builder.Field("Value", 6, 0x10, 0x13, 0); // FIELD BYREF VAR 0
        var owner = builder.Specification(0x15, 0x11, 8, 1, 8); // RefBox<int32>
        var reference = builder.Member(owner, "Value", 6, 0x10, 0x13, 0);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("refs.dll", builder.Build())).Value!;
        var result = resolver.ResolveField(assembly, reference);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(definition, result.Value!.Handle);
        Assert.Equal(new TypeSignature.ByReference(new TypeSignature.Primitive(PrimitiveTypeCode.Int32)), result.Value.Signature);
        Assert.Equal(new TypeSignature.ByReference(new TypeSignature.Variable(false, 0)), resolver.ResolveField(assembly, definition).Value!.Signature);
    }

    [Theory]
    [InlineData(new byte[] { 6, 0x10, 1 })] // ref void
    [InlineData(new byte[] { 6, 0x10, 0x10, 8 })] // ref ref int
    [InlineData(new byte[] { 6, 0x0f, 0x10, 8 })] // pointer to ref int
    [InlineData(new byte[] { 6, 0x1d, 0x10, 8 })] // array of ref int
    [InlineData(new byte[] { 6, 0x45, 0x10, 8 })] // pinned field
    public void Enabling_ref_fields_does_not_allow_invalid_nested_forms(byte[] signature)
    {
        var builder = new ResolutionAssemblyBuilder(new("RefFields", new Version(1, 0, 0, 0)));
        builder.Type("", "RefBox"); var field = builder.Field("Value", signature);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("refs.dll", builder.Build())).Value!;
        var result = resolver.ResolveField(assembly, field);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.InvalidSignature);
    }

    [Fact]
    public void Authored_CSharp_ref_field_preserves_aliasing_and_binds_the_closed_signature()
    {
        // Only authored test code executes. The compiler still opens the image as data.
        int value = 11;
        var box = new BindingRefBox<int>(ref value);
        box.ReferenceValue = 73;
        Assert.Equal(73, value);
        string path = typeof(RefFieldResolutionTests).Assembly.Location;
        using var pe = new PEReader(File.OpenRead(path));
        MetadataReader reader = pe.GetMetadataReader();
        var references = reader.MemberReferences.Where(h =>
            reader.StringComparer.Equals(reader.GetMemberReference(h).Name, "ReferenceValue")).ToArray();
        Assert.Equal(2, references.Length); // Closed caller and open generic constructor.
        using var resolver = AssemblyResolver.Create([]).Value!;
        var assembly = resolver.OpenInput(path).Value!;
        var results = references.Select(h => resolver.ResolveField(assembly, h)).ToArray();
        Assert.All(results, r => Assert.True(r.Success, string.Join("; ", r.Diagnostics)));
        Assert.Contains(results, r => r.Value!.Signature == new TypeSignature.ByReference(new TypeSignature.Primitive(PrimitiveTypeCode.Int32)));
        Assert.Contains(results, r => r.Value!.Signature == new TypeSignature.ByReference(new TypeSignature.Variable(false, 0)));
    }
}

internal ref struct BindingRefBox<T>
{
    internal ref T ReferenceValue;
    internal BindingRefBox(ref T value) => ReferenceValue = ref value;
}
