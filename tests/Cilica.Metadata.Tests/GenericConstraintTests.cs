using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class GenericConstraintTests
{
    internal static BoxedValueTranslationResult Authored()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(GenericConstraintProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ValueTranslator.TranslateGenerics(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(GenericConstraintProbe).GetMethod("Execute")!.MetadataToken), library);
    }

    [Fact]
    public void Class_struct_and_constructor_constraints_are_checked_without_executing_constructors()
    {
        var result = Authored(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.DoesNotContain(result.Program!.Flow.Flow.Methods, m => m.Source.Name.Contains(".ctor", StringComparison.Ordinal));
        ScalarBoxedIrValidator.Validate(ValueScalarLowering.LowerBoxes(result.Program));
    }

    [Theory]
    [InlineData("class", false, false)]
    [InlineData("class", true, true)]
    [InlineData("struct", true, false)]
    [InlineData("struct", false, true)]
    [InlineData("new", true, false)]
    [InlineData("new", false, true)]
    [InlineData("new-public", true, true)]
    [InlineData("nullable", true, false)]
    [InlineData("conflict", true, false)]
    [InlineData("variance", false, false)]
    public void Invalid_metadata_arguments_cannot_enter_closed_layouts(string constraint, bool reference, bool succeeds)
    {
        var b = new ResolutionAssemblyBuilder(new("Constraints" + constraint + reference, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        if (constraint == "new-public") b.Method(".ctor", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            emit: il => il.OpCode(ILOpCode.Ret));
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var nullable = constraint == "nullable" ? b.Type("System", "Nullable`1", baseType: valueBase, arity: 1,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout) : default;
        var flags = constraint switch
        {
            "class" => GenericParameterAttributes.ReferenceTypeConstraint,
            "struct" or "nullable" => GenericParameterAttributes.NotNullableValueTypeConstraint,
            "new" or "new-public" => GenericParameterAttributes.DefaultConstructorConstraint,
            "conflict" => GenericParameterAttributes.ReferenceTypeConstraint | GenericParameterAttributes.NotNullableValueTypeConstraint,
            _ => GenericParameterAttributes.Covariant,
        };
        var generic = b.Type("Test", "Container`1", baseType: valueBase, arity: 1,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout, genericAttributes: flags);
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(generic) << 2));
        b.Field("Value", 6, 0x13, 0);
        b.Type("Test", "Driver", baseType: obj);
        byte[] argument = constraint == "nullable" ? [0x15, 0x11, checked((byte)(MetadataTokens.GetRowNumber(nullable) << 2)), 1, 8]
            : [reference ? (byte)0x1c : (byte)8];
        var entry = b.Method("Execute", [0, 0, 8], locals: [7, 1, 0x15, 0x11, encoded, 1, .. argument],
            emit: il => { il.LoadConstantI4(3); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("constraints.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateGenerics(resolver, input, entry, input);
        Assert.Equal(succeeds, result.Success);
        if (!succeeds) { Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics); }
    }
    [Fact]
    public void Constraint_inspection_is_charged_to_the_snapshot_budget()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ConstrainedValue<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var handle = (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ConstrainedValue<>).MetadataToken);
        var definition = resolver.ResolveType(input, handle).Value!;
        var raw = resolver.ReadTypeInput(input, handle).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(PrimitiveTypeCode.Int32)])), library,
            new(MaximumMembers: raw.MetadataMembersRead));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Id == Cilica.Metadata.DiagnosticIds.TranslationLimit);
    }

    [Theory]
    [InlineData(PrimitiveTypeCode.Boolean)] [InlineData(PrimitiveTypeCode.Char)]
    [InlineData(PrimitiveTypeCode.SByte)] [InlineData(PrimitiveTypeCode.Byte)]
    [InlineData(PrimitiveTypeCode.Int16)] [InlineData(PrimitiveTypeCode.UInt16)]
    [InlineData(PrimitiveTypeCode.UInt32)] [InlineData(PrimitiveTypeCode.Int64)] [InlineData(PrimitiveTypeCode.UInt64)]
    public void Integer_arguments_satisfy_struct_constraints_without_loading_runtime_types(PrimitiveTypeCode code)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ConstrainedValue<>).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var definition = resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ConstrainedValue<>).MetadataToken)).Value!;
        var result = resolver.ReadClosedTypeInput(new(definition, new([new TypeSignature.Primitive(code)])), library, new());
        Assert.NotNull(result.Value); Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Unused_nested_type_arguments_cannot_bypass_constraints()
    {
        var b = new ResolutionAssemblyBuilder(new("PhantomConstraint", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var generic = b.Type("Test", "Container`1", baseType: valueBase, arity: 1,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            genericAttributes: GenericParameterAttributes.NotNullableValueTypeConstraint);
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(generic) << 2));
        b.Type("Test", "Driver", baseType: obj);
        var keep = b.Method("Keep", [0x10, 1, 1, 8, 8], arity: 1,
            emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var call = b.Instantiate(keep, 0x0a, 1, 0x15, 0x11, encoded, 1, 0x1c);
        var entry = b.Method("Execute", [0, 0, 8], emit: il => { il.LoadConstantI4(3); il.Call(call); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("phantom.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateGenerics(resolver, input, entry, input);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("constraints", StringComparison.Ordinal));
    }

}
