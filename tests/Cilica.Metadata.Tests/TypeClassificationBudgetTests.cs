using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class TypeClassificationBudgetTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Scalar_enums_validate_without_consuming_executable_layout_identities(bool optimized, bool fields)
    {
        var result = Translate(257, fields, false, false);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.DoesNotContain(result.Program!.Flow.Flow.Flow.Types.Types, t => t.Source.Name.StartsWith("Test.Status", StringComparison.Ordinal));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [(fields ? "EnumFields" : "EnumLocals", result.Program, (a, b) => unchecked(a + b))]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Malformed_last_enum_remains_diagnostic_after_the_fact_cache_fills(bool fields)
    {
        var result = Translate(257, fields, true, false);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Enum scalar storage", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Transient_enum_validation_still_charges_the_field_budget(bool fields)
    {
        var result = Translate(1, fields, false, false, manyFields: true);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit && d.Message.Contains("field", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void Transient_enum_validation_still_charges_the_metadata_work_budget()
    {
        var result = Translate(1, false, false, true);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit && d.Message.Contains("limit", StringComparison.Ordinal));
    }
    private static ManagedExceptionTranslationResult Translate(int count, bool fields, bool malformed, bool expensive, bool manyFields = false)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ClassificationBudget", new Version(1, 0, 0, 0)));
        var reference = b.Reference(library.Identity);
        var parent = b.TypeReference("System", "Enum", reference);
        var types = new List<TypeDefinitionHandle>();
        for (int i = 0; i < count; i++)
        {
            types.Add(b.Type("Test", "Status" + i, baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed));
            b.FieldWithAttributes("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
                6, malformed && i == count - 1 ? (byte)10 : (byte)8);
        }
        if (manyFields)
        {
            var signature = new BlobBuilder(); signature.WriteByte(6); signature.WriteByte(0x11);
            signature.WriteCompressedInteger(MetadataTokens.GetRowNumber(types[^1]) << 2);
            for (int i = 0; i < ObjectModelLimits.MaximumFields; i++)
            {
                var constant = b.FieldWithAttributes("Constant" + i, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, signature.ToArray());
                b.Constant(constant, i);
            }
        }
        if (expensive)
            for (int i = 0; i < ObjectModelLimits.MaximumMetadataMembers; i++) b.Method("Unused" + i, [0, 0, 1]);
        var objectType = b.TypeReference("System", "Object", reference);
        var baseCtor = b.Member(objectType, ".ctor", 0x20, 0, 1);
        var owner = b.Type("Test", "Payload", baseType: objectType);
        var slots = new List<FieldDefinitionHandle>();
        if (fields)
            foreach (var type in types)
            {
                var signature = new BlobBuilder(); signature.WriteByte(6); signature.WriteByte(0x11);
                signature.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) << 2);
                slots.Add(b.Field("Field" + slots.Count, signature.ToArray()));
            }
        var constructor = b.Method(".ctor", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            emit: il => { il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Root");
        var locals = new BlobBuilder(); locals.WriteByte(7); locals.WriteCompressedInteger(fields ? 1 : count);
        foreach (var type in fields ? [owner] : types)
        {
            locals.WriteByte(fields ? (byte)0x12 : (byte)0x11);
            locals.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) << 2);
        }
        var root = b.Method("Run", [0, 2, 8, 8, 8], locals: locals.ToArray(), emit: il => {
            if (fields)
            {
                il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.StoreLocal(0);
                il.LoadLocal(0); il.LoadArgument(0); il.OpCode(ILOpCode.Stfld); il.Token(slots[0]);
                il.LoadLocal(0); il.LoadArgument(1); il.OpCode(ILOpCode.Stfld); il.Token(slots[^1]);
                il.LoadLocal(0); il.OpCode(ILOpCode.Ldfld); il.Token(slots[0]);
                il.LoadLocal(0); il.OpCode(ILOpCode.Ldfld); il.Token(slots[^1]);
            }
            else
            {
                il.LoadArgument(0); il.StoreLocal(0); il.LoadArgument(1); il.StoreLocal(count - 1);
                il.LoadLocal(0); il.LoadLocal(count - 1);
            }
            il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
    }
}
