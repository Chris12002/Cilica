using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class TypeResolutionTests
{
    private static AssemblyIdentity Identity(string name) => new(name, new Version(1, 0, 0, 0));

    [Fact]
    public void Chained_facades_resolve_outer_and_nested_types_in_the_implementation()
    {
        using var files = new ResolverFiles();
        var implementation = new ResolutionAssemblyBuilder(Identity("Implementation"));
        TypeDefinitionHandle outer = implementation.Type("Sample", "Outer");
        TypeDefinitionHandle inner = implementation.Type("", "Inner", outer);
        files.Write("Impl.dll", implementation.Build());
        var second = new ResolutionAssemblyBuilder(Identity("Second"));
        second.Forward("Sample", "Outer", second.Reference(Identity("Implementation")));
        files.Write("Second.dll", second.Build());
        var first = new ResolutionAssemblyBuilder(Identity("First"));
        ExportedTypeHandle exported = first.Forward("Sample", "Outer", first.Reference(Identity("Second")));
        first.NestedExport("Inner", exported);
        string facadePath = files.Write("First.dll", first.Build());
        using var resolver = files.Open("refs");
        ResolvedAssembly facade = resolver.OpenInput(facadePath).Value!;

        ResolvedType resolvedOuter = resolver.ResolveType(facade, "Sample", "Outer").Value!;
        ResolvedType resolvedInner = resolver.ResolveType(facade, "Sample", "Outer", "Inner").Value!;

        Assert.Equal(Identity("Implementation"), resolvedOuter.Assembly.Identity);
        Assert.Equal(outer, resolvedOuter.Handle);
        Assert.Equal(Identity("Implementation"), resolvedInner.Assembly.Identity);
        Assert.Equal(inner, resolvedInner.Handle);
    }

    [Fact]
    public void Type_reference_scopes_resolve_through_forwarders_and_nested_definitions()
    {
        using var files = new ResolverFiles();
        var implementation = new ResolutionAssemblyBuilder(Identity("Implementation"));
        var outer = implementation.Type("Sample", "Outer");
        var inner = implementation.Type("", "Inner", outer);
        files.Write("Impl.dll", implementation.Build());
        var facade = new ResolutionAssemblyBuilder(Identity("Facade"));
        facade.Forward("Sample", "Outer", facade.Reference(Identity("Implementation")));
        files.Write("Facade.dll", facade.Build());
        var inputBuilder = new ResolutionAssemblyBuilder(Identity("Input"));
        var typeRef = inputBuilder.TypeReference("Sample", "Outer", inputBuilder.Reference(Identity("Facade")));
        var nestedRef = inputBuilder.TypeReference("", "Inner", typeRef);
        string inputPath = files.Write("Input.dll", inputBuilder.Build(), "input");
        using var resolver = files.Open("refs");
        ResolvedAssembly input = resolver.OpenInput(inputPath).Value!;

        var result = resolver.ResolveType(input, nestedRef);

        Assert.True(result.Success);
        Assert.Equal(Identity("Implementation"), result.Value!.Assembly.Identity);
        Assert.Equal(inner, result.Value.Handle);
    }

    [Fact]
    public void Current_module_typeref_and_typedef_need_no_reference_directory()
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(Identity("Input"));
        var type = builder.Type("Sample", "Local");
        var reference = builder.TypeReference("Sample", "Local", MetadataTokens.EntityHandle(0x00000001));
        string path = files.Write("Input.dll", builder.Build());
        using var resolver = files.Open();
        ResolvedAssembly input = resolver.OpenInput(path).Value!;

        Assert.Equal(type, resolver.ResolveType(input, reference).Value!.Handle);
        Assert.Equal(type, resolver.ResolveType(input, type).Value!.Handle);
    }

    [Fact]
    public void Equal_type_names_in_different_assemblies_keep_their_identity()
    {
        using var files = new ResolverFiles();
        foreach (string name in new[] { "First", "Second" })
        {
            var builder = new ResolutionAssemblyBuilder(Identity(name));
            builder.Type("Sample", "Equal");
            files.Write(name + ".dll", builder.Build());
        }
        using var resolver = files.Open("refs");

        ResolvedType first = resolver.ResolveType(resolver.ResolveAssembly(Identity("First")).Value!, "Sample", "Equal").Value!;
        ResolvedType second = resolver.ResolveType(resolver.ResolveAssembly(Identity("Second")).Value!, "Sample", "Equal").Value!;

        Assert.NotEqual(first, second);
        Assert.Equal(Identity("First"), first.Assembly.Identity);
        Assert.Equal(Identity("Second"), second.Assembly.Identity);
        Assert.False(resolver.ResolveType(first.Assembly, "sample", "Equal").Success);
    }

    [Fact]
    public void Missing_forwarder_target_retains_the_forwarding_chain()
    {
        using var files = new ResolverFiles();
        var facade = new ResolutionAssemblyBuilder(Identity("Facade"));
        facade.Forward("Sample", "Missing", facade.Reference(Identity("Missing")));
        string path = files.Write("Facade.dll", facade.Build());
        using var resolver = files.Open("refs");

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, "Sample", "Missing");

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.AssemblyNotFound, diagnostic.Id);
        Assert.Contains("Facade", diagnostic.Location!, StringComparison.Ordinal);
        Assert.Contains("Sample.Missing", diagnostic.Location!, StringComparison.Ordinal);
        Assert.Contains("Missing, Version=", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Forwarding_cycles_terminate_with_a_chain(bool self)
    {
        using var files = new ResolverFiles();
        var first = new ResolutionAssemblyBuilder(Identity("First"));
        first.Forward("Sample", "Cycle", first.Reference(Identity(self ? "First" : "Second")));
        string path = files.Write("First.dll", first.Build());
        if (!self)
        {
            var second = new ResolutionAssemblyBuilder(Identity("Second"));
            second.Forward("Sample", "Cycle", second.Reference(Identity("First")));
            files.Write("Second.dll", second.Build());
        }
        using var resolver = files.Open("refs");

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, "Sample", "Cycle");

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.TypeResolutionCycle, diagnostic.Id);
        Assert.Contains("First", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(" -> ", diagnostic.Message, StringComparison.Ordinal);
        if (!self) Assert.Contains("Second", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cyclic_typeref_scopes_are_diagnosed_without_recursion()
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(Identity("Input"));
        var reference = builder.TypeReference("Sample", "Cycle", MetadataTokens.TypeReferenceHandle(1));
        string path = files.Write("Input.dll", builder.Build());
        using var resolver = files.Open();

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, reference);

        Assert.Equal(DiagnosticIds.TypeResolutionCycle, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("definition-and-forwarder")]
    [InlineData("duplicate-forwarder")]
    public void Conflicting_type_definitions_or_exports_are_not_silently_selected(string shape)
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(Identity("Input"));
        if (shape != "duplicate-forwarder") builder.Type("Sample", "Duplicate");
        if (shape == "duplicate") builder.Type("Sample", "Duplicate");
        else
        {
            var target = builder.Reference(Identity("Missing"));
            builder.Forward("Sample", "Duplicate", target);
            if (shape == "duplicate-forwarder") builder.Forward("Sample", "Duplicate", target);
        }
        string path = files.Write("Input.dll", builder.Build());
        using var resolver = files.Open();

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, "Sample", "Duplicate");

        Assert.Equal(DiagnosticIds.TypeConflict, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Missing_nested_type_and_invalid_type_handles_are_diagnosed()
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(Identity("Input"));
        builder.Type("Sample", "Outer");
        string path = files.Write("Input.dll", builder.Build());
        using var resolver = files.Open();
        ResolvedAssembly input = resolver.OpenInput(path).Value!;

        Assert.Equal(DiagnosticIds.TypeNotFound, Assert.Single(resolver.ResolveType(input, "Sample", "Outer", "Missing").Diagnostics).Id);
        Assert.Equal(DiagnosticIds.ReferenceImageInvalid, Assert.Single(resolver.ResolveType(input, MetadataTokens.TypeReferenceHandle(99)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.ReferenceImageInvalid, Assert.Single(resolver.ResolveType(input, MetadataTokens.TypeDefinitionHandle(99)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(resolver.ResolveType(input, MetadataTokens.TypeSpecificationHandle(1)).Diagnostics).Id);
    }

    [Fact]
    public void Foreign_session_images_are_programming_errors()
    {
        using var files = new ResolverFiles();
        string path = files.Write("Input.dll", new ResolutionAssemblyBuilder(Identity("Input")).Build());
        using var first = files.Open();
        using var second = files.Open();
        ResolvedAssembly input = first.OpenInput(path).Value!;

        Assert.Throws<ArgumentException>(() => second.ResolveType(input, "Sample", "Anything"));
    }

    [Fact]
    public void Compiled_fixture_forwarders_resolve_without_loading_the_assemblies()
    {
        using var files = new ResolverFiles();
        files.Write(Fixture.Basic + ".dll", Fixture.Bytes(Fixture.Basic));
        string path = files.Write(Fixture.Forwarding + ".dll", Fixture.Bytes(Fixture.Forwarding));
        using var resolver = files.Open("refs");

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, "Cilica.Fixtures.Basic", "Outer", "Inner");

        Assert.True(result.Success);
        Assert.Equal(Fixture.Basic, result.Value!.Assembly.Identity.Name);
    }

    [Fact]
    public void Missing_type_in_a_present_forwarding_target_retains_the_chain()
    {
        using var files = new ResolverFiles();
        var facade = new ResolutionAssemblyBuilder(Identity("Facade"));
        facade.Forward("Sample", "Missing", facade.Reference(Identity("Target")));
        string path = files.Write("Facade.dll", facade.Build());
        files.Write("Target.dll", new ResolutionAssemblyBuilder(Identity("Target")).Build());
        using var resolver = files.Open("refs");

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, "Sample", "Missing");

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.TypeNotFound, diagnostic.Id);
        Assert.Contains("Facade", diagnostic.Location!, StringComparison.Ordinal);
        Assert.Contains("Target", diagnostic.Location!, StringComparison.Ordinal);
    }

    [Fact]
    public void Deep_acyclic_typeref_scopes_terminate_at_the_documented_limit()
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(Identity("Input"));
        for (int index = 0; index < 257; index++)
            builder.TypeReference("Sample", "Nested", index == 256
                ? MetadataTokens.EntityHandle(0x00000001) : MetadataTokens.TypeReferenceHandle(index + 2));
        string path = files.Write("Input.dll", builder.Build());
        using var resolver = files.Open();

        var result = resolver.ResolveType(resolver.OpenInput(path).Value!, MetadataTokens.TypeReferenceHandle(1));

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.TypeResolutionCycle, diagnostic.Id);
        Assert.Contains("256", diagnostic.Message, StringComparison.Ordinal);
    }
}
