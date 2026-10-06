using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ClosedMethodDeclarationReadTests
{
    [Theory]
    [InlineData("valid")] [InlineData("signature")] [InlineData("arity")]
    [InlineData("owner")] [InlineData("open")] [InlineData("constraint")] [InlineData("foreign")]
    public void Bodyless_closed_declarations_authenticate_signature_constraints_and_work(string defect)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ClosedDeclaration", new Version(1, 0, 0, 0)));
        b.Type("Test", "IValue", attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
        var handle = b.Method("Echo", [0x30, 1, 1, 0x1e, 0, 0x1e, 0], arity: 1,
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract,
            implementation: MethodImplAttributes.IL,
            genericAttributes: defect == "constraint" ? GenericParameterAttributes.ReferenceTypeConstraint : GenericParameterAttributes.None);
        var otherOwner = b.Type("Test", "Other");
        var spec = b.Instantiate(handle, 0x0a, 1, 8);
        var path = files.Write("input.dll", b.Build()); var image = resolver.OpenInput(path).Value!;
        var method = resolver.ResolveMethod(image, defect == "open" ? handle : spec).Value!;
        if (defect == "signature") method = method with { Signature = method.Signature with { ReturnType = new TypeSignature.Primitive(PrimitiveTypeCode.Int64) } };
        if (defect == "arity") method = method with { MethodArguments = SignatureList<TypeSignature>.Empty };
        if (defect == "owner") method = method with { DeclaringType = new(resolver.ResolveType(image, otherOwner).Value!, SignatureList<TypeSignature>.Empty) };
        if (defect == "foreign")
        {
            using var other = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
            var foreign = other.OpenInput(path).Value!;
            var argument = new TypeSignature.Named(other.ResolveType(foreign, otherOwner).Value!, SignatureTypeKind.Class);
            method = method with { MethodArguments = new([argument]), Signature = method.Signature with { ReturnType = argument, Parameters = new([argument]) } };
            Assert.Throws<ArgumentException>(() => resolver.ReadClosedMethodDeclarationInput(method, library, new())); return;
        }
        var read = resolver.ReadClosedMethodDeclarationInput(method, library, new());
        Assert.Equal(defect == "valid", read.Success);
        if (!read.Success) { Assert.NotEmpty(read.Diagnostics); return; }
        Assert.False(read.Value!.HasBody);
        Assert.Equal(method.Signature, read.Value.Signature);
        Assert.Equal(ClosedGenericKey.Create(method.DeclaringType.Arguments, method.MethodArguments), read.Value.Source.Specialization);
        int used = read.Value.MetadataMembersRead;
        Assert.True(resolver.ReadClosedMethodDeclarationInput(method, library, new(MaximumMembers: used)).Success);
        Assert.Contains(resolver.ReadClosedMethodDeclarationInput(method, library, new(MaximumMembers: used - 1)).Diagnostics,
            d => d.Id == DiagnosticIds.TranslationLimit);
    }
}
