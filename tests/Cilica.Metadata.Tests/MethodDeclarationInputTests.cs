using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class MethodDeclarationInputTests
{
    [Fact]
    public void Bodyless_declarations_have_bounded_input_without_weakening_body_reading()
    {
        var builder = new ResolutionAssemblyBuilder(new("Declarations", new Version(1, 0, 0, 0)));
        var owner = builder.Type("Example", "Read", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var method = builder.Method("Read", [0x20, 0, 8], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract);
        using var files = new ResolverFiles(); using var resolver = files.Open(); var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var read = resolver.ReadMethodDeclarationInput(image, method, new(MaximumMembers: 5)); Assert.True(read.Success);
        var declaration = read.Value!; Assert.False(declaration.HasBody); Assert.True(declaration.OwnerExternallyVisible);
        Assert.Equal("Read", declaration.DeclaredName); Assert.Equal(MetadataTokens.GetToken(owner), declaration.OwnerToken); Assert.Equal(5, declaration.MetadataMembersRead);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, Assert.Single(resolver.ReadMethodInput(image, method).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadMethodDeclarationInput(image, method, new(MaximumMembers: 4)).Diagnostics).Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadMethodDeclarationInput(image, method, new(MaximumMembers: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ReadMethodDeclarationInput(image, method, new(MaximumOwnerDepth: 65)));
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Declaring_owners_are_bounded_before_name_formatting(int depth, bool success)
    {
        var builder = new ResolutionAssemblyBuilder(new("DeepDeclarations", new Version(1, 0, 0, 0))); TypeDefinitionHandle owner = default;
        for (int i = 0; i < depth; i++) owner = builder.Type("", "N" + i, parent: owner);
        var method = builder.Method("Read", [0, 0, 8]);
        using var files = new ResolverFiles(); using var resolver = files.Open(); var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var read = resolver.ReadMethodDeclarationInput(image, method); Assert.Equal(success, read.Success);
        if (!success) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(read.Diagnostics).Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Missing_method_rows_are_diagnostics(int row)
    {
        var builder = new ResolutionAssemblyBuilder(new("MissingDeclaration", new Version(1, 0, 0, 0))); builder.Type("", "Read");
        using var files = new ResolverFiles(); using var resolver = files.Open(); var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var read = resolver.ReadMethodDeclarationInput(image, MetadataTokens.MethodDefinitionHandle(row));
        Assert.Null(read.Value); Assert.Equal(DiagnosticIds.InvalidSignature, Assert.Single(read.Diagnostics).Id);
    }
}
