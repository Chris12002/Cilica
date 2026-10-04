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

public sealed class ClosedGenericTypeTests
{
    internal static BoxedValueTranslationResult Translate(string method = "Execute", bool enabled = true)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ClosedGenericTypeProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ClosedGenericTypeProbe).GetMethod(method)!.MetadataToken);
        return enabled ? ValueTranslator.TranslateGenerics(resolver, input, entry, library)
            : ValueTranslator.TranslateBoxes(resolver, input, entry, library);
    }

    [Fact]
    public void Closed_layouts_fields_boxes_and_owner_static_methods_retain_distinct_identities()
    {
        var result = Translate(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var catalog = result.Program!.Flow.Flow.Types;
        var pairs = catalog.Types.Where(t => t.Source.Token == typeof(GenericPair<,>).MetadataToken).ToArray();
        Assert.Equal(2, pairs.Length);
        Assert.NotEqual(ObjectTypeId.From(pairs[0].Source), ObjectTypeId.From(pairs[1].Source));
        Assert.NotEqual(pairs[0].Fields[0].Storage, pairs[1].Fields[0].Storage);
        var plans = ValueLayoutPlanner.Build(catalog);
        Assert.All(pairs, pair => Assert.Single(plans.Single(p => p.Type == ObjectTypeId.From(pair.Source)).Slots.Where(s => s.Storage is ObjectStorage.Reference)));
        var creates = result.Program.Flow.Flow.Methods.Where(m => m.Source.Token == typeof(GenericPair<,>).GetMethod("Create")!.MetadataToken).ToArray();
        Assert.Equal(2, creates.Length);
        Assert.NotEqual(IrMethodId.From(creates[0].Source), IrMethodId.From(creates[1].Source));
        ScalarBoxedIrValidator.Validate(ValueScalarLowering.LowerBoxes(result.Program));
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("recursive")]
    [InlineData("expanding")]
    public void Invalid_generic_layouts_are_bounded_and_report_the_inclusion(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("GenericLayout" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var valueBase = b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var hidden = b.Type("Test", "Hidden", baseType: obj, attributes: TypeAttributes.NotPublic);
        var generic = b.Type("Test", "Container`1", baseType: valueBase, arity: 1,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        byte encoded = checked((byte)(MetadataTokens.GetRowNumber(generic) << 2));
        if (kind == "recursive") b.Field("Next", 6, 0x15, 0x11, encoded, 1, 0x13, 0);
        if (kind == "expanding") b.Field("Next", 6, 0x15, 0x11, encoded, 1, 0x1d, 0x13, 0);
        b.Type("Test", "Driver", baseType: obj);
        byte[] argument = kind == "hidden" ? [0x12, checked((byte)(MetadataTokens.GetRowNumber(hidden) << 2))] : [8];
        var closed = b.Specification([0x15, 0x11, encoded, 1, .. argument]);
        var entry = b.Method("Execute", [0, 0, 8], locals: [7, 1, 0x15, 0x11, encoded, 1, .. argument], emit: il =>
        {
            il.LoadLocalAddress(0); il.OpCode(ILOpCode.Initobj); il.Token(closed); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("types.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateGenerics(resolver, input, entry, input);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        if (kind == "hidden") Assert.Contains(result.Diagnostics, d => d.Message.Contains("visible", StringComparison.OrdinalIgnoreCase));
        else Assert.Contains(result.Diagnostics, d => (d.Location ?? "").Contains("value inclusion:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Execute", false)]
    [InlineData("Constraint", true)]
    public void Unselected_or_constraint_bearing_types_have_no_partial_program(string method, bool enabled)
    {
        var result = Translate(method, enabled);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        if (enabled) Assert.Contains(result.Diagnostics, d => d.Message.Contains("constraints", StringComparison.OrdinalIgnoreCase));
    }
}
