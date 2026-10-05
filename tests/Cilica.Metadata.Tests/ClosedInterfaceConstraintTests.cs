using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedInterfaceConstraintTests
{
    [Fact]
    public void Runtime_interface_snapshot_retains_closed_inheritance_without_executing_library_code()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var definition = resolver.ResolveType(library, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(System.Collections.Generic.IReadOnlyList<>).MetadataToken)).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Int64)])), library, new());
        Assert.NotNull(result.Value); Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Value.Interfaces, signature => signature is TypeSignature.Generic generic
            && generic.Arguments.Count == 1 && generic.Arguments[0] == new TypeSignature.Primitive(PrimitiveTypeCode.Int64));
    }

    [Fact]
    public void Metadata_variance_admission_does_not_enable_unimplemented_runtime_conversions()
    {
        var result = IntegerObjectTranslationTests.Translate(nameof(VariantContracts.Test), fixture: typeof(VariantContracts));
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("interface", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(7, VariantContracts.Test(7, 11));
    }

    [Theory]
    [InlineData(GenericParameterAttributes.None, PrimitiveTypeCode.Object, true)]
    [InlineData(GenericParameterAttributes.Covariant, PrimitiveTypeCode.Object, true)]
    [InlineData(GenericParameterAttributes.Contravariant, PrimitiveTypeCode.Object, true)]
    [InlineData(GenericParameterAttributes.Covariant, PrimitiveTypeCode.Int32, true)]
    [InlineData(GenericParameterAttributes.Contravariant, PrimitiveTypeCode.Int64, true)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.ReferenceTypeConstraint, PrimitiveTypeCode.Object, true)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.ReferenceTypeConstraint, PrimitiveTypeCode.Int32, false)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.NotNullableValueTypeConstraint, PrimitiveTypeCode.Int32, true)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.NotNullableValueTypeConstraint, PrimitiveTypeCode.Object, false)]
    [InlineData(GenericParameterAttributes.Covariant | GenericParameterAttributes.Contravariant, PrimitiveTypeCode.Object, false)]
    [InlineData((GenericParameterAttributes)32, PrimitiveTypeCode.Object, false)]
    public void Variance_declarations_preserve_special_constraints(GenericParameterAttributes flags, PrimitiveTypeCode argument, bool succeeds)
    {
        var b = new ResolutionAssemblyBuilder(new("VariantDeclarations", new Version(1, 0, 0, 0)));
        b.Type("System", "Object");
        var contract = b.Type("Test", "IContract`1", arity: 1,
            attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, genericAttributes: flags);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("contracts.dll", b.Build())).Value!;
        var definition = resolver.ResolveType(input, contract).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(argument)])), input, new());
        Assert.Equal(succeeds, result.Value is not null);
        if (succeeds) { Assert.Empty(result.Diagnostics); Assert.NotEmpty(result.Value!.Source.Specialization); }
        else Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData(GenericParameterAttributes.Covariant)] [InlineData(GenericParameterAttributes.Contravariant)]
    public void Class_parameters_cannot_claim_interface_variance(GenericParameterAttributes flags)
    {
        var b = new ResolutionAssemblyBuilder(new("VariantClass", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var type = b.Type("Test", "Class`1", baseType: obj, arity: 1, genericAttributes: flags);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("class.dll", b.Build())).Value!;
        var definition = resolver.ResolveType(input, type).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Object)])), input, new());
        Assert.Null(result.Value); Assert.Contains(result.Diagnostics, d => d.Message.Contains("interface type parameter", StringComparison.Ordinal));
    }
}
