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

public sealed class ValueMetadataTests
{
    private const TypeAttributes Struct = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout;
    private static ResolutionAssemblyBuilder Builder(string name = "Values") => new(new(name, new Version(1, 0, 0, 0)));
    private static byte[] FieldType(EntityHandle type, bool value = true)
    {
        var blob = new BlobBuilder(); blob.WriteByte(0x06); blob.WriteByte(value ? (byte)0x11 : (byte)0x12);
        blob.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) * 4 + (type.Kind == HandleKind.TypeReference ? 1 : 0));
        return blob.ToArray();
    }
    private static ValueCatalogResult Build(ResolutionAssemblyBuilder builder, TypeDefinitionHandle root)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("values.dll", builder.Build())).Value!;
        return ValueModelBuilder.BuildCatalog(resolver, [resolver.ResolveType(input, root).Value!], input);
    }
    private static TypeDefinitionHandle Bases(ResolutionAssemblyBuilder builder)
    {
        var objectBase = builder.Type("System", "Object");
        return builder.Type("System", "ValueType", baseType: objectBase, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
    }

    [Fact]
    public void Authored_structs_produce_detached_nested_reference_and_vector_paths()
    {
        ValueTypeCatalog catalog;
        using (var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!)
        {
            var input = resolver.OpenInput(typeof(ValuePair).Assembly.Location).Value!;
            var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
            var result = ValueModelBuilder.BuildCatalog(resolver,
                [resolver.ResolveType(input, (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(ValuePair).MetadataToken)).Value!], library);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); catalog = result.Catalog!;
        }
        var plan = ValueLayoutPlanner.Build(catalog).Single(p => p.Type == catalog.Roots[0]);
        Assert.Equal(2, catalog.Types.Length); Assert.NotNull(catalog.References.String);
        Assert.Equal(2, catalog.References.Arrays.Length);
        Assert.Equal(8, plan.Slots.Length); Assert.Equal(6, plan.References.Length);
        Assert.Equal(4, plan.References.Count(p => p.Fields.Length == 2));
    }

    [Fact]
    public void Repeated_nested_types_and_duplicate_roots_share_metadata_but_not_field_paths()
    {
        var builder = Builder(); var valueBase = Bases(builder);
        var leaf = builder.Type("Example", "Leaf", baseType: valueBase, attributes: Struct);
        builder.Field("Number", 0x06, 0x08); builder.Field("Reference", 0x06, 0x1c);
        var root = builder.Type("Example", "Pair", baseType: valueBase, attributes: Struct);
        builder.Field("Left", FieldType(leaf)); builder.Field("Right", FieldType(leaf));
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("values.dll", builder.Build())).Value!;
        var resolved = resolver.ResolveType(input, root).Value!;
        var result = ValueModelBuilder.BuildCatalog(resolver, [resolved, resolved], input);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Single(result.Catalog!.Roots); Assert.Equal(2, result.Catalog.Types.Length);
        Assert.Equal(2, ValueLayoutPlanner.Build(result.Catalog).Single(p => p.Type == result.Catalog.Roots[0]).References.Length);
    }

    [Fact]
    public void CSharp_empty_struct_size_marker_keeps_an_empty_logical_payload()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(EmptyBoxValue).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var handle = (TypeDefinitionHandle)MetadataTokens.EntityHandle(typeof(EmptyBoxValue).MetadataToken);
        Assert.Equal(1, resolver.ReadTypeInput(input, handle).Value!.ClassSize);
        var result = ValueModelBuilder.BuildCatalog(resolver, [resolver.ResolveType(input, handle).Value!], library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Empty(Assert.Single(ValueLayoutPlanner.Build(result.Catalog!)).Slots);
    }

    [Theory]
    [InlineData(1, false, 0, true)]
    [InlineData(1, true, 0, false)]
    [InlineData(2, false, 0, false)]
    [InlineData(1, false, 1, false)]
    public void Empty_size_exception_does_not_admit_custom_nonempty_layouts(int size, bool field, int packing, bool success)
    {
        var builder = Builder(); var valueBase = Bases(builder);
        var root = builder.Type("Example", "EmptySized", baseType: valueBase, attributes: Struct);
        builder.TypeLayout(root, (ushort)packing, (uint)size);
        if (field) builder.Field("Number", 6, 8);
        Assert.Equal(success, Build(builder, root).Success);
    }

    [Theory]
    [InlineData("class_base")]
    [InlineData("enum")]
    [InlineData("generic")]
    [InlineData("interface")]
    [InlineData("explicit")]
    [InlineData("packing")]
    [InlineData("size")]
    [InlineData("initializer")]
    [InlineData("reference_assembly")]
    [InlineData("not_sealed")]
    public void Unsupported_value_shapes_return_diagnostics_and_no_partial_catalog(string shape)
    {
        var builder = Builder(); var valueBase = Bases(builder);
        EntityHandle parent = shape == "class_base" ? MetadataTokens.TypeDefinitionHandle(2) : valueBase;
        if (shape == "enum") parent = builder.Type("System", "Enum", baseType: valueBase, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var root = builder.Type("Example", "Unsupported", baseType: parent, arity: shape == "generic" ? 1 : 0,
            attributes: shape == "explicit" ? TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout
                : shape == "not_sealed" ? Struct & ~TypeAttributes.Sealed : Struct);
        if (shape == "interface") builder.Implements(root, MetadataTokens.TypeDefinitionHandle(2));
        if (shape == "packing") builder.TypeLayout(root, 4, 0);
        if (shape == "size") builder.TypeLayout(root, 0, 16);
        if (shape == "initializer") builder.Method(".cctor", [0, 0, 1]);
        if (shape == "reference_assembly") builder.MarkReferenceAssembly();
        var result = Build(builder, root);
        Assert.False(result.Success); Assert.Null(result.Catalog);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("pointer")]
    [InlineData("byref")]
    [InlineData("value_array")]
    [InlineData("primitive")]
    [InlineData("offset")]
    [InlineData("inline_cycle")]
    [InlineData("class_as_value")]
    [InlineData("value_as_class")]
    public void Invalid_storage_reports_inclusion_and_fails_closed(string shape)
    {
        var builder = Builder(); var valueBase = Bases(builder);
        var root = builder.Type("Example", "Unsupported", baseType: valueBase, attributes: Struct);
        byte[] signature = shape switch
        {
            "pointer" => [0x06, 0x0f, 0x08], "byref" => [0x06, 0x10, 0x08], "primitive" => [0x06, 0x0a],
            "inline_cycle" => FieldType(root), "class_as_value" => FieldType(MetadataTokens.TypeDefinitionHandle(2)),
            "value_as_class" => FieldType(root, false), "value_array" => [0x06, 0x1d, .. FieldType(root)[1..]],
            _ => [0x06, 0x08],
        };
        var field = builder.FieldWithAttributes("UnsupportedField", shape == "static" ? FieldAttributes.Public | FieldAttributes.Static : FieldAttributes.Public, signature);
        if (shape == "offset") builder.FieldOffset(field, 0);
        var result = Build(builder, root);
        Assert.False(result.Success); Assert.Null(result.Catalog);
        Assert.All(result.Diagnostics, d => Assert.NotNull(d.Location));
        if (shape is not ("inline_cycle" or "value_as_class"))
            Assert.Contains("UnsupportedField", Assert.Single(result.Diagnostics).Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_budget_is_shared_between_inline_values_and_referenced_classes(bool excess)
    {
        var builder = Builder(); var valueBase = Bases(builder);
        var node = builder.Type("Example", "Node", baseType: MetadataTokens.TypeDefinitionHandle(2));
        for (int i = 0; i < 2048; i++) builder.Field("Number" + i, 0x06, 0x08);
        var root = builder.Type("Example", "Value", baseType: valueBase, attributes: Struct);
        builder.Field("Node", FieldType(node, false));
        for (int i = 1; i < 2048 + (excess ? 1 : 0); i++) builder.Field("Number" + i, 0x06, 0x08);
        var result = Build(builder, root);
        Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Metadata_budget_includes_both_runtime_bases_and_value_declarations(bool excess)
    {
        var builder = Builder(); var valueBase = Bases(builder);
        var root = builder.Type("Example", "Value", baseType: valueBase, attributes: Struct);
        // Three owner/initializer visits for each of Object, ValueType and Value.
        for (int i = 0; i < ObjectModelLimits.MaximumMetadataMembers - 9 + (excess ? 1 : 0); i++)
            builder.Method("Method" + i, [0, 0, 1]);
        var result = Build(builder, root);
        Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Type_budget_includes_discovered_reference_types(bool excess)
    {
        var builder = Builder(); var valueBase = Bases(builder);
        var root = builder.Type("Example", "Value", baseType: valueBase, attributes: Struct);
        int count = ObjectModelLimits.MaximumTypes - 3 + (excess ? 1 : 0);
        for (int i = 0; i < count; i++) builder.Field("Reference" + i, FieldType(MetadataTokens.TypeDefinitionHandle(5 + i), false));
        for (int i = 0; i < count; i++) builder.Type("Example", "Reference" + i, baseType: MetadataTokens.TypeDefinitionHandle(2));
        var result = Build(builder, root);
        Assert.Equal(!excess, result.Success);
        if (excess)
        {
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticIds.TranslationLimit, diagnostic.Id);
            Assert.Contains("Observed 257/256 discovered identities", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("257 distinct nominal declarations, 0 closed generic identities, 0 vector identities", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("Partial closure only", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("Per-image identities:", diagnostic.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Forwarded_runtime_base_retains_the_defining_assembly_identity()
    {
        var core = Builder("Core"); Bases(core);
        var facade = Builder("Facade"); var coreRef = facade.Reference(new("Core", new Version(1, 0, 0, 0)));
        facade.Forward("System", "Object", coreRef); facade.Forward("System", "ValueType", coreRef);
        var app = Builder("App");
        var baseRef = app.TypeReference("System", "ValueType", app.Reference(new("Facade", new Version(1, 0, 0, 0))));
        var root = app.Type("Example", "Value", baseType: baseRef, attributes: Struct);
        app.Field("Number", 0x06, 0x08);
        using var files = new ResolverFiles();
        var corePath = files.Write("core.dll", core.Build()); var facadePath = files.Write("facade.dll", facade.Build());
        var inputPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var input = resolver.OpenInput(inputPath).Value!; var library = resolver.OpenInput(facadePath).Value!;
        var result = ValueModelBuilder.BuildCatalog(resolver, [resolver.ResolveType(input, root).Value!], library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(resolver.OpenInput(corePath).Value!.ContentHash, result.Catalog!.ValueBase.ContentHash);
        Assert.NotEqual(library.ContentHash, result.Catalog.ValueBase.ContentHash);
    }

    [Fact]
    public void Selected_runtime_identity_cannot_be_substituted_by_a_same_named_base()
    {
        var core = Builder("Core"); Bases(core);
        var spoof = Builder("Spoof"); var otherBase = Bases(spoof);
        var root = spoof.Type("Example", "Value", baseType: otherBase, attributes: Struct);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("spoof.dll", spoof.Build())).Value!;
        var library = resolver.OpenInput(files.Write("core.dll", core.Build())).Value!;
        var result = ValueModelBuilder.BuildCatalog(resolver, [resolver.ResolveType(input, root).Value!], library);
        Assert.False(result.Success);
        Assert.Contains("selected System.ValueType", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }
}
