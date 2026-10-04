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

public sealed class BoxedValueTranslationTests
{
    internal static BoxedValueTranslationResult Authored()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(BoxedValueProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ValueTranslator.TranslateBoxes(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BoxedValueProbe).GetMethod("Execute")!.MetadataToken), library);
    }

    internal static BoxedValueTranslationResult Reduced(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("BoxedCil" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var value = b.Type("Test", "Value", baseType: valueBase, attributes: TypeAttributes.Sealed | TypeAttributes.SequentialLayout
            | (kind == "hidden" ? TypeAttributes.NotPublic : TypeAttributes.Public));
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(value) << 2));
        var field = b.FieldWithAttributes("Number", FieldAttributes.Public | (kind == "readonly" ? FieldAttributes.InitOnly : 0), 6, 8);
        b.Type("Test", "Driver", baseType: obj);
        var mutate = b.Method("Mutate", [0, 1, 1, 0x10, 0x11, encoded], locals: [7, 1, 0x11, encoded], emit: il =>
        {
            il.LoadLocal(0); il.OpCode(ILOpCode.Box); il.Token(value); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); il.LoadConstantI4(17); il.OpCode(ILOpCode.Stfld); il.Token(field); il.OpCode(ILOpCode.Ret);
        });
        var entry = b.Method("Execute", [0, 0, 8], locals: [7, 1, 0x11, encoded], emit: il =>
        {
            if (kind == "box_reference") il.OpCode(ILOpCode.Ldnull); else il.LoadLocal(0);
            il.OpCode(ILOpCode.Box); il.Token(kind == "class_box" ? obj : value);
            il.OpCode(ILOpCode.Dup);
            if (kind == "unbox_scalar") { il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); }
            il.OpCode(ILOpCode.Unbox); il.Token(value);
            il.Call(mutate);
            il.OpCode(ILOpCode.Unbox_any); il.Token(kind == "reference_unbox_any" ? obj : value);
            il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("boxes.dll", b.Build())).Value!;
        return ValueTranslator.TranslateBoxes(resolver, input, entry, input);
    }

    [Fact]
    public void Real_CSharp_boxes_compose_class_graphs_struct_copies_and_returns()
    {
        var result = Authored(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var values = result.Program!.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values).ToArray();
        Assert.Contains(values, v => v.Expression is IrExpression.BoxValue);
        Assert.Contains(values, v => v.Expression is IrExpression.UnboxValue);
        ScalarBoxedIrValidator.Validate(ValueScalarLowering.LowerBoxes(result.Program));
    }

    [Fact]
    public void Unbox_CIL_lends_mutable_storage_to_an_allocating_static_helper()
    {
        var result = Reduced("valid"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values),
            v => v.Expression is IrExpression.UnboxLocation);
        ScalarBoxedIrValidator.Validate(ValueScalarLowering.LowerBoxes(result.Program));
    }

    [Theory]
    [InlineData("box_reference")]
    [InlineData("class_box")]
    [InlineData("unbox_scalar")]
    [InlineData("reference_unbox_any")]
    [InlineData("hidden")]
    [InlineData("readonly")]
    public void Unsupported_or_invalid_boxing_has_diagnostics_without_partial_IR(string kind)
    {
        var result = Reduced(kind); Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Earlier_object_value_profile_remains_closed_to_boxing()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(BoxedValueProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateObjects(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(BoxedValueProbe).GetMethod("Execute")!.MetadataToken), library);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
