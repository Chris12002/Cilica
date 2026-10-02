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

public sealed class ClassVirtualTargetTests(ITestOutputHelper output)
{
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    private const MethodAttributes NewSlot = Virtual | MethodAttributes.NewSlot;
    private static ResolutionAssemblyBuilder Builder() => new(new("ClassTargets", new Version(1, 0, 0, 0)));
    private static void Body(InstructionEncoder il) { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); }

    [Fact]
    public void Exact_targets_and_canonical_slots_agree_with_authored_dotnet_calls()
    {
        object[] receivers = [new ExactDispatchBase(), new ExactDispatchMiddle(), new ExactDispatchLeaf(), new ExactDispatchInherited(),
            new ExactDispatchHidden(), new ExactDispatchHiddenLeaf(), new ExactDispatchNonVirtual(), new ExactDispatchSibling()];
        var declarations = receivers.Select(o => o.GetType()).SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.IsVirtual).ToArray();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ExactDispatchBase).Assembly.Location).Value!;
        int count = 0;
        foreach (object receiver in receivers) foreach (var declaration in declarations)
        {
            var resolvedType = resolver.ResolveType(image, MetadataTokens.TypeDefinitionHandle(receiver.GetType().MetadataToken & 0xffffff)).Value!;
            var result = resolver.ResolveClassVirtualTarget(image, MetadataTokens.MethodDefinitionHandle(declaration.MetadataToken & 0xffffff), resolvedType);
            if (!declaration.DeclaringType!.IsInstanceOfType(receiver))
            {
                Assert.Null(result.Value); Assert.NotEmpty(result.Diagnostics); continue;
            }
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            var target = result.Value!;
            Assert.Equal(declaration.GetBaseDefinition().MetadataToken, MetadataTokens.GetToken(target.Slot.Handle));
            var implementation = (MethodInfo)typeof(ExactDispatchBase).Module.ResolveMethod(MetadataTokens.GetToken(target.Implementation.Handle))!;
            // Only trusted, authored fixture methods run as the .NET oracle. The
            // resolver reads their assembly as data and never invokes these helpers.
            Assert.Equal(Call(declaration, receiver, dispatch: true), Call(implementation, receiver, dispatch: false));
            Assert.Equal(target.Declaration.Signature, target.Implementation.Signature);
            count++;
        }
        Assert.Equal(41, count);
        output.WriteLine($"{count} exact receiver/declaration pairs match .NET callvirt and canonical base slots.");
    }

    private static int Call(MethodInfo method, object receiver, bool dispatch)
    {
        var function = new DynamicMethod("AuthoredDispatchOracle", typeof(int), [typeof(object)]);
        var il = function.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, method.DeclaringType!);
        for (int i = 0; i < method.GetParameters().Length; i++) il.Emit(OpCodes.Ldc_I4, i + 17);
        il.Emit(dispatch ? OpCodes.Callvirt : OpCodes.Call, method); il.Emit(OpCodes.Ret);
        return function.CreateDelegate<Func<object, int>>()(receiver);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("final")]
    [InlineData("newslot")]
    [InlineData("final_newslot")]
    [InlineData("reuse_without_parent")]
    public void Slot_rules_select_one_body_and_keep_hidden_chains_separate(string shape)
    {
        var builder = Builder();
        var root = builder.Type("Example", "Base");
        var first = builder.Method("Read", [0x20, 0, 8], attributes: shape == "reuse_without_parent" ? Virtual
            : shape == "final_newslot" ? NewSlot | MethodAttributes.Final : NewSlot, emit: Body);
        var middle = builder.Type("Example", "Middle", baseType: root);
        var second = builder.Method("Read", [0x20, 0, 8], attributes: shape is "newslot" or "final_newslot" ? NewSlot
            : shape == "final" ? Virtual | MethodAttributes.Final : Virtual, emit: Body);
        var leaf = builder.Type("Example", "Leaf", baseType: middle);
        MethodDefinitionHandle? third = shape == "final" ? null : builder.Method("Read", [0x20, 0, 8], attributes: Virtual, emit: Body);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("slots.dll", builder.Build())).Value!;
        var receiver = resolver.ResolveType(image, leaf).Value!;
        var old = resolver.ResolveClassVirtualTarget(image, first, receiver);
        Assert.True(old.Success, string.Join("; ", old.Diagnostics));
        Assert.Equal(first, old.Value!.Slot.Handle);
        Assert.Equal(shape is "newslot" or "final_newslot" ? first : third ?? second, old.Value.Implementation.Handle);
        var newer = resolver.ResolveClassVirtualTarget(image, second, receiver);
        Assert.True(newer.Success, string.Join("; ", newer.Diagnostics));
        Assert.Equal(shape is "newslot" or "final_newslot" ? second : first, newer.Value!.Slot.Handle);
        Assert.Equal(third ?? second, newer.Value.Implementation.Handle);
    }

    [Theory]
    [InlineData("final_override")]
    [InlineData("sealed_parent")]
    [InlineData("abstract_receiver")]
    [InlineData("abstract_parent")]
    [InlineData("abstract_method")]
    [InlineData("interface")]
    [InlineData("methodimpl")]
    [InlineData("duplicate")]
    [InlineData("duplicate_nonvirtual")]
    [InlineData("return_type")]
    [InlineData("private")]
    [InlineData("static")]
    [InlineData("nonvirtual")]
    [InlineData("generic_owner")]
    [InlineData("generic_enclosing")]
    [InlineData("generic_method")]
    [InlineData("cycle")]
    [InlineData("nil")]
    [InlineData("missing")]
    [InlineData("module_parent")]
    public void Unsupported_or_invalid_slot_metadata_never_returns_a_partial_choice(string shape)
    {
        var builder = Builder();
        var enclosing = shape == "generic_enclosing" ? builder.Type("Example", "Generic", arity: 1) : default;
        var parent = builder.Type("Example", "Parent", arity: shape == "generic_owner" ? 1 : 0,
            parent: enclosing,
            attributes: shape switch { "sealed_parent" => TypeAttributes.Public | TypeAttributes.Sealed,
                "abstract_parent" => TypeAttributes.Public | TypeAttributes.Abstract,
                "interface" => TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, _ => TypeAttributes.Public },
            baseType: shape == "cycle" ? MetadataTokens.TypeDefinitionHandle(3) : default);
        var first = builder.Method("Read", shape == "generic_method" ? [0x30, 1, 0, 8] : [0x20, 0, 8],
            arity: shape == "generic_method" ? 1 : 0,
            attributes: shape == "final_override" ? NewSlot | MethodAttributes.Final
                : shape == "abstract_method" ? NewSlot | MethodAttributes.Abstract : NewSlot, emit: Body);
        var child = builder.Type("Example", "Child", baseType: shape == "module_parent" ? MetadataTokens.TypeDefinitionHandle(1) : parent,
            attributes: TypeAttributes.Public | (shape == "abstract_receiver" ? TypeAttributes.Abstract : 0));
        var second = builder.Method("Read", shape == "return_type" ? [0x20, 0, 0x1c] : shape == "static" ? [0, 0, 8] : [0x20, 0, 8],
            attributes: shape switch { "private" => MethodAttributes.Private | MethodAttributes.Virtual,
                "static" => Virtual | MethodAttributes.Static, "nonvirtual" => MethodAttributes.Public, _ => Virtual }, emit: Body);
        if (shape == "duplicate") builder.Method("Read", [0x20, 0, 8], attributes: Virtual, emit: Body);
        if (shape == "duplicate_nonvirtual") builder.Method("Read", [0x20, 0, 8], attributes: MethodAttributes.Public, emit: Body);
        if (shape == "methodimpl") builder.MethodImplementation(child, second, first);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("invalid.dll", builder.Build())).Value!;
        var receiver = resolver.ResolveType(image, child).Value!;
        var declaration = shape is "nonvirtual" or "static" ? second : shape == "nil" ? default
            : shape == "missing" ? MetadataTokens.MethodDefinitionHandle(999) : first;
        var result = resolver.ResolveClassVirtualTarget(image, declaration, receiver);
        Assert.Null(result.Value); Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, d => { Assert.Equal(DiagnosticSeverity.Error, d.Severity); Assert.Contains("token 0x", d.Location, StringComparison.Ordinal); });
    }

    [Fact]
    public void Metadata_ledger_has_exact_excess_and_shared_consumption_boundaries()
    {
        var builder = Builder(); var parent = builder.Type("", "Parent");
        var method = builder.Method("Read", [0x20, 0, 8], attributes: NewSlot, emit: Body);
        var child = builder.Type("", "Child", baseType: parent);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("budget.dll", builder.Build())).Value!;
        var receiver = resolver.ResolveType(image, child).Value!;
        var measured = new MemberResolutionBudget();
        Assert.True(resolver.ResolveClassVirtualTarget(image, method, receiver, measured).Success);
        int used = 65536 - measured.Remaining;
        var exact = new MemberResolutionBudget(used);
        Assert.True(resolver.ResolveClassVirtualTarget(image, method, receiver, exact).Success); Assert.Equal(0, exact.Remaining);
        var excess = resolver.ResolveClassVirtualTarget(image, method, receiver, new MemberResolutionBudget(used - 1));
        Assert.Null(excess.Value); Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(excess.Diagnostics).Id);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ResolveClassVirtualTarget(image, method, receiver, exact).Diagnostics).Id);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Hierarchy_walk_is_iterative_and_bounded(int depth, bool accepted)
    {
        var builder = Builder(); var leaf = builder.Type("", "T0");
        var method = builder.Method("Read", [0x20, 0, 8], attributes: NewSlot, emit: Body);
        for (int i = 1; i < depth; i++) leaf = builder.Type("", "T" + i, baseType: leaf);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("deep.dll", builder.Build())).Value!;
        var result = resolver.ResolveClassVirtualTarget(image, method, resolver.ResolveType(image, leaf).Value!);
        Assert.Equal(accepted, result.Success);
        if (accepted) Assert.Equal(method, result.Value!.Implementation.Handle);
        else Assert.Equal(DiagnosticIds.TypeResolutionCycle, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Cross_image_equal_names_and_tokens_keep_slot_and_body_identities_separate()
    {
        var coreIdentity = new AssemblyIdentity("SlotCore", new Version(1, 0, 0, 0));
        var core = new ResolutionAssemblyBuilder(coreIdentity);
        core.Type("Example", "SameName");
        var original = core.Method("Read", [0x20, 0, 8], attributes: NewSlot, emit: Body);
        var app = new ResolutionAssemblyBuilder(new("SlotApp", new Version(1, 0, 0, 0)));
        var parent = app.TypeReference("Example", "SameName", app.Reference(coreIdentity));
        var child = app.Type("Example", "SameName", baseType: parent);
        var replacement = app.Method("Read", [0x20, 0, 8], attributes: Virtual, emit: Body);
        var other = new ResolutionAssemblyBuilder(new("OtherSlots", new Version(1, 0, 0, 0)));
        var unrelated = other.Type("Example", "SameName");
        other.Method("Read", [0x20, 0, 8], attributes: NewSlot, emit: Body);
        using var files = new ResolverFiles();
        string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build()), otherPath = files.Write("other.dll", other.Build());
        using var resolver = files.Open("refs");
        var coreImage = resolver.OpenInput(corePath).Value!;
        var appImage = resolver.OpenInput(appPath).Value!;
        var otherImage = resolver.OpenInput(otherPath).Value!;
        var receiver = resolver.ResolveType(appImage, child).Value!;
        var result = resolver.ResolveClassVirtualTarget(coreImage, original, receiver);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(original, replacement); Assert.Equal(0x02000002, MetadataTokens.GetToken(child));
        Assert.Equal(coreImage.ContentHash, result.Value!.Slot.DeclaringType.Definition.Assembly.ContentHash);
        Assert.Equal(appImage.ContentHash, result.Value.Implementation.DeclaringType.Definition.Assembly.ContentHash);
        Assert.False(resolver.ResolveClassVirtualTarget(coreImage, original, resolver.ResolveType(otherImage, unrelated).Value!).Success);
        var overriddenDeclaration = resolver.ResolveClassVirtualTarget(appImage, replacement, receiver);
        Assert.Equal(result.Value.Slot, overriddenDeclaration.Value!.Slot);
    }

    [Fact]
    public void Foreign_session_readers_are_rejected_before_equal_content_identity_is_used()
    {
        var builder = Builder(); var type = builder.Type("", "Owner");
        var method = builder.Method("Read", [0x20, 0, 8], attributes: NewSlot, emit: Body);
        using var files = new ResolverFiles(); string path = files.Write("owner.dll", builder.Build());
        using var first = files.Open(); using var second = files.Open();
        var image = first.OpenInput(path).Value!; var foreign = second.OpenInput(path).Value!;
        var ownType = first.ResolveType(image, type).Value!; var foreignType = second.ResolveType(foreign, type).Value!;
        Assert.Equal(ownType, foreignType);
        Assert.Throws<ArgumentException>(() => first.ResolveClassVirtualTarget(image, method, foreignType));
        Assert.Throws<ArgumentException>(() => first.ResolveClassVirtualTarget(foreign, method, ownType));
    }
}
