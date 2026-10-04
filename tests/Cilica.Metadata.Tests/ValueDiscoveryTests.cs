using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueDiscoveryTests
{
    internal static ValueDiscoveryResult Authored(Type probe, string name = "Execute", MethodReadLimits? limits = null)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(probe.Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ValueProgramDiscovery.Read(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(probe.GetMethod(name)!.MetadataToken), library, limits);
    }

    [Theory]
    [InlineData(typeof(ValueReturnProbe), 5, 1)]
    [InlineData(typeof(ValueBorrowProbe), 4, 2)]
    public void Reads_real_CIL_closures_and_exact_layouts_without_claiming_translation(Type probe, int methods, int layouts)
    {
        var result = Authored(probe);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var input = result.Input!;
        Assert.Equal(methods, input.Methods.Length); Assert.Equal(layouts, input.Types.Types.Length);
        Assert.All(input.Methods, m => Assert.Equal(ObjectMethodKind.Static, m.Binding.Kind));
        Assert.All(input.Methods.SelectMany(m => m.Calls.Values), target => Assert.Contains(input.Methods, m => m.Binding.Method == target));
        Assert.All(input.Methods.SelectMany(m => m.Fields.Values), field => Assert.Contains(input.Types.Types.SelectMany(t => t.Fields), f => f.Id == field));
        Assert.Contains(input.Methods, m => m.TypeOperands.Count > 0);
        Assert.All(input.Types.Types, t => Assert.True(t.ExternallyVisible));
        if (probe == typeof(ValueBorrowProbe)) Assert.Contains(input.Methods, m => m.Parameters.Any(p => p.Kind == IrTypeKind.ManagedLocation));
        else Assert.Contains(input.Methods, m => m.ReturnType.Kind == IrTypeKind.Value);
    }

    [Fact]
    public void Entry_cannot_borrow_or_return_structs()
    {
        var result = Authored(typeof(ValueReturnProbe), "Make");
        Assert.False(result.Success); Assert.Contains("entry requires Int32", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Caller_read_limits_remain_effective()
    {
        var result = Authored(typeof(ValueReturnProbe), limits: new(MaximumBodyBytes: 1));
        Assert.False(result.Success); Assert.Null(result.Input); Assert.Single(result.Diagnostics);
    }

    internal static (ResolutionAssemblyBuilder Builder, MethodDefinitionHandle Entry) Fixture(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("ValueDiscovery" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var enclosing = kind == "hidden_enclosing" ? b.Type("Test", "Hidden", attributes: TypeAttributes.NotPublic) : default;
        var value = b.Type("Test", "Value", parent: enclosing, baseType: valueBase, attributes:
            TypeAttributes.Sealed | TypeAttributes.SequentialLayout | (kind is "hidden_type" or "hidden_type_only" ? TypeAttributes.NotPublic
                : kind == "hidden_enclosing" ? TypeAttributes.NestedPublic : TypeAttributes.Public));
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(value) << 2));
        var field = b.FieldWithAttributes("Number", kind == "private_scope_field" ? FieldAttributes.PrivateScope
            : kind is "private_field" or "same_owner" ? FieldAttributes.Private
            : kind is "readonly_write" or "readonly_address" ? FieldAttributes.Public | FieldAttributes.InitOnly : FieldAttributes.Public, 6, 8);
        var helper = b.Method("Helper", kind == "reference_parameter" ? [0, 1, 1, 0x1c]
            : kind == "byref_return" ? [0, 0, 0x10, 0x11, encoded]
            : kind == "nested_byref" ? [0, 1, 1, 0x10, 0x10, 0x11, encoded]
            : kind == "modified_parameter" ? [0, 1, 1, 0x1f, encoded, 0x11, encoded]
            : [0, 0, 1], attributes: (kind == "private_method" ? MethodAttributes.Private : MethodAttributes.Public) | MethodAttributes.Static,
            emit: il => il.OpCode(ILOpCode.Ret));
        if (kind is not ("same_owner" or "private_scope_field")) b.Type("Test", "Driver", baseType: obj);
        var entry = b.Method("Execute", [0, 0, 8], locals: kind == "byref_local" ? [7, 1, 0x10, 0x11, encoded] : [7, 1, 0x11, encoded], emit: il =>
        {
            if (kind == "hidden_type_only")
            {
                il.LoadLocalAddress(0); il.OpCode(ILOpCode.Initobj); il.Token(value);
                il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); return;
            }
            if (kind is "private_method" or "reference_parameter" or "byref_return" or "nested_byref" or "modified_parameter") il.Call(helper);
            il.LoadLocalAddress(0);
            if (kind == "readonly_write") { il.LoadConstantI4(1); il.OpCode(ILOpCode.Stfld); il.Token(field); il.LoadConstantI4(0); }
            else if (kind == "readonly_address") { il.OpCode(ILOpCode.Ldflda); il.Token(field); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); }
            else { il.OpCode(ILOpCode.Ldfld); il.Token(field); }
            il.OpCode(ILOpCode.Ret);
        });
        return (b, entry);
    }

    internal static ValueDiscoveryResult Read(ResolutionAssemblyBuilder b, MethodDefinitionHandle entry)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("values.dll", b.Build())).Value!;
        return ValueProgramDiscovery.Read(resolver, input, entry, input);
    }

    [Theory]
    [InlineData("private_field", "Fields require")]
    [InlineData("private_scope_field", "Fields require")]
    [InlineData("hidden_type", "Fields require")]
    [InlineData("hidden_type_only", "Value type use requires")]
    [InlineData("hidden_enclosing", "Fields require")]
    [InlineData("readonly_write", "Readonly value fields")]
    [InlineData("private_method", "Calls require")]
    [InlineData("byref_local", "Initial value CIL signatures")]
    [InlineData("byref_return", "Initial value CIL signatures")]
    [InlineData("reference_parameter", "Initial value CIL signatures")]
    [InlineData("nested_byref", "Invalid or unsupported type signature")]
    [InlineData("modified_parameter", "Initial value CIL signatures")]
    public void Rejects_inaccessible_metadata_and_unsupported_signature_shapes(string kind, string message)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.False(result.Success); Assert.Null(result.Input);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal); Assert.NotEmpty(diagnostic.Location!);
    }

    [Theory]
    [InlineData("same_owner")]
    [InlineData("readonly_address")]
    public void Preserves_same_owner_access_and_readonly_address_metadata(string kind)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Single(result.Input!.Methods); Assert.Single(result.Input.Types.Types);
    }
}
