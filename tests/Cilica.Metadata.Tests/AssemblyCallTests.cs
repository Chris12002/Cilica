using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class AssemblyCallTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public System.Threading.Tasks.Task Assembly_helpers_preserve_values_borrows_and_faults(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized, [("Flow", AssemblyCalls.Flow), ("Fault", AssemblyCalls.Fault)], typeof(AssemblyCalls));

    [Theory]
    [InlineData(TypeAttributes.Public, MethodAttributes.Assembly, true)]
    [InlineData(TypeAttributes.NotPublic, MethodAttributes.Public, true)]
    [InlineData(TypeAttributes.NestedPublic, MethodAttributes.Assembly, true)]
    [InlineData(TypeAttributes.NestedAssembly, MethodAttributes.Public, true)]
    [InlineData(TypeAttributes.NestedFamORAssem, MethodAttributes.FamORAssem, true)]
    [InlineData(TypeAttributes.NestedPrivate, MethodAttributes.Public, false)]
    [InlineData(TypeAttributes.NestedFamily, MethodAttributes.Public, false)]
    [InlineData(TypeAttributes.NestedFamANDAssem, MethodAttributes.Public, false)]
    [InlineData(TypeAttributes.Public, MethodAttributes.Private, false)]
    [InlineData(TypeAttributes.Public, MethodAttributes.Family, false)]
    [InlineData(TypeAttributes.Public, MethodAttributes.FamANDAssem, false)]
    public void Same_image_access_checks_owner_chain_and_member(TypeAttributes visibility, MethodAttributes access, bool allowed)
    {
        var builder = new ResolutionAssemblyBuilder(new("Access", new Version(1, 0, 0, 0)));
        TypeDefinitionHandle parent = default;
        if (visibility is not (TypeAttributes.Public or TypeAttributes.NotPublic)) parent = builder.Type("", "Outer", attributes: TypeAttributes.NotPublic);
        builder.Type("", "Helper", parent: parent, attributes: visibility);
        var helper = builder.Method("Value", [0, 0, 8], attributes: access | MethodAttributes.Static,
            emit: il => { il.LoadConstantI4(17); il.OpCode(ILOpCode.Ret); });
        builder.Type("", "Caller");
        var root = builder.Method("Root", [0, 0, 8], emit: il => { il.Call(helper); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var declaration = resolver.ReadMethodDeclarationInput(image, helper).Value!;
        var body = resolver.ReadMethodInput(image, helper).Value!;
        bool ownerVisible = visibility is TypeAttributes.Public or TypeAttributes.NotPublic or TypeAttributes.NestedPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem;
        Assert.Equal(ownerVisible, declaration.OwnerAssemblyVisible);
        Assert.Equal(ownerVisible, body.OwnerAssemblyVisible);
        var result = ValueTranslator.TranslateIntegerObjects(resolver, image, root, runtime);
        Assert.Equal(allowed, result.Success);
        if (allowed) ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains("assembly-accessible", result.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.False(ValueTranslator.TranslateExceptions(resolver, image, root, runtime).Success);
    }

    [Theory]
    [InlineData(TypeAttributes.NotPublic, MethodAttributes.Public, false)]
    [InlineData(TypeAttributes.Public, MethodAttributes.Assembly, false)]
    [InlineData(TypeAttributes.Public, MethodAttributes.FamORAssem, false)]
    [InlineData(TypeAttributes.Public, MethodAttributes.Public, true)]
    public void A_different_image_gets_no_assembly_access(TypeAttributes visibility, MethodAttributes access, bool allowed)
    {
        var identity = new AssemblyIdentity("Helpers", new Version(1, 0, 0, 0));
        var helpers = new ResolutionAssemblyBuilder(identity);
        helpers.Type("", "Helper", attributes: visibility);
        helpers.Method("Value", [0, 0, 8], attributes: access | MethodAttributes.Static,
            emit: il => { il.LoadConstantI4(17); il.OpCode(ILOpCode.Ret); });
        var caller = new ResolutionAssemblyBuilder(new("Caller", new Version(1, 0, 0, 0)));
        var target = caller.Member(caller.TypeReference("", "Helper", caller.Reference(identity)), "Value", 0, 0, 8);
        caller.Type("", "Caller");
        var root = caller.Method("Root", [0, 0, 8], emit: il => { il.Call(target); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); files.Write("Helpers.dll", helpers.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(files.Write("Caller.dll", caller.Build())).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, image, root, runtime);
        Assert.Equal(allowed, result.Success);
        if (!allowed) Assert.Contains("assembly-accessible", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_public_nested_owner_does_not_escape_its_private_ancestor()
    {
        var builder = new ResolutionAssemblyBuilder(new("NestedAccess", new Version(1, 0, 0, 0)));
        var outer = builder.Type("", "Outer");
        var hidden = builder.Type("", "Hidden", parent: outer, attributes: TypeAttributes.NestedPrivate);
        builder.Type("", "PublicChild", parent: hidden);
        var helper = builder.Method("Value", [0, 0, 8], emit: il => { il.LoadConstantI4(17); il.OpCode(ILOpCode.Ret); });
        builder.Type("", "Caller");
        var root = builder.Method("Root", [0, 0, 8], emit: il => { il.Call(helper); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        Assert.False(resolver.ReadMethodDeclarationInput(image, helper).Value!.OwnerAssemblyVisible);
        Assert.False(resolver.ReadMethodInput(image, helper).Value!.OwnerAssemblyVisible);
        Assert.False(ValueTranslator.TranslateIntegerObjects(resolver, image, root, runtime).Success);
    }

    [Fact]
    public void Detached_bindings_cannot_drop_required_owner_access()
    {
        var program = IntegerObjectTranslationTests.Translate("Flow", fixture: typeof(AssemblyCalls)).Program!;
        var bindings = program.Flow.Bindings;
        var hidden = bindings.First(b => !b.OwnerExternallyVisible && b.OwnerAssemblyVisible);
        var changed = program with { Flow = program.Flow with { Bindings = bindings.Replace(hidden, hidden with { OwnerAssemblyVisible = false }) } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(changed));
    }
}
