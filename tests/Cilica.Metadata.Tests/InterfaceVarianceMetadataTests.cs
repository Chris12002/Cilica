using System;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InterfaceVarianceMetadataTests
{
    [Theory]
    [InlineData(GenericParameterAttributes.Covariant, "return", true)]
    [InlineData(GenericParameterAttributes.Covariant, "parameter", false)]
    [InlineData(GenericParameterAttributes.Covariant, "byref", false)]
    [InlineData(GenericParameterAttributes.Covariant, "outside", false)]
    [InlineData(GenericParameterAttributes.Contravariant, "return", false)]
    [InlineData(GenericParameterAttributes.Contravariant, "parameter", true)]
    [InlineData(GenericParameterAttributes.None, "parameter", true)]
    public void Interface_variance_positions_are_checked_before_closed_assignment(GenericParameterAttributes variance, string position, bool succeeds)
    {
        var b = new ResolutionAssemblyBuilder(new("VariancePositions", new Version(1, 0, 0, 0)));
        b.Type("System", "Object");
        var contract = b.Type("Test", "IContract`1", arity: 1, genericAttributes: variance,
            attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
        byte[] signature = position switch { "parameter" => [0x20, 1, 1, 0x13, 0], "byref" => [0x20, 0, 0x10, 0x13, 0],
            "outside" => [0x20, 0, 0x13, 1], _ => [0x20, 0, 0x13, 0] };
        b.Method("Member", signature, attributes: MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("positions.dll", b.Build())).Value!;
        var definition = resolver.ResolveType(input, contract).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Object)])), input, new());
        Assert.Equal(succeeds, result.Value is not null);
        if (!succeeds) Assert.NotEmpty(result.Diagnostics);
        else
        {
            Assert.Equal(variance, Assert.Single(result.Value!.ClosedParameterAttributes));
            var shortBudget = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Object)])), input,
                new(MaximumMembers: result.Value.MetadataMembersRead - 1));
            Assert.Null(shortBudget.Value); Assert.Contains(shortBudget.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Nested_variant_declarations_must_be_sound_too(bool valid)
    {
        var b = new ResolutionAssemblyBuilder(new("NestedVariance", new Version(1, 0, 0, 0)));
        b.Type("System", "Object");
        var nested = b.Type("Test", "INested`1", arity: 1, genericAttributes: GenericParameterAttributes.Covariant,
            attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
        b.Method("Nested", valid ? [0x20, 0, 0x13, 0] : [0x20, 1, 1, 0x13, 0],
            attributes: MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        var outer = b.Type("Test", "IOuter`1", arity: 1, genericAttributes: GenericParameterAttributes.Covariant,
            attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
        byte token = checked((byte)(System.Reflection.Metadata.Ecma335.MetadataTokens.GetRowNumber(nested) << 2));
        b.Method("Outer", [0x20, 0, 0x15, 0x12, token, 1, 0x13, 0],
            attributes: MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("nested.dll", b.Build())).Value!;
        var definition = resolver.ResolveType(input, outer).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Object)])), input, new());
        Assert.Equal(valid, result.Value is not null);
        if (!valid) Assert.Contains(result.Diagnostics, d => d.Message.Contains("variance position", StringComparison.Ordinal));
    }
}
