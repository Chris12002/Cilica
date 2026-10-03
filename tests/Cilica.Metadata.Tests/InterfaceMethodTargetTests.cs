using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InterfaceMethodTargetTests(ITestOutputHelper output)
{
    private const TypeAttributes Interface = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
    private const MethodAttributes Abstract = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract;
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    private const MethodAttributes Explicit = MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final;
    private static ResolutionAssemblyBuilder Builder() => new(new("InterfaceTargets", new Version(1, 0, 0, 0)));
    private static void Body(InstructionEncoder il) { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); }

    [Theory]
    [InlineData(TypeAttributes.Public, TypeAttributes.NestedPublic, true)]
    [InlineData(TypeAttributes.NotPublic, TypeAttributes.NestedPublic, false)]
    [InlineData(TypeAttributes.Public, TypeAttributes.NestedPrivate, false)]
    public void Nested_contract_dispatch_requires_public_enclosing_types(TypeAttributes outerFlags, TypeAttributes middleFlags, bool success)
    {
        var builder = Builder();
        var outer = builder.Type("", "Outer", attributes: outerFlags);
        var middle = builder.Type("", "Middle", parent: outer, attributes: middleFlags);
        var contract = builder.Type("", "Contract", parent: middle,
            attributes: TypeAttributes.NestedPublic | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = builder.Method("Read", [0x20, 0, 8], attributes: Abstract);
        var receiver = builder.Type("", "Receiver"); builder.Implements(receiver, contract);
        var body = builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: Body);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("nested.dll", builder.Build())).Value!;
        var result = resolver.ResolveInterfaceMethodTarget(image, declaration, resolver.ResolveType(image, receiver).Value!);
        Assert.Equal(success, result.Success);
        if (success) Assert.Equal(body, result.Value!.Implementation.Handle);
        else
        {
            Assert.Null(result.Value);
            Assert.Contains("public enclosing types", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Exact_mappings_and_effective_bodies_match_authored_dotnet_interface_calls()
    {
        object[] receivers = [new ExactInterfaceBase(), new ExactInterfaceOverride(), new ExactInterfaceHidden(),
            new ExactInterfaceReimplemented(), new ExactInterfaceNonVirtualHidden(), new ExactInterfaceInherited(),
            new ExactInterfaceExplicit(), new ExactInterfaceExplicitInherited(), new ExactInterfaceExplicitReimplemented(),
            new ExactInterfaceReexplicit(), new ExactInterfaceFromPlain(), new ExactInterfaceDiamond(), new ExactInterfaceUnrelated()];
        Type[] contracts = [typeof(IExactRead), typeof(IExactSecond)];
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(IExactRead).Assembly.Location).Value!;
        int count = 0;
        foreach (var receiver in receivers) foreach (var contract in contracts) foreach (var declaration in contract.GetMethods())
        {
            var type = resolver.ResolveType(image, MetadataTokens.TypeDefinitionHandle(receiver.GetType().MetadataToken & 0xffffff)).Value!;
            var result = resolver.ResolveInterfaceMethodTarget(image,
                MetadataTokens.MethodDefinitionHandle(declaration.MetadataToken & 0xffffff), type);
            if (!contract.IsInstanceOfType(receiver)) { Assert.Null(result.Value); Assert.NotEmpty(result.Diagnostics); continue; }
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            var target = result.Value!;
            var map = receiver.GetType().GetInterfaceMap(contract);
            var expected = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, declaration)];
            Assert.Equal(expected.MetadataToken, MetadataTokens.GetToken(target.Implementation.Handle));
            var body = (MethodInfo)typeof(IExactRead).Module.ResolveMethod(MetadataTokens.GetToken(target.Implementation.Handle))!;
            Assert.Equal(Call(declaration, receiver, true), Call(body, receiver, false));
            Assert.True(declaration.IsAbstract);
            Assert.Equal(target.Declaration.Signature, target.Implementation.Signature);
            Assert.Equal(expected.IsVirtual ? expected.GetBaseDefinition().MetadataToken : (int?)null,
                target.Slot is null ? null : MetadataTokens.GetToken(target.Slot.Handle));
            count++;
        }
        Assert.Equal(47, count);
        output.WriteLine($"{count} exact interface body choices and authored calls agree with .NET.");
    }

    private static int Call(MethodInfo method, object receiver, bool dispatch)
    {
        var function = new DynamicMethod("AuthoredInterfaceOracle", typeof(int), [typeof(object)], typeof(InterfaceMethodTargetTests).Module, true);
        var il = function.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, method.DeclaringType!);
        for (int i = 0; i < method.GetParameters().Length; i++) il.Emit(OpCodes.Ldc_I4, i + 17);
        il.Emit(dispatch ? OpCodes.Callvirt : OpCodes.Call, method); il.Emit(OpCodes.Ret);
        return function.CreateDelegate<Func<object, int>>()(receiver);
    }

    [Theory]
    [InlineData("implicit")]
    [InlineData("nonvirtual")]
    [InlineData("inherited")]
    [InlineData("override")]
    [InlineData("hidden")]
    [InlineData("reimplemented")]
    [InlineData("explicit")]
    [InlineData("explicit_inherited")]
    [InlineData("reexplicit")]
    [InlineData("diamond")]
    public void Raw_mapping_paths_select_one_body(string shape)
    {
        var builder = Builder(); var contract = builder.Type("", "IRead", attributes: Interface);
        var declaration = builder.Method("Read", [0x20, 0, 8], attributes: Abstract);
        EntityHandle listed = contract;
        if (shape == "diamond")
        {
            var left = builder.Type("", "Left", attributes: Interface); builder.Implements(left, contract);
            var right = builder.Type("", "Right", attributes: Interface); builder.Implements(right, contract);
            var diamond = builder.Type("", "Diamond", attributes: Interface); builder.Implements(diamond, left); builder.Implements(diamond, right); listed = diamond;
        }
        var parent = builder.Type("", "Parent"); builder.Implements(parent, listed);
        bool explicitBase = shape is "explicit" or "explicit_inherited";
        var original = builder.Method(explicitBase ? "IRead.Read" : "Read", [0x20, 0, 8],
            attributes: explicitBase ? Explicit : shape == "nonvirtual" ? MethodAttributes.Public : Virtual | MethodAttributes.NewSlot, emit: Body);
        if (explicitBase) builder.MethodImplementation(parent, original, declaration);
        var leaf = builder.Type("", "Leaf", baseType: parent);
        MethodDefinitionHandle expected = original;
        if (shape is "override" or "hidden" or "reimplemented" or "reexplicit")
        {
            var replacement = builder.Method(shape == "reexplicit" ? "IRead.Read" : "Read", [0x20, 0, 8],
                attributes: shape == "reexplicit" ? Explicit : Virtual | (shape == "override" ? 0 : MethodAttributes.NewSlot), emit: Body);
            if (shape == "reexplicit") builder.MethodImplementation(leaf, replacement, declaration);
            if (shape is "reimplemented" or "reexplicit") builder.Implements(leaf, contract);
            if (shape != "hidden") expected = replacement;
        }
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("raw.dll", builder.Build())).Value!;
        var result = resolver.ResolveInterfaceMethodTarget(image, declaration, resolver.ResolveType(image, leaf).Value!);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(expected, result.Value!.Implementation.Handle);
        Assert.Equal(explicitBase || shape == "reexplicit", result.Value.IsExplicit);
    }

    [Theory]
    [InlineData("unrelated")]
    [InlineData("missing_body")]
    [InlineData("default")]
    [InlineData("private_contract")]
    [InlineData("private_method")]
    [InlineData("static_method")]
    [InlineData("generic_method")]
    [InlineData("generic_interface")]
    [InlineData("generic_owner")]
    [InlineData("abstract_receiver")]
    [InlineData("interface_receiver")]
    [InlineData("sealed_parent")]
    [InlineData("cycle")]
    [InlineData("class_edge")]
    [InlineData("duplicate_edge")]
    [InlineData("duplicate_method")]
    [InlineData("duplicate_declaration")]
    [InlineData("return_type")]
    [InlineData("duplicate_mapping")]
    [InlineData("wrong_mapping_owner")]
    [InlineData("wrong_mapping_signature")]
    [InlineData("private_mapping_not_final")]
    [InlineData("class_mapping")]
    [InlineData("instance_field")]
    [InlineData("generic_enclosing")]
    [InlineData("interface_class_base")]
    [InlineData("interface_remapping")]
    [InlineData("missing")]
    [InlineData("nil")]
    public void Invalid_or_unsupported_metadata_returns_diagnostics_without_a_partial_target(string shape)
    {
        var builder = Builder();
        var enclosing = shape == "generic_enclosing" ? builder.Type("", "Generic", arity: 1) : default;
        var contract = builder.Type("", "IRead", parent: enclosing, arity: shape == "generic_interface" ? 1 : 0,
            baseType: shape == "interface_class_base" ? MetadataTokens.TypeDefinitionHandle(4) : default,
            attributes: shape == "private_contract" ? TypeAttributes.Interface | TypeAttributes.Abstract
                : shape == "generic_enclosing" ? TypeAttributes.NestedPublic | TypeAttributes.Interface | TypeAttributes.Abstract : Interface);
        var declaration = builder.Method("Read", shape == "generic_method" ? [0x30, 1, 0, 8] : shape == "static_method" ? [0, 0, 8] : [0x20, 0, 8],
            arity: shape == "generic_method" ? 1 : 0, attributes: shape switch {
                "private_method" => (Abstract & ~MethodAttributes.MemberAccessMask) | MethodAttributes.Private,
                "static_method" => Abstract | MethodAttributes.Static, "default" => Abstract & ~MethodAttributes.Abstract, _ => Abstract },
            emit: shape == "default" ? Body : null);
        if (shape == "cycle") builder.Implements(contract, contract);
        if (shape == "duplicate_declaration") builder.Method("Read", [0x20, 0, 8], attributes: Abstract);
        if (shape == "interface_remapping") builder.MethodImplementation(contract, declaration, declaration);
        if (shape == "instance_field") builder.Field("Bad", 0x06, 0x08);
        var parent = builder.Type("", "Parent", attributes: TypeAttributes.Public | (shape == "sealed_parent" ? TypeAttributes.Sealed : 0));
        var baseMethod = builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: Body);
        var leaf = builder.Type("", "Leaf", baseType: parent, arity: shape == "generic_owner" ? 1 : 0,
            attributes: TypeAttributes.Public | (shape == "abstract_receiver" ? TypeAttributes.Abstract : shape == "interface_receiver" ? TypeAttributes.Abstract | TypeAttributes.Interface : 0));
        if (shape != "unrelated") builder.Implements(leaf, shape == "class_edge" ? parent : contract);
        if (shape == "duplicate_edge") builder.Implements(leaf, contract);
        var body = builder.Method("Read", shape is "return_type" or "wrong_mapping_signature" ? [0x20, 0, 0x1c] : [0x20, 0, 8],
            attributes: shape == "private_mapping_not_final" ? MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.NewSlot : Virtual | MethodAttributes.NewSlot, emit: Body);
        if (shape == "duplicate_method") builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: Body);
        if (shape == "missing_body")
        {
            // Use an overload-only interface declaration so neither existing
            // zero-parameter method can satisfy it.
            var other = builder.Type("", "Other", attributes: Interface);
            declaration = builder.Method("Missing", [0x20, 0, 8], attributes: Abstract); builder.Implements(leaf, other);
        }
        if (shape is "duplicate_mapping" or "wrong_mapping_owner" or "wrong_mapping_signature" or "private_mapping_not_final" or "class_mapping")
        {
            builder.MethodImplementation(leaf, shape == "wrong_mapping_owner" ? baseMethod : body, shape == "class_mapping" ? baseMethod : declaration);
            if (shape == "duplicate_mapping") builder.MethodImplementation(leaf, body, declaration);
        }
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("invalid.dll", builder.Build())).Value!;
        var result = resolver.ResolveInterfaceMethodTarget(image, shape == "missing" ? MetadataTokens.MethodDefinitionHandle(999) : shape == "nil" ? default : declaration,
            resolver.ResolveType(image, leaf).Value!);
        Assert.Null(result.Value); Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, d => Assert.Contains("token 0x", d.Location, StringComparison.Ordinal));
    }

    [Fact]
    public void Exact_short_and_exhausted_shared_metadata_budgets_are_enforced()
    {
        var builder = Builder(); var contract = builder.Type("", "IRead", attributes: Interface);
        var declaration = builder.Method("Read", [0x20, 0, 8], attributes: Abstract);
        var leaf = builder.Type("", "Leaf"); builder.Implements(leaf, contract);
        builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: Body);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("budget.dll", builder.Build())).Value!;
        var receiver = resolver.ResolveType(image, leaf).Value!;
        var measured = new MemberResolutionBudget(); Assert.True(resolver.ResolveInterfaceMethodTarget(image, declaration, receiver, measured).Success);
        int used = 65536 - measured.Remaining;
        var exact = new MemberResolutionBudget(used); Assert.True(resolver.ResolveInterfaceMethodTarget(image, declaration, receiver, exact).Success); Assert.Equal(0, exact.Remaining);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ResolveInterfaceMethodTarget(image, declaration, receiver, new(used - 1)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ResolveInterfaceMethodTarget(image, declaration, receiver, exact).Diagnostics).Id);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Interface_graph_depth_and_breadth_have_exact_boundaries(int count, bool accepted)
    {
        foreach (bool deep in new[] { false, true })
        {
            var builder = Builder(); var contract = builder.Type("", "I0", attributes: Interface);
            var declaration = builder.Method("Read", [0x20, 0, 8], attributes: Abstract);
            var listed = new TypeDefinitionHandle[count]; listed[0] = contract;
            for (int i = 1; i < count; i++)
            {
                listed[i] = builder.Type("", "I" + i, attributes: Interface);
                if (deep) builder.Implements(listed[i], listed[i - 1]);
            }
            var leaf = builder.Type("", "Leaf");
            if (deep) builder.Implements(leaf, listed[^1]);
            else foreach (var type in listed) builder.Implements(leaf, type);
            builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: Body);
            using var files = new ResolverFiles(); using var resolver = files.Open();
            var image = resolver.OpenInput(files.Write("graph.dll", builder.Build())).Value!;
            var result = resolver.ResolveInterfaceMethodTarget(image, declaration, resolver.ResolveType(image, leaf).Value!);
            Assert.Equal(accepted, result.Success);
            if (!accepted) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        }
    }

    [Fact]
    public void Cross_image_equal_names_and_tokens_do_not_merge_contracts_or_bodies()
    {
        var identity = new AssemblyIdentity("InterfaceCore", new Version(1, 0, 0, 0));
        var core = new ResolutionAssemblyBuilder(identity);
        core.Type("Example", "SameName", attributes: Interface);
        var declaration = core.Method("Read", [0x20, 0, 8], attributes: Abstract);
        var app = new ResolutionAssemblyBuilder(new("InterfaceApp", new Version(1, 0, 0, 0)));
        var reference = app.Reference(identity);
        var contract = app.TypeReference("Example", "SameName", reference);
        var member = app.Member(contract, "Read", 0x20, 0, 8);
        var leaf = app.Type("Example", "SameName"); app.Implements(leaf, contract);
        var body = app.Method("IRead.Read", [0x20, 0, 8], attributes: Explicit, emit: Body); app.MethodImplementation(leaf, body, member);
        var other = new ResolutionAssemblyBuilder(new("OtherInterface", new Version(1, 0, 0, 0)));
        var unrelated = other.Type("Example", "SameName", attributes: Interface);
        var wrong = other.Method("Read", [0x20, 0, 8], attributes: Abstract);
        using var files = new ResolverFiles();
        string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build()), otherPath = files.Write("other.dll", other.Build());
        using var resolver = files.Open("refs");
        var coreImage = resolver.OpenInput(corePath).Value!; var appImage = resolver.OpenInput(appPath).Value!; var otherImage = resolver.OpenInput(otherPath).Value!;
        var receiver = resolver.ResolveType(appImage, leaf).Value!;
        var result = resolver.ResolveInterfaceMethodTarget(coreImage, declaration, receiver);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); Assert.Equal(declaration, body); Assert.Equal(leaf, unrelated);
        Assert.Equal(coreImage.ContentHash, result.Value!.Declaration.DeclaringType.Definition.Assembly.ContentHash);
        Assert.Equal(appImage.ContentHash, result.Value.Implementation.DeclaringType.Definition.Assembly.ContentHash);
        Assert.False(resolver.ResolveInterfaceMethodTarget(otherImage, wrong, receiver).Success);
    }

    [Fact]
    public void Foreign_resolver_readers_fail_before_equal_content_identity_comparisons()
    {
        var builder = Builder(); var contract = builder.Type("", "IRead", attributes: Interface);
        var declaration = builder.Method("Read", [0x20, 0, 8], attributes: Abstract);
        var leaf = builder.Type("", "Leaf"); builder.Implements(leaf, contract);
        builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: Body);
        using var files = new ResolverFiles(); string path = files.Write("owner.dll", builder.Build());
        using var first = files.Open(); using var second = files.Open();
        var image = first.OpenInput(path).Value!; var foreign = second.OpenInput(path).Value!;
        var own = first.ResolveType(image, leaf).Value!; var other = second.ResolveType(foreign, leaf).Value!;
        Assert.Equal(own, other);
        Assert.Throws<ArgumentException>(() => first.ResolveInterfaceMethodTarget(image, declaration, other));
        Assert.Throws<ArgumentException>(() => first.ResolveInterfaceMethodTarget(foreign, declaration, own));
    }
}
