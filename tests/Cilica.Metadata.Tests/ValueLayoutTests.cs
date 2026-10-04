using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueLayoutTests
{
    private static readonly string Hash = new('a', 64);
    private static ObjectTypeId Id(int row) => new(Hash, 0x02000000 | row);
    private static TypeSource Source(int row) => new("Values", Hash, Id(row).Token, "Value" + row);
    private static ObjectField Field(int owner, int row, ObjectStorage storage) =>
        new(new(Id(owner), 0x04000000 | row), "Field" + row, FieldAttributes.Public, storage);
    private static ValueTypeLayout Value(int row, params ObjectField[] fields) =>
        new(Source(row), TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout, [.. fields]);
    private static ValueTypeCatalog Catalog(ImmutableArray<ValueTypeLayout> types, params ObjectTypeId[] roots) =>
        new([.. roots], Id(3), types, new([Id(2)], Id(2), [new(Source(2), null, TypeAttributes.Public, [])]));
    private static ValueTypeCatalog Nested() => Catalog([
        Value(4, Field(4, 1, new ObjectStorage.Int32()), Field(4, 2, new ObjectStorage.Reference(Id(2)))),
        Value(5, Field(5, 3, new ObjectStorage.Value(Id(4))), Field(5, 4, new ObjectStorage.Value(Id(4))))
    ], Id(5));

    [Fact]
    public void Repeated_nested_values_keep_distinct_copy_and_reference_paths()
    {
        var catalog = Nested(); var plans = ValueLayoutPlanner.Build(catalog);
        var parent = plans.Single(p => p.Type == Id(5));
        Assert.Equal(6, parent.ExpandedFields); Assert.Equal(2, parent.Depth);
        Assert.Equal(4, parent.Slots.Length); Assert.Equal(2, parent.References.Length);
        Assert.Equal(new[] { 3, 3, 4, 4 }, parent.Slots.Select(s => s.Fields[0].Token & 0xffffff));
        Assert.Equal(new[] { 1, 2, 1, 2 }, parent.Slots.Select(s => s.Fields[1].Token & 0xffffff));
        Assert.All(parent.References, p => Assert.Equal(new ObjectStorage.Reference(Id(2)), p.Storage));
        Assert.All(parent.Slots, p => { Assert.Equal(Id(5), p.Fields[0].Owner); Assert.Equal(Id(4), p.Fields[1].Owner); });
        // Plans are derived from immutable input, with no mutable layout cache.
        Assert.Equal(4, ValueLayoutPlanner.Build(catalog).Single(p => p.Type == Id(5)).Slots.Length);
    }

    [Fact]
    public void Empty_values_retain_identity_and_consume_inline_expansion_budget()
    {
        var plan = ValueLayoutPlanner.Build(Catalog([
            Value(4), Value(5, Field(5, 1, new ObjectStorage.Value(Id(4))))
        ], Id(5)));
        Assert.Empty(plan[0].Slots); Assert.Empty(plan[1].Slots);
        Assert.Equal(0, plan[0].ExpandedFields); Assert.Equal(1, plan[1].ExpandedFields);
        Assert.NotEqual(plan[0].Type, plan[1].Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inline_cycles_are_rejected_without_recursion(bool mutual)
    {
        var catalog = mutual ? Catalog([
            Value(4, Field(4, 1, new ObjectStorage.Value(Id(5)))),
            Value(5, Field(5, 2, new ObjectStorage.Value(Id(4))))
        ], Id(4)) : Catalog([Value(4, Field(4, 1, new ObjectStorage.Value(Id(4))))], Id(4));
        Assert.Contains("recursive inline containment", Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inline_depth_has_an_exact_boundary(bool excess)
    {
        int count = ValueTypeLimits.MaximumDepth + (excess ? 1 : 0);
        var types = Enumerable.Range(4, count).Select(row => Value(row, Field(row, row,
            row == 4 ? new ObjectStorage.Int32() : new ObjectStorage.Value(Id(row - 1))))).ToImmutableArray();
        var catalog = Catalog(types, Id(count + 3));
        if (excess) Assert.Contains("depth limit", Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog)).Message, StringComparison.Ordinal);
        else Assert.Equal(count, ValueLayoutPlanner.Build(catalog)[^1].Slots.Single().Fields.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Small_declaration_graph_cannot_expand_exponentially(bool empty)
    {
        var types = ImmutableArray.CreateBuilder<ValueTypeLayout>();
        types.Add(empty ? Value(4) : Value(4, Field(4, 1, new ObjectStorage.Int32())));
        for (int row = 5; row < 24; ++row)
            types.Add(Value(row, Field(row, row * 2, new ObjectStorage.Value(Id(row - 1))),
                Field(row, row * 2 + 1, new ObjectStorage.Value(Id(row - 1)))));
        Assert.Contains(empty ? "expanded field limit" : "limit", Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(Catalog(types.ToImmutable(), Id(23)))).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_storage_is_bounded_independently_of_inline_field_count(bool excess)
    {
        int width = excess ? 127 : 126;
        var types = ImmutableArray.CreateBuilder<ValueTypeLayout>();
        types.Add(Value(4, [.. Enumerable.Range(1, width).Select(row => Field(4, row, new ObjectStorage.Int32()))]));
        for (int row = 5; row < 68; row++)
            types.Add(Value(row, Field(row, width + row, new ObjectStorage.Value(Id(row - 1)))));
        var catalog = Catalog(types.ToImmutable(), Id(67));
        if (excess) Assert.Contains("path component limit", Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog)).Message, StringComparison.Ordinal);
        else Assert.Equal(262080, ValueLayoutPlanner.Build(catalog).Sum(p => p.Slots.Sum(s => s.Fields.Length)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Combined_type_limit_includes_object_and_value_bases(bool excess)
    {
        int count = ObjectModelLimits.MaximumTypes - 2 + (excess ? 1 : 0);
        var types = Enumerable.Range(4, count).Select(row => Value(row)).ToImmutableArray();
        var catalog = Catalog(types, [.. types.Select(t => ObjectTypeId.From(t.Source))]);
        if (excess) Assert.Contains("combined type limit", Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog)).Message, StringComparison.Ordinal);
        else Assert.Equal(count, ValueLayoutPlanner.Build(catalog).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_limit_has_an_exact_boundary(bool excess)
    {
        var type = Value(4, [.. Enumerable.Range(1, ObjectModelLimits.MaximumFields + (excess ? 1 : 0))
            .Select(row => Field(4, row, new ObjectStorage.Int32()))]);
        var catalog = Catalog([type], Id(4));
        if (excess) Assert.Contains("combined field limit", Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog)).Message, StringComparison.Ordinal);
        else Assert.Equal(ObjectModelLimits.MaximumFields, ValueLayoutPlanner.Build(catalog)[0].Slots.Length);
    }

    [Theory]
    [InlineData("default_types")]
    [InlineData("default_roots")]
    [InlineData("missing_root")]
    [InlineData("duplicate_root")]
    [InlineData("duplicate_type")]
    [InlineData("unrelated")]
    [InlineData("class_collision")]
    [InlineData("base_collision")]
    [InlineData("base_hash")]
    [InlineData("missing_source")]
    [InlineData("explicit")]
    [InlineData("abstract")]
    [InlineData("not_sealed")]
    [InlineData("default_fields")]
    [InlineData("static")]
    [InlineData("wrong_owner")]
    [InlineData("duplicate_field")]
    [InlineData("cross_type_field")]
    [InlineData("unknown_value")]
    [InlineData("value_as_reference")]
    [InlineData("reference_as_value")]
    [InlineData("null_storage")]
    public void Forged_layouts_fail_before_producing_a_plan(string shape)
    {
        var catalog = Nested(); var first = catalog.Types[0]; var second = catalog.Types[1];
        var originalFirst = first; var originalSecond = second;
        switch (shape)
        {
            case "default_types": catalog = catalog with { Types = default }; break;
            case "default_roots": catalog = catalog with { Roots = default }; break;
            case "missing_root": catalog = catalog with { Roots = [Id(9)] }; break;
            case "duplicate_root": catalog = catalog with { Roots = [Id(5), Id(5)] }; break;
            case "duplicate_type": catalog = catalog with { Types = catalog.Types.Add(first) }; break;
            case "unrelated": catalog = catalog with { Types = catalog.Types.Add(Value(9)) }; break;
            case "class_collision": catalog = catalog with { Types = catalog.Types.Add(Value(2)) }; break;
            case "base_collision": catalog = catalog with { ValueBase = Id(2) }; break;
            case "base_hash": catalog = catalog with { ValueBase = new("invalid", Id(3).Token) }; break;
            case "missing_source": first = first with { Source = null! }; break;
            case "explicit": first = first with { Attributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout }; break;
            case "abstract": first = first with { Attributes = first.Attributes | TypeAttributes.Abstract }; break;
            case "not_sealed": first = first with { Attributes = first.Attributes & ~TypeAttributes.Sealed }; break;
            case "default_fields": first = first with { Fields = default }; break;
            case "static": first = first with { Fields = [first.Fields[0] with { Attributes = FieldAttributes.Public | FieldAttributes.Static }] }; break;
            case "wrong_owner": first = first with { Fields = [Field(5, 1, new ObjectStorage.Int32())] }; break;
            case "duplicate_field": first = first with { Fields = [first.Fields[0], first.Fields[0]] }; break;
            case "cross_type_field": second = second with { Fields = [Field(5, 1, new ObjectStorage.Value(Id(4)))] }; break;
            case "unknown_value": first = first with { Fields = [Field(4, 1, new ObjectStorage.Value(Id(9)))] }; break;
            case "value_as_reference": first = first with { Fields = [Field(4, 1, new ObjectStorage.Reference(Id(4)))] }; break;
            case "reference_as_value": first = first with { Fields = [Field(4, 1, new ObjectStorage.Value(Id(2)))] }; break;
            case "null_storage": first = first with { Fields = [Field(4, 1, null!)] }; break;
        }
        if (first != originalFirst || second != originalSecond) catalog = catalog with { Types = [first, second] };
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog));
    }

    [Fact]
    public void New_storage_kind_is_rejected_by_existing_executable_object_catalogs()
    {
        var catalog = Nested().References;
        catalog = catalog with { Roots = [Id(4)], Types = catalog.Types.Add(new(Source(4), Id(2), TypeAttributes.Public,
            [Field(4, 1, new ObjectStorage.Value(Id(5)))])) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
    }
}
