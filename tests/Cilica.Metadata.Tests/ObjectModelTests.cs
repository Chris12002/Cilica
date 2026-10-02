using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectModelTests
{
    private static ResolutionAssemblyBuilder Builder(string name = "Objects") => new(new(name, new Version(1, 0, 0, 0)));
    private static byte[] Reference(EntityHandle type, bool valueType = false)
    {
        var blob = new BlobBuilder(); blob.WriteByte(0x06); blob.WriteByte(valueType ? (byte)0x11 : (byte)0x12);
        blob.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) * 4 + (type.Kind == HandleKind.TypeReference ? 1 : 0));
        return blob.ToArray();
    }
    private static ObjectModelResult Build(ResolutionAssemblyBuilder builder, TypeDefinitionHandle root)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        return ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, root).Value!, image);
    }
    private static ObjectModel Model()
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "Node", baseType: objectBase);
        builder.Field("Value", 0x06, 0x08);
        builder.FieldWithAttributes("Next", FieldAttributes.Public | FieldAttributes.InitOnly, Reference(root));
        var result = Build(builder, root); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Model!;
    }

    [Fact]
    public void Detached_model_preserves_field_identity_order_and_reference_slots_after_disposal()
    {
        var model = Model(); ObjectModelValidator.Validate(model);
        Assert.Equal(2, model.Types.Length);
        var node = model.Types.Single(t => ObjectTypeId.From(t.Source) == model.Entry);
        Assert.Equal("Example.Node", node.Source.Name); Assert.Equal(model.ObjectBase, node.BaseType);
        Assert.Equal(new[] { "Value", "Next" }, node.Fields.Select(f => f.Name));
        Assert.Equal(new[] { 0x04000001, 0x04000002 }, node.Fields.Select(f => f.Id.Token));
        Assert.All(node.Fields, f => Assert.Equal(model.Entry, f.Id.Owner));
        Assert.IsType<ObjectStorage.Int32>(node.Fields[0].Storage);
        Assert.Equal(model.Entry, Assert.IsType<ObjectStorage.Reference>(node.Fields[1].Storage).Type);
        Assert.Equal(FieldAttributes.Public | FieldAttributes.InitOnly, node.Fields[1].Attributes);
        Assert.Equal(new[] { 1 }, node.ReferenceSlots);
        Assert.Contains("Example.Node", JsonSerializer.Serialize(model), StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_cycles_and_duplicate_field_names_keep_distinct_declarations()
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var first = builder.Type("Example", "First", baseType: objectBase);
        var secondHandle = MetadataTokens.TypeDefinitionHandle(4);
        builder.Field("Link", Reference(first)); builder.Field("Link", Reference(secondHandle));
        var second = builder.Type("Example", "Second", baseType: objectBase);
        builder.Field("Back", Reference(first)); builder.Field("Object", 0x06, 0x1c);
        var result = Build(builder, first); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(3, result.Model!.Types.Length);
        Assert.Equal(secondHandle, second);
        Assert.All(result.Model.Types.Where(t => !t.Fields.IsEmpty), t => Assert.Equal(new[] { 0, 1 }, t.ReferenceSlots));
        ObjectModelValidator.Validate(result.Model);
    }

    [Fact]
    public void Same_named_cross_assembly_classes_and_forwarded_object_base_use_content_identity()
    {
        var core = Builder("Core"); core.Type("System", "Object");
        var facade = Builder("Facade"); facade.Forward("System", "Object", facade.Reference(new("Core", new Version(1, 0, 0, 0))));
        var first = Builder("First");
        var firstObject = first.TypeReference("System", "Object", first.Reference(new("Facade", new Version(1, 0, 0, 0))));
        var firstRoot = first.Type("Example", "Node", baseType: firstObject);
        first.Field("Other", Reference(first.TypeReference("Example", "Node", first.Reference(new("Second", new Version(1, 0, 0, 0))))));
        var second = Builder("Second");
        var secondObject = second.TypeReference("System", "Object", second.Reference(new("Core", new Version(1, 0, 0, 0))));
        second.Type("Example", "Node", baseType: secondObject);
        second.Field("Back", Reference(second.TypeReference("Example", "Node", second.Reference(new("First", new Version(1, 0, 0, 0))))));
        using var files = new ResolverFiles();
        files.Write("core.dll", core.Build()); string libraryPath = files.Write("facade.dll", facade.Build());
        string firstPath = files.Write("first.dll", first.Build()); files.Write("second.dll", second.Build());
        ObjectModel model;
        using (var resolver = files.Open("refs"))
        {
            var image = resolver.OpenInput(firstPath).Value!;
            var result = ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, firstRoot).Value!, resolver.OpenInput(libraryPath).Value!);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics)); model = result.Model!;
        }
        var nodes = model.Types.Where(t => t.Source.Name == "Example.Node").ToArray();
        Assert.Equal(2, nodes.Length); Assert.Equal(nodes[0].Source.Token, nodes[1].Source.Token);
        Assert.NotEqual(nodes[0].Source.ContentHash, nodes[1].Source.ContentHash);
        Assert.All(nodes, node => Assert.Equal(model.ObjectBase, node.BaseType));
        ObjectModelValidator.Validate(model);
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("abstract")]
    [InlineData("interface")]
    [InlineData("implements")]
    [InlineData("sequential")]
    [InlineData("explicit")]
    [InlineData("packing")]
    [InlineData("class_size")]
    [InlineData("missing_base")]
    [InlineData("valuetype_base")]
    [InlineData("initializer")]
    [InlineData("module_initializer")]
    [InlineData("reference_assembly")]
    public void Unsupported_type_shapes_never_return_partial_models(string shape)
    {
        var builder = Builder();
        if (shape == "module_initializer") builder.Method(".cctor", [0, 0, 1]);
        var objectBase = builder.Type("System", "Object");
        EntityHandle baseType = shape == "missing_base" ? default : objectBase;
        if (shape == "valuetype_base") baseType = builder.Specification(Reference(objectBase, valueType: true)[1..]);
        var attributes = TypeAttributes.Public | (shape switch
        {
            "abstract" => TypeAttributes.Abstract, "interface" => TypeAttributes.Interface,
            "sequential" => TypeAttributes.SequentialLayout, "explicit" => TypeAttributes.ExplicitLayout, _ => 0,
        });
        var root = builder.Type("Example", "Bad", baseType: baseType, arity: shape == "generic" ? 1 : 0, attributes: attributes);
        if (shape == "implements") builder.Implements(root, objectBase);
        if (shape == "packing") builder.TypeLayout(root, 4, 0);
        if (shape == "class_size") builder.TypeLayout(root, 0, 16);
        if (shape == "initializer") builder.Method(".cctor", [0, 0, 1]);
        if (shape == "reference_assembly") builder.MarkReferenceAssembly();
        var result = Build(builder, root);
        Assert.Null(result.Model); Assert.False(result.Success);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("literal")]
    [InlineData("marshal")]
    [InlineData("rva")]
    [InlineData("invalid_access")]
    [InlineData("offset")]
    [InlineData("string")]
    [InlineData("array")]
    [InlineData("pointer")]
    [InlineData("byref")]
    [InlineData("valuetype")]
    [InlineData("modifier")]
    public void Unsupported_field_storage_reports_the_declaration(string shape)
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "Bad", baseType: objectBase);
        var attributes = shape switch
        {
            "static" => FieldAttributes.Public | FieldAttributes.Static,
            "literal" => FieldAttributes.Public | FieldAttributes.Literal,
            "marshal" => FieldAttributes.Public | FieldAttributes.HasFieldMarshal,
            "rva" => FieldAttributes.Public | FieldAttributes.HasFieldRVA,
            "invalid_access" => (FieldAttributes)7, _ => FieldAttributes.Public,
        };
        byte[] signature = shape switch
        {
            "string" => [0x06, 0x0e], "array" => [0x06, 0x1d, 0x08], "pointer" => [0x06, 0x0f, 0x08],
            "byref" => [0x06, 0x10, 0x08], "valuetype" => Reference(root, valueType: true),
            "modifier" => [0x06, 0x1f, 0x08, 0x08], _ => [0x06, 0x08],
        };
        var field = builder.FieldWithAttributes("BadStorage", attributes, signature);
        if (shape == "offset") builder.FieldOffset(field, 0);
        var result = Build(builder, root); Assert.Null(result.Model);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains(diagnostic.Id, new[] { DiagnosticIds.ExecutionUnsupported, DiagnosticIds.ExecutionSignatureUnsupported });
        Assert.Contains("BadStorage", diagnostic.Location, StringComparison.Ordinal);
        Assert.Contains("0x04000001", diagnostic.Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fields")]
    [InlineData("sealed")]
    [InlineData("parent")]
    public void Selected_object_base_must_have_the_profile_shape(string shape)
    {
        var builder = Builder();
        EntityHandle parent = shape == "parent" ? builder.Type("Example", "OtherBase") : default;
        var objectBase = builder.Type("System", "Object", baseType: parent,
            attributes: TypeAttributes.Public | (shape == "sealed" ? TypeAttributes.Sealed : 0));
        if (shape == "fields") builder.Field("Hidden", 0x06, 0x08);
        var root = builder.Type("Example", "Root", baseType: objectBase);
        var result = Build(builder, root); Assert.Null(result.Model);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(result.Diagnostics).Id);
        Assert.Contains("fieldless System.Object", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_limit_accepts_exact_boundary_and_rejects_excess(bool excess)
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "ManyFields", baseType: objectBase);
        for (int i = 0; i < ObjectModelLimits.MaximumFields + (excess ? 1 : 0); i++) builder.Field("Field" + i, 0x06, 0x08);
        var result = Build(builder, root); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        else Assert.Equal(ObjectModelLimits.MaximumFields, result.Model!.Types.Sum(t => t.Fields.Length));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Type_limit_counts_the_base_and_pending_field_targets(bool excess)
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "Root", baseType: objectBase);
        int count = ObjectModelLimits.MaximumTypes - 2 + (excess ? 1 : 0);
        for (int i = 0; i < count; i++) builder.Field("Target" + i, Reference(MetadataTokens.TypeDefinitionHandle(4 + i)));
        for (int i = 0; i < count; i++) builder.Type("Example", "Target" + i, baseType: objectBase);
        var result = Build(builder, root); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        else Assert.Equal(ObjectModelLimits.MaximumTypes, result.Model!.Types.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Metadata_work_limit_is_aggregate_across_root_and_base(bool excess)
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "ManyMethods", baseType: objectBase);
        // Each snapshot inspects its owner, type initializer list and module initializer list.
        int count = ObjectModelLimits.MaximumMetadataMembers - 6 + (excess ? 1 : 0);
        for (int i = 0; i < count; i++) builder.Method("Method" + i, [0, 0, 1]);
        var result = Build(builder, root); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Aggregate_field_budget_cannot_be_reset_by_another_type(bool excess)
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "First", baseType: objectBase);
        builder.Field("Next", Reference(MetadataTokens.TypeDefinitionHandle(4)));
        for (int i = 1; i < 2048; i++) builder.Field("First" + i, 0x06, 0x08);
        builder.Type("Example", "Second", baseType: objectBase);
        for (int i = 0; i < 2048 + (excess ? 1 : 0); i++) builder.Field("Second" + i, 0x06, 0x08);
        var result = Build(builder, root); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Caller_constructed_models_obey_the_aggregate_field_budget(bool excess)
    {
        var model = Model(); var node = model.Types.Single(t => ObjectTypeId.From(t.Source) == model.Entry);
        var extraId = model.Entry with { Token = 0x02000009 };
        ObjectField Field(ObjectTypeId owner, int row) => new(new(owner, 0x04000000 | row), "Field" + row, FieldAttributes.Public, new ObjectStorage.Int32());
        var first = Enumerable.Range(1, 2048).Select(row => Field(model.Entry, row)).ToImmutableArray();
        first = first.SetItem(0, first[0] with { Storage = new ObjectStorage.Reference(extraId) });
        var extra = node with { Source = node.Source with { Token = extraId.Token },
            Fields = [.. Enumerable.Range(2049, 2048 + (excess ? 1 : 0)).Select(row => Field(extraId, row))] };
        model = model with { Types = [model.Types.Single(t => ObjectTypeId.From(t.Source) == model.ObjectBase), node with { Fields = first }, extra] };
        if (excess) Assert.Contains("aggregate field limit", Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(model)).Message, StringComparison.Ordinal);
        else ObjectModelValidator.Validate(model);
    }

    [Fact]
    public void Caller_constructed_models_obey_the_type_budget()
    {
        var model = Model(); var node = model.Types.Single(t => ObjectTypeId.From(t.Source) == model.Entry);
        model = model with { Types = model.Types.AddRange(Enumerable.Range(10, 255)
            .Select(row => node with { Source = node.Source with { Token = 0x02000000 | row }, Fields = [] })) };
        Assert.Contains("type count", Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(model)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("entry_hash")]
    [InlineData("entry_token")]
    [InlineData("duplicate_type")]
    [InlineData("default_types")]
    [InlineData("unrelated_type")]
    [InlineData("missing_target")]
    [InlineData("field_owner")]
    [InlineData("field_token")]
    [InlineData("field_order")]
    [InlineData("default_fields")]
    [InlineData("storage")]
    [InlineData("base")]
    [InlineData("base_fields")]
    [InlineData("base_sealed")]
    [InlineData("attributes")]
    [InlineData("cross_type_field_token")]
    [InlineData("module_type")]
    public void Caller_forged_models_are_rejected(string corruption)
    {
        var model = Model(); var node = model.Types.Single(t => ObjectTypeId.From(t.Source) == model.Entry);
        var objectBase = model.Types.Single(t => ObjectTypeId.From(t.Source) == model.ObjectBase);
        switch (corruption)
        {
            case "entry_hash": model = model with { Entry = model.Entry with { ContentHash = new string('z', 64) } }; break;
            case "entry_token": model = model with { Entry = model.Entry with { Token = 0x06000003 } }; break;
            case "duplicate_type": model = model with { Types = model.Types.Add(node) }; break;
            case "default_types": model = model with { Types = default }; break;
            case "unrelated_type": model = model with { Types = model.Types.Add(node with
                { Source = node.Source with { Token = 0x02000009 }, Fields = [] }) }; break;
            case "missing_target": node = node with { Fields = node.Fields.SetItem(1, node.Fields[1] with
                { Storage = new ObjectStorage.Reference(model.Entry with { Token = 0x02000009 }) }) }; break;
            case "field_owner": node = node with { Fields = node.Fields.SetItem(0, node.Fields[0] with
                { Id = node.Fields[0].Id with { Owner = model.ObjectBase } }) }; break;
            case "field_token": node = node with { Fields = node.Fields.SetItem(0, node.Fields[0] with
                { Id = node.Fields[0].Id with { Token = 0x06000001 } }) }; break;
            case "field_order": node = node with { Fields = [.. node.Fields.Reverse()] }; break;
            case "default_fields": node = node with { Fields = default }; break;
            case "storage": node = node with { Fields = node.Fields.SetItem(0, node.Fields[0] with { Storage = new UnsupportedStorage() }) }; break;
            case "base": node = node with { BaseType = model.Entry }; break;
            case "base_fields": objectBase = objectBase with { Fields = node.Fields }; break;
            case "base_sealed": objectBase = objectBase with { Attributes = objectBase.Attributes | TypeAttributes.Sealed }; break;
            case "attributes": node = node with { Attributes = node.Attributes | TypeAttributes.ExplicitLayout }; break;
            case "module_type":
                var moduleId = model.Entry with { Token = 0x02000001 };
                node = node with { Source = node.Source with { Token = moduleId.Token }, Fields =
                    [.. node.Fields.Select(f => f with { Id = f.Id with { Owner = moduleId },
                        Storage = f.Storage is ObjectStorage.Reference ? new ObjectStorage.Reference(moduleId) : f.Storage })] };
                model = model with { Entry = moduleId }; break;
            case "cross_type_field_token":
                var extraId = model.Entry with { Token = 0x02000009 };
                var extra = node with { Source = node.Source with { Token = extraId.Token }, Fields =
                    [node.Fields[0] with { Id = node.Fields[0].Id with { Owner = extraId } }] };
                node = node with { Fields = node.Fields.SetItem(1, node.Fields[1] with { Storage = new ObjectStorage.Reference(extraId) }) };
                model = model with { Types = [objectBase, node, extra] }; break;
        }
        if (!new[] { "entry_hash", "entry_token", "duplicate_type", "default_types", "unrelated_type", "cross_type_field_token" }.Contains(corruption))
            model = model with { Types = [objectBase, node] };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(model));
    }

    private sealed record UnsupportedStorage : ObjectStorage;
}
