using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueTranslationTests
{
    internal static ValueTranslationResult Authored(Type probe)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(probe.Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ValueTranslator.Translate(resolver, input,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(probe.GetMethod("Execute")!.MetadataToken), library);
    }

    [Theory]
    [InlineData(typeof(ValueReturnProbe))]
    [InlineData(typeof(ValueBorrowProbe))]
    [InlineData(typeof(ValueCilProbe))]
    public void Real_CIL_lowers_through_validated_copy_and_borrow_IR(Type probe)
    {
        var result = Authored(probe); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ManagedLocationIrValidator.Validate(result.Program!);
        var lowered = ValueScalarLowering.LowerLocations(result.Program!, result.Bindings);
        ScalarLocationIrValidator.Validate(lowered);
        Assert.Contains(result.Program!.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Type.Kind == IrTypeKind.ManagedLocation);
    }

    internal static ValueTranslationResult Read(ResolutionAssemblyBuilder builder, MethodDefinitionHandle entry)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("values.dll", builder.Build())).Value!;
        return ValueTranslator.Translate(resolver, input, entry, input);
    }

    internal static (ResolutionAssemblyBuilder Builder, MethodDefinitionHandle Entry) Fixture(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("ValueTranslation" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        const TypeAttributes flags = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout;
        var value = b.Type("Test", "Value", baseType: valueBase, attributes: flags);
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(value) << 2));
        var field = b.FieldWithAttributes("Number", FieldAttributes.Public | (kind == "readonly_indirect" ? FieldAttributes.InitOnly : 0), 6, 8);
        var other = b.Type("Test", "Other", baseType: valueBase, attributes: flags);
        b.Field("Other", 6, 8);
        var otherField = b.Field("Number", 6, 8);
        b.Type("Test", "Driver", baseType: obj);
        var entry = b.Method("Execute", [0, 1, 8, 8], locals: [7, 2, 0x11, encoded, 0x11, encoded], emit: il =>
        {
            switch (kind)
            {
                case "stack_join":
                case "wrong_join":
                    var alternate = il.DefineLabel(); var join = il.DefineLabel();
                    il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, alternate);
                    il.LoadLocal(0); il.Branch(ILOpCode.Br, join); il.MarkLabel(alternate);
                    if (kind == "wrong_join") il.LoadConstantI4(0); else il.LoadLocal(1);
                    il.Branch(ILOpCode.Br, join); il.MarkLabel(join); il.StoreLocal(0); break;
                case "borrow_join":
                    var destination = il.DefineLabel(); il.LoadLocalAddress(0); il.Branch(ILOpCode.Br, destination);
                    il.MarkLabel(destination); il.OpCode(ILOpCode.Pop); break;
                case "readonly_indirect":
                    il.LoadLocalAddress(0); il.OpCode(ILOpCode.Ldflda); il.Token(field);
                    il.LoadConstantI4(1); il.OpCode(ILOpCode.Stind_i4); break;
                case "wrong_field_owner":
                    il.LoadLocalAddress(0); il.OpCode(ILOpCode.Ldfld); il.Token(otherField); il.OpCode(ILOpCode.Pop); break;
                case "wrong_object_token":
                    il.LoadLocalAddress(0); il.OpCode(ILOpCode.Initobj); il.Token(other); break;
                case "wrong_store": il.LoadConstantI4(1); il.StoreLocal(0); break;
                case "wrong_indirection": il.LoadConstantI4(0); il.OpCode(ILOpCode.Ldind_i4); il.OpCode(ILOpCode.Pop); break;
                case "wrong_index": il.LoadLocal(2); il.OpCode(ILOpCode.Pop); break;
                case "underflow": il.OpCode(ILOpCode.Pop); break;
                case "dead_block": il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); break;
                case "unsupported": il.LoadLocal(0); il.OpCode(ILOpCode.Box); il.Token(value); il.OpCode(ILOpCode.Pop); break;
                case "residual_return": il.LoadConstantI4(4); break;
                case "max_stack": for (int i = 0; i < 9; i++) il.LoadConstantI4(i); break;
                case "expanded_statement_budget":
                    for (int i = 0; i < 16385; i++)
                    {
                        il.LoadLocalAddress(0); il.LoadConstantI4(0); il.OpCode(ILOpCode.Stfld); il.Token(field);
                    }
                    break;
                case "copy_overlap":
                    il.LoadLocalAddress(0); il.LoadConstantI4(37); il.OpCode(ILOpCode.Stfld); il.Token(field);
                    il.LoadLocalAddress(0); il.LoadLocalAddress(0); il.OpCode(ILOpCode.Cpobj); il.Token(value);
                    il.LoadLocalAddress(1); il.LoadLocalAddress(0); il.OpCode(ILOpCode.Cpobj); il.Token(value);
                    il.LoadLocalAddress(0); il.OpCode(ILOpCode.Initobj); il.Token(value);
                    il.LoadLocal(1); il.StoreLocal(0); break;
            }
            il.LoadLocal(0); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Ret);
        });
        return (b, entry);
    }

    [Theory]
    [InlineData("wrong_join", "incompatible stack")]
    [InlineData("borrow_join", "cannot cross block joins")]
    [InlineData("readonly_indirect", "Readonly managed storage")]
    [InlineData("wrong_field_owner", "exact inline receiver")]
    [InlineData("wrong_object_token", "exact element type")]
    [InlineData("wrong_store", "exact storage type")]
    [InlineData("wrong_indirection", "managed location")]
    [InlineData("wrong_index", "Variable index")]
    [InlineData("underflow", "stack operand")]
    [InlineData("dead_block", "Unreachable blocks")]
    [InlineData("unsupported", "Instruction 'box'")]
    [InlineData("residual_return", "residual evaluation-stack")]
    [InlineData("max_stack", "declared maximum")]
    [InlineData("expanded_statement_budget", "IR statement limit")]
    public void Invalid_and_unsupported_bodies_fail_without_partial_IR(string kind, string message)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Bindings);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("IL_", diagnostic.Location!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stack_join")]
    [InlineData("copy_overlap")]
    public void Exact_value_joins_and_overlapping_whole_copy_validate(string kind)
    {
        var (b, entry) = Fixture(kind); var result = Read(b, entry);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarLocationIrValidator.Validate(ValueScalarLowering.LowerLocations(result.Program!, result.Bindings));
        if (kind == "copy_overlap")
            Assert.Equal(2, result.Program!.Flow.Methods.Single().Blocks.SelectMany(b => b.Values).Count(v => v.Expression is IrExpression.LoadLocation));
    }
}
