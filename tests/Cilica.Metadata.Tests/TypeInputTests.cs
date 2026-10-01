using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class TypeInputTests
{
    private static ResolutionAssemblyBuilder Builder() => new(new("Types", new Version(1, 0, 0, 0)));

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Owner_depth_is_bounded_before_formatting_a_nested_name(int count, bool success)
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        TypeDefinitionHandle root = default;
        for (int i = 0; i < count; i++) root = builder.Type(i == 0 ? "Example" : "", "Nested" + i, parent: root, baseType: objectBase);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, root).Value!, image);
        Assert.Equal(success, result.Success);
        if (!success) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Nested_type_inside_a_generic_owner_cannot_bypass_the_profile()
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var parent = builder.Type("Example", "Parent", baseType: objectBase, arity: 1);
        var root = builder.Type("", "Child", parent: parent, baseType: objectBase);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var read = resolver.ReadTypeInput(image, root); Assert.True(read.Success);
        Assert.Equal(1, read.Value!.OwnerGenericArity);
        var result = ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, root).Value!, image);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Nil_or_nonexistent_type_rows_are_diagnostics(int row)
    {
        var builder = Builder(); builder.Type("System", "Object");
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = resolver.ReadTypeInput(image, MetadataTokens.TypeDefinitionHandle(row));
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.InvalidSignature, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("wrong_header")]
    [InlineData("truncated_reference")]
    public void Malformed_field_signatures_never_escape_as_partial_input(string shape)
    {
        var builder = Builder(); var root = builder.Type("Example", "Broken");
        builder.Field("Broken", shape switch { "empty" => [], "wrong_header" => [0, 0x08], _ => [0x06, 0x12] });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = resolver.ReadTypeInput(image, root);
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.InvalidSignature, Assert.Single(result.Diagnostics).Id);
        Assert.Contains("0x04000001", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_limits_bound_fields_and_member_work_independently()
    {
        var builder = Builder(); var root = builder.Type("Example", "Bounded"); builder.Field("Value", 0x06, 0x08);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        Assert.True(resolver.ReadTypeInput(image, root, new(MaximumFields: 1, MaximumMembers: 4)).Success);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadTypeInput(image, root, new(MaximumFields: 0)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadTypeInput(image, root, new(MaximumMembers: 3)).Diagnostics).Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadTypeInput(image, root, new(MaximumFields: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadTypeInput(image, root, new(MaximumMembers: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadTypeInput(image, root, new(MaximumOwnerDepth: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadTypeInput(image, root, new(MaximumOwnerDepth: 65)));
    }

    [Fact]
    public void Foreign_roots_are_rejected_even_when_they_equal_the_selected_object_identity()
    {
        var builder = Builder(); var root = builder.Type("System", "Object");
        using var files = new ResolverFiles(); string path = files.Write("input.dll", builder.Build());
        using var first = files.Open(); using var second = files.Open();
        var firstImage = first.OpenInput(path).Value!; var secondImage = second.OpenInput(path).Value!;
        var foreign = first.ResolveType(firstImage, root).Value!;
        Assert.Throws<ArgumentException>(() => second.ReadTypeInput(firstImage, root));
        Assert.Throws<ArgumentException>(() => ObjectModelBuilder.Build(second, foreign, secondImage));
        Assert.Throws<ArgumentException>(() => ObjectModelBuilder.Build(second, second.ResolveType(secondImage, root).Value!, firstImage));
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.ReadTypeInput(firstImage, root));
    }

    [Fact]
    public void Missing_reference_and_transitive_unsupported_type_retain_inclusion_evidence()
    {
        var builder = Builder(); var objectBase = builder.Type("System", "Object");
        var root = builder.Type("Example", "Root", baseType: objectBase);
        // FIELD CLASS with a TypeDefOrRef-coded row 4.
        builder.Field("Child", 0x06, 0x12, 0x10);
        builder.Type("Example", "Child", baseType: objectBase, arity: 1);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, root).Value!, image);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
        Assert.Contains("via field 0x04000001", diagnostic.Location, StringComparison.Ordinal);
        Assert.Contains("0x02000004", diagnostic.Location, StringComparison.Ordinal);

        var missing = Builder(); var missingObject = missing.Type("System", "Object");
        var missingRoot = missing.Type("Example", "Root", baseType: missingObject);
        var reference = missing.Reference(new("Absent", new Version(1, 0, 0, 0)));
        missing.TypeReference("Example", "Absent", reference);
        missing.Field("Absent", 0x06, 0x12, 0x05);
        var missingImage = resolver.OpenInput(files.Write("missing.dll", missing.Build())).Value!;
        var failed = ObjectModelBuilder.Build(resolver, resolver.ResolveType(missingImage, missingRoot).Value!, missingImage);
        Assert.Equal(DiagnosticIds.AssemblyNotFound, Assert.Single(failed.Diagnostics).Id);
        Assert.Contains("0x04000001", Assert.Single(failed.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Null(failed.Model);
    }
}
