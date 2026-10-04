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

public sealed class ValueObjectTranslationTests
{
    internal static ValueObjectTranslationResult Authored()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueObjectProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ValueTranslator.TranslateObjects(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueObjectProbe).GetMethod("Execute")!.MetadataToken), library);
    }

    [Fact]
    public void Discovers_and_translates_class_constructors_fields_and_value_borrows()
    {
        var result = Authored(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; Assert.Equal(10, program.Flow.Flow.Methods.Length);
        Assert.Equal(3, program.Bindings.Count(b => b.Kind == ObjectMethodKind.Constructor));
        Assert.Single(program.Bindings, b => b.Kind == ObjectMethodKind.ObjectBaseConstructor);
        Assert.Equal(2, program.Flow.Flow.Types.Types.Length);
        Assert.Equal(4, program.Flow.Flow.Types.References.Types.Length);
        var roots = ValueRootAssignment.Assign(program);
        Assert.Equal(5, roots.SelectMany(r => r.Safepoints).Count(s => s.Kind == ObjectSafepointKind.Allocation));
        ScalarLocationIrValidator.Validate(ValueScalarLowering.LowerObjects(program));
    }

    [Fact]
    public void Earlier_value_CIL_profile_still_rejects_class_construction()
    {
        Assert.False(ValueTranslationTests.Authored(typeof(ValueObjectProbe)).Success);
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ValueObjectProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateArrays(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValueObjectProbe).GetMethod("Execute")!.MetadataToken), library);
        Assert.False(result.Success);
    }

    internal static (ResolutionAssemblyBuilder Builder, MethodDefinitionHandle Entry) Fixture(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("ValueObjects" + kind, new Version(1, 0, 0, 0)));
        const MethodAttributes ctorFlags = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var obj = b.Type("System", "Object");
        var objCtor = b.Method(".ctor", [0x20, 0, 1], attributes: ctorFlags, emit: il =>
        { if (kind == "nontrivial_object") { il.LoadConstantI4(0); il.OpCode(ILOpCode.Pop); } il.OpCode(ILOpCode.Ret); });
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var value = b.Type("Test", "Value", baseType: valueBase, attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        byte valueEncoded = checked((byte)(MetadataTokens.GetRowNumber(value) << 2));
        b.Field("Number", 6, 8);
        var node = b.Type("Test", "Node", baseType: obj, attributes: kind == "hidden_class" ? TypeAttributes.NotPublic : TypeAttributes.Public);
        byte nodeEncoded = checked((byte)(MetadataTokens.GetRowNumber(node) << 2));
        var field = b.FieldWithAttributes("Number", kind == "private_field" ? FieldAttributes.Private : FieldAttributes.Public | FieldAttributes.InitOnly, 6, 8);
        if (kind == "combined_field_limit")
            for (int i = 1; i < ObjectModelLimits.MaximumFields; ++i)
                b.Field("Field" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), 6, 8);
        if (kind == "inline_heap_field") b.Field("Inline", 6, 0x11, valueEncoded);
        var valueHelper = kind == "constructor_value_flow" ? b.Method("Make", [0, 0, 0x11, valueEncoded],
            locals: [7, 1, 0x11, valueEncoded], emit: il => { il.LoadLocal(0); il.OpCode(ILOpCode.Ret); }) : default;
        var ctor = b.Method(".ctor", kind == "value_parameter" ? [0x20, 1, 1, 0x11, valueEncoded] : [0x20, 1, 1, 8],
            attributes: kind == "bad_ctor_flags" ? MethodAttributes.Public : kind == "private_ctor" ? MethodAttributes.Private | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : ctorFlags,
            emit: il =>
            {
                if (kind == "constructor_value_flow") { il.Call(valueHelper); il.OpCode(ILOpCode.Pop); }
                if (kind == "observe_this") { il.LoadArgument(0); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Pop); }
                if (kind != "missing_base") { il.LoadArgument(0); il.Call(objCtor); }
                if (kind == "repeated_base") { il.LoadArgument(0); il.Call(objCtor); }
                il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Stfld); il.Token(field); il.OpCode(ILOpCode.Ret);
            });
        var instance = b.Method("Read", [0x20, 0, 8], attributes: MethodAttributes.Public, emit: il =>
        { il.LoadArgument(0); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Driver", baseType: obj);
        var entry = b.Method("Execute", [0, 0, 8], locals: [7, 2, 0x11, valueEncoded, 0x12, nodeEncoded], emit: il =>
        {
            if (kind == "value_parameter") il.LoadLocal(0); else il.LoadConstantI4(7);
            il.OpCode(ILOpCode.Newobj); il.Token(kind == "newobj_nonconstructor" ? instance : ctor);
            il.StoreLocal(1);
            if (kind == "readonly_write") { il.LoadLocal(1); il.LoadConstantI4(0); il.OpCode(ILOpCode.Stfld); il.Token(field); }
            if (kind == "direct_ctor_call") { il.LoadLocal(1); il.LoadConstantI4(0); il.Call(ctor); }
            il.LoadLocal(1);
            if (kind == "instance_method") il.Call(instance);
            else { il.OpCode(ILOpCode.Ldfld); il.Token(field); }
            il.OpCode(ILOpCode.Ret);
        });
        return (b, entry);
    }
    internal static ValueObjectTranslationResult Read(ResolutionAssemblyBuilder builder, MethodDefinitionHandle entry)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("values.dll", builder.Build())).Value!;
        return ValueTranslator.TranslateObjects(resolver, input, entry, input);
    }

    [Theory]
    [InlineData("valid")]
    public void Nominal_reference_locals_and_operation_only_classes_are_included(string kind)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarLocationIrValidator.Validate(ValueScalarLowering.LowerObjects(result.Program!));
    }

    [Theory]
    [InlineData("missing_base")]
    [InlineData("repeated_base")]
    [InlineData("observe_this")]
    [InlineData("nontrivial_object")]
    [InlineData("bad_ctor_flags")]
    [InlineData("private_ctor")]
    [InlineData("private_field")]
    [InlineData("readonly_write")]
    [InlineData("direct_ctor_call")]
    [InlineData("instance_method")]
    [InlineData("newobj_nonconstructor")]
    [InlineData("value_parameter")]
    [InlineData("hidden_class")]
    [InlineData("inline_heap_field")]
    [InlineData("constructor_value_flow")]
    [InlineData("combined_field_limit")]
    public void Invalid_or_unsupported_class_semantics_return_diagnostics_without_partial_IR(string kind)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.NotEmpty(result.Diagnostics); Assert.All(result.Diagnostics, d => Assert.NotEmpty(d.Message));
        if (kind == "constructor_value_flow") Assert.Contains("constructor value/borrow flow", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        if (kind == "combined_field_limit") Assert.Contains("remaining field/interface budget", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        if (kind == "observe_this") Assert.Contains("uninitialized constructor receiver", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }
}
