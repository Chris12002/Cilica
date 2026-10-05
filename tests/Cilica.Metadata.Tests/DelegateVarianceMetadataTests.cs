using System;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DelegateVarianceMetadataTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Nested_delegate_variance_is_checked_from_interface_signatures(bool valid)
    {
        var b = new ResolutionAssemblyBuilder(new("NestedDelegateVariance", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var multicast = b.Type("System", "MulticastDelegate", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var callable = b.Type("Test", "Callable`1", baseType: multicast, arity: 1, genericAttributes: GenericParameterAttributes.Covariant,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        b.Method("Invoke", valid ? [0x20, 0, 0x13, 0] : [0x20, 1, 1, 0x13, 0], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        var contract = b.Type("Test", "IContract`1", arity: 1, genericAttributes: GenericParameterAttributes.Covariant,
            attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
        byte token = checked((byte)(System.Reflection.Metadata.Ecma335.MetadataTokens.GetRowNumber(callable) << 2));
        b.Method("Get", [0x20, 0, 0x15, 0x12, token, 1, 0x13, 0],
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("nested.dll", b.Build())).Value!;
        var definition = resolver.ResolveType(input, contract).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Object)])), input, new());
        Assert.Equal(valid, result.Success);
        if (!valid) Assert.Contains(result.Diagnostics, d => d.Message.Contains("variance position", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(GenericParameterAttributes.Covariant, "return", PrimitiveTypeCode.Int32, true)]
    [InlineData(GenericParameterAttributes.Covariant, "parameter", PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Contravariant, "parameter", PrimitiveTypeCode.Int32, true)]
    [InlineData(GenericParameterAttributes.Contravariant, "return", PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Covariant, "byref", PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Covariant, "outside", PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.Contravariant, "return", PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.ReferenceTypeConstraint, "return", PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.ReferenceTypeConstraint, "return", PrimitiveTypeCode.Object, true)]
    public void Delegate_invoke_variance_and_constraints_are_validated(GenericParameterAttributes flags, string position,
        PrimitiveTypeCode argument, bool succeeds)
    {
        var b = new ResolutionAssemblyBuilder(new("DelegateVariance", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var multicast = b.Type("System", "MulticastDelegate", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var callable = b.Type("Test", "Callable`1", baseType: multicast, arity: 1, genericAttributes: flags,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        byte[] signature = position switch { "parameter" => [0x20, 1, 1, 0x13, 0], "byref" => [0x20, 0, 0x10, 0x13, 0],
            "outside" => [0x20, 0, 0x13, 1], _ => [0x20, 0, 0x13, 0] };
        b.Method("Invoke", signature, attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("variance.dll", b.Build())).Value!;
        var definition = resolver.ResolveType(input, callable).Value!;
        var closed = new ConstructedType(definition, new([new TypeSignature.Primitive(argument)]));
        var result = resolver.ReadClosedTypeInput(closed, input, new());
        Assert.Equal(succeeds, result.Success);
        if (!succeeds) { Assert.NotEmpty(result.Diagnostics); return; }
        Assert.NotEmpty(result.Value!.Source.Specialization);
        var shortBudget = resolver.ReadClosedTypeInput(closed, input, new(MaximumMembers: result.Value.MetadataMembersRead - 1));
        Assert.False(shortBudget.Success);
        Assert.Contains(shortBudget.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
        var declarations = resolver.ReadDelegateDeclarations(closed, new());
        Assert.True(declarations.Success, string.Join("; ", declarations.Diagnostics));
        var invoke = Assert.Single(declarations.Value!.Methods);
        var expected = new TypeSignature.Primitive(argument);
        Assert.Equal(expected, position == "parameter" ? Assert.Single(invoke.Signature.Parameters) : invoke.Signature.ReturnType);
        Assert.Equal(result.Value.Source.Specialization, invoke.Source.Specialization);
    }

    [Fact]
    public void A_foreign_multicast_base_does_not_authorize_delegate_variance()
    {
        var selected = new ResolutionAssemblyBuilder(new("SelectedRuntime", new Version(1, 0, 0, 0)));
        var obj = selected.Type("System", "Object");
        selected.Type("System", "MulticastDelegate", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var foreign = new ResolutionAssemblyBuilder(new("ForeignRuntime", new Version(1, 0, 0, 0)));
        var foreignObject = foreign.Type("System", "Object");
        var baseType = foreign.Type("System", "MulticastDelegate", baseType: foreignObject, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var callable = foreign.Type("Test", "Callable`1", baseType: baseType, arity: 1, genericAttributes: GenericParameterAttributes.Covariant,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        foreign.Method("Invoke", [0x20, 0, 0x13, 0], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var runtime = resolver.OpenInput(files.Write("selected.dll", selected.Build())).Value!;
        var input = resolver.OpenInput(files.Write("foreign.dll", foreign.Build())).Value!;
        var definition = resolver.ResolveType(input, callable).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Int32)])), runtime, new());
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("selected-runtime delegate", StringComparison.Ordinal));
    }
}
