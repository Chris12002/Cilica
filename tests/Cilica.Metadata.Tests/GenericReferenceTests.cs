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

public sealed class GenericReferenceTests
{
    internal static BoxedValueTranslationResult Translate(string method = "Execute", bool enabled = true)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(GenericReferenceProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(GenericReferenceProbe).GetMethod(method)!.MetadataToken);
        return enabled ? ValueTranslator.TranslateGenerics(resolver, input, entry, library)
            : ValueTranslator.TranslateBoxes(resolver, input, entry, library);
    }

    [Fact]
    public void Closed_class_hierarchies_fields_and_constructors_keep_exact_owner_identity()
    {
        var result = Translate(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var flow = result.Program!.Flow.Flow;
        var types = flow.Types.References.Types;
        var nodes = types.Where(t => t.Source.Token == typeof(GenericNode<>).MetadataToken).ToArray();
        var children = types.Where(t => t.Source.Token == typeof(GenericChild<>).MetadataToken).ToArray();
        Assert.Equal(2, nodes.Length); Assert.Equal(2, children.Length);
        Assert.NotEqual(ObjectTypeId.From(nodes[0].Source), ObjectTypeId.From(nodes[1].Source));
        Assert.NotEqual(nodes[0].Fields[0].Id, nodes[1].Fields[0].Id);
        Assert.Contains(nodes, node => node.ReferenceSlots.Length == 1);
        Assert.Contains(nodes, node => node.ReferenceSlots.Length == 3);
        Assert.All(children, child => Assert.Contains(nodes, node => ObjectTypeId.From(node.Source) == child.BaseType));
        Assert.Equal(4, flow.Methods.Count(m => m.Source.Token == typeof(GenericNode<>).GetConstructors()[0].MetadataToken
            || m.Source.Token == typeof(GenericChild<>).GetConstructors()[0].MetadataToken));
        ScalarBoxedIrValidator.Validate(ValueScalarLowering.LowerBoxes(result.Program));
    }

    [Theory]
    [InlineData("Execute", false)]
    [InlineData("UnsupportedInlineField", true)]
    public void Unselected_generics_and_unsupported_inline_class_fields_are_diagnostics(string method, bool enabled)
    {
        var result = Translate(method, enabled);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recursive_base_and_expanding_reference_graphs_are_diagnostics(bool expanding)
    {
        var b = new ResolutionAssemblyBuilder(new("GenericReferenceLimit" + expanding, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        byte encoded = 4 << 2;
        var parent = b.Specification(0x15, 0x12, encoded, 1, 0x13, 0);
        b.Type("Test", "Node`1", baseType: expanding ? obj : parent, arity: 1);
        if (expanding) b.Field("Next", 6, 0x15, 0x12, encoded, 1, 0x1d, 0x13, 0);
        b.Type("Test", "Driver", baseType: obj);
        var entry = b.Method("Execute", [0, 0, 8], locals: [7, 1, 0x15, 0x12, encoded, 1, 8],
            emit: il => { il.LoadConstantI4(3); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("reference-limit.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateGenerics(resolver, input, entry, input);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(expanding ? "64" : "cycle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => (d.Location ?? "").Contains("layout inclusion:", StringComparison.Ordinal));
    }

}
