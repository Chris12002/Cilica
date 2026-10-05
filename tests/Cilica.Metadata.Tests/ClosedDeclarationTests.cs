using System;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedDeclarationTests
{
    [Fact]
    public void Selection_retains_closed_signatures_and_rejects_ambiguity_missing_members_and_bad_environments()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var builder = new ResolutionAssemblyBuilder(new("Declarations", new Version(1, 0, 0, 0)));
        var owner = builder.Type("Test", "Owner`1", arity: 1);
        var read = builder.Method("Read", [0x20, 0, 0x13, 0], attributes: System.Reflection.MethodAttributes.Public);
        builder.Method("Overload", [0, 1, 8, 8]);
        builder.Method("Overload", [0, 1, 8, 0x0e]);
        var input = resolver.OpenInput(files.Write("declarations.dll", builder.Build())).Value!;
        var type = resolver.ResolveType(input, owner).Value!;
        var closed = new ConstructedType(type, new([new TypeSignature.Primitive(PrimitiveTypeCode.Int64)]));
        var selected = resolver.ResolveClosedDeclaration(closed, "Read", 0, new());
        Assert.True(selected.Success, string.Join("; ", selected.Diagnostics));
        Assert.Equal(read, selected.Value!.Handle); Assert.Equal(closed, selected.Value.DeclaringType);
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int64), selected.Value.Signature.ReturnType);
        Assert.False(resolver.ResolveClosedDeclaration(closed, "Overload", 1, new()).Success);
        Assert.Contains(resolver.ResolveClosedDeclaration(closed, "Missing", 0, new()).Diagnostics, d => d.Id == DiagnosticIds.MemberNotFound);
        Assert.False(resolver.ResolveClosedDeclaration(new(type, SignatureList<TypeSignature>.Empty), "Read", 0, new()).Success);
        Assert.Contains(resolver.ResolveClosedDeclaration(closed, "Read", 0, new(1)).Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }
}
