using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class CompiledMemberResolutionTests
{
    [Fact]
    public void CSharp_generic_overloads_agree_with_the_authored_dotnet_fixture()
    {
        // Execute only our own compiled test code, never a resolver input assembly.
        Assert.Equal(11, BindingCalls.Generic(1));
        Assert.Equal(22, BindingCalls.Concrete());
        string path = typeof(BindingCalls).Assembly.Location;
        using var pe = new PEReader(File.OpenRead(path));
        MetadataReader reader = pe.GetMetadataReader();
        using var resolver = AssemblyResolver.Create([]).Value!;
        var assembly = resolver.OpenInput(path).Value!;
        var references = reader.MemberReferences.Where(h => reader.StringComparer.Equals(reader.GetMemberReference(h).Name, "Choose")).ToArray();
        Assert.Equal(2, references.Length);
        foreach (var reference in references)
        {
            var result = resolver.ResolveMethod(assembly, reference);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            var blob = reader.GetBlobReader(reader.GetMemberReference(reference).Signature);
            blob.ReadSignatureHeader();
            blob.ReadCompressedInteger();
            blob.ReadSignatureTypeCode();
            var parameter = blob.ReadSignatureTypeCode();
            var expected = typeof(BindingOverloads<>).GetMethods().Single(m => m.Name == "Choose"
                && m.GetParameters()[0].ParameterType.IsGenericParameter == (parameter == SignatureTypeCode.GenericTypeParameter));
            Assert.Equal(expected.MetadataToken, MetadataTokens.GetToken(result.Value!.Handle));
        }
    }

    [Fact]
    public void Interface_reference_resolves_to_its_declaration_and_explicit_body_remains_distinct()
    {
        Assert.Equal(33, BindingCalls.Interface());
        string path = typeof(BindingCalls).Assembly.Location;
        using var pe = new PEReader(File.OpenRead(path));
        MetadataReader reader = pe.GetMetadataReader();
        using var resolver = AssemblyResolver.Create([]).Value!;
        var assembly = resolver.OpenInput(path).Value!;
        var reference = Assert.Single(reader.MemberReferences, h => reader.StringComparer.Equals(reader.GetMemberReference(h).Name, "ReadBindingValue"));
        var result = resolver.ResolveMethod(assembly, reference);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(typeof(IBindingValue<>).GetMethod("ReadBindingValue")!.MetadataToken, MetadataTokens.GetToken(result.Value!.Handle));
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), result.Value.Signature.ReturnType);
        var implementationType = reader.TypeDefinitions.Single(h => reader.StringComparer.Equals(reader.GetTypeDefinition(h).Name, "BindingImplementation"));
        var implementation = reader.GetMethodImplementation(Assert.Single(reader.GetTypeDefinition(implementationType).GetMethodImplementations()));
        var body = resolver.ResolveMethod(assembly, implementation.MethodBody);
        var declaration = resolver.ResolveMethod(assembly, implementation.MethodDeclaration);
        Assert.True(body.Success, string.Join("; ", body.Diagnostics));
        Assert.True(declaration.Success, string.Join("; ", declaration.Diagnostics));
        Assert.Equal(result.Value, declaration.Value);
        Assert.NotEqual(result.Value.Handle, body.Value!.Handle);
    }

    [Fact]
    public void Compiled_inherited_generic_call_resolves_to_the_base_declaration()
    {
        Assert.Equal(44, BindingCalls.Inherited());
        string path = typeof(BindingCalls).Assembly.Location;
        using var pe = new PEReader(File.OpenRead(path));
        MetadataReader reader = pe.GetMetadataReader();
        using var resolver = AssemblyResolver.Create([]).Value!;
        var assembly = resolver.OpenInput(path).Value!;
        var reference = Assert.Single(reader.MemberReferences, h => reader.StringComparer.Equals(reader.GetMemberReference(h).Name, "ReadInheritedBinding"));
        var result = resolver.ResolveMethod(assembly, reference);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(typeof(BindingBase<>).GetMethod("ReadInheritedBinding")!.MetadataToken, MetadataTokens.GetToken(result.Value!.Handle));
        Assert.Equal(new TypeSignature.Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Int32)), result.Value.Signature.Parameters[0]);
    }
}

internal static class BindingOverloads<T>
{
    public static int Choose(T value) => 11;
    public static int Choose(int value) => 22;
}

internal interface IBindingValue<T>
{
    T ReadBindingValue();
}

internal sealed class BindingImplementation : IBindingValue<int>
{
    int IBindingValue<int>.ReadBindingValue() => 33;
}

internal static class BindingCalls
{
    internal static int Generic<T>(T value) => BindingOverloads<T>.Choose(value);
    internal static int Concrete() => BindingOverloads<int>.Choose(1);
    internal static int Interface() => ((IBindingValue<int>)new BindingImplementation()).ReadBindingValue();
    internal static int Inherited() => BindingDerived<int>.ReadInheritedBinding([1]);
}

internal class BindingBase<T>
{
    public static int ReadInheritedBinding(T value) => 44;
}

internal sealed class BindingDerived<T> : BindingBase<T[]>;
