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

public sealed class ValueArrayTranslationTests
{
    internal static ValueArrayTranslationResult Authored()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueArrayProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ValueTranslator.TranslateArrays(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueArrayProbe).GetMethod("Execute")!.MetadataToken), library);
    }

    [Fact]
    public void Static_CIL_composes_nested_values_allocations_borrows_and_return_packets()
    {
        var result = Authored(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; Assert.Equal(5, program.Flow.Flow.Methods.Length);
        Assert.Equal(2, program.Flow.Flow.Types.Types.Length);
        Assert.Single(program.Flow.Flow.Types.References.Arrays);
        var roots = ValueRootAssignment.Assign(program);
        Assert.Equal(2, roots.SelectMany(r => r.Safepoints).Count(s => s.Kind == ObjectSafepointKind.Allocation));
        var lowered = ValueScalarLowering.LowerArrays(program); ScalarLocationIrValidator.Validate(lowered);
        Assert.Contains(lowered.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.NewArray);
    }

    [Fact]
    public void Old_CIL_profile_still_rejects_allocation()
    {
        var result = ValueTranslationTests.Authored(typeof(ValueArrayProbe)); Assert.False(result.Success); Assert.Null(result.Program);
    }

    internal static (ResolutionAssemblyBuilder Builder, MethodDefinitionHandle Entry) Fixture(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("ValueArrays" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var integer = b.Type("System", "Int32", baseType: valueBase, attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        var value = b.Type("Test", "Value", baseType: valueBase, attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        var encoded = checked((byte)(MetadataTokens.GetRowNumber(value) << 2));
        b.Field("Number", 6, 8);
        var fake = b.Type("Other", "Int32", baseType: valueBase, attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        b.Type("Test", "Driver", baseType: obj);
        var entry = b.Method("Execute", [0, 0, 8], locals: [7, 2, 0x11, encoded, 0x1d, 8], emit: il =>
        {
            if (kind == "null_order") { il.OpCode(ILOpCode.Ldnull); il.LoadConstantI4(-1); il.OpCode(ILOpCode.Ldelem_i4); il.OpCode(ILOpCode.Ret); return; }
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(kind == "fake_integer" ? fake : kind == "value_element" ? value : integer);
            il.StoreLocal(1);
            if (kind == "new_only") { il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); return; }
            if (kind == "wrong_array") il.LoadLocal(0); else il.LoadLocal(1);
            if (kind == "wrong_index") { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldelem_i4); }
            else if (kind == "wrong_store") { il.LoadConstantI4(0); il.LoadLocal(0); il.OpCode(ILOpCode.Stelem_i4); il.LoadConstantI4(0); }
            else if (kind == "element_address") { il.LoadConstantI4(0); il.OpCode(ILOpCode.Ldelema); il.Token(integer); }
            else
            {
                il.OpCode(ILOpCode.Ldlen);
                if (kind == "split_length") { var target = il.DefineLabel(); il.Branch(ILOpCode.Br, target); il.MarkLabel(target); }
                if (kind != "bare_length") il.OpCode(ILOpCode.Conv_i4);
            }
            il.OpCode(ILOpCode.Ret);
        });
        return (b, entry);
    }

    internal static ValueArrayTranslationResult Read(ResolutionAssemblyBuilder builder, MethodDefinitionHandle entry)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("values.dll", builder.Build())).Value!;
        return ValueTranslator.TranslateArrays(resolver, input, entry, input);
    }

    [Theory]
    [InlineData("fake_integer", "selected runtime's Int32")]
    [InlineData("value_element", "selected runtime's Int32")]
    [InlineData("bare_length", "adjacent conv.i4")]
    [InlineData("split_length", "adjacent conv.i4")]
    [InlineData("wrong_array", "Int32 vector")]
    [InlineData("wrong_index", "exact storage type")]
    [InlineData("wrong_store", "exact storage type")]
    [InlineData("element_address", "Instruction 'ldelema'")]
    public void Invalid_array_CIL_is_rejected_before_native_lowering(string kind, string message)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(message, Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("new_only")]
    [InlineData("null_order")]
    public void Operation_only_vector_roots_and_null_access_views_are_registered(string kind)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Single(result.Program!.Flow.Flow.Types.References.Arrays);
        ScalarLocationIrValidator.Validate(ValueScalarLowering.LowerArrays(result.Program));
    }
}
