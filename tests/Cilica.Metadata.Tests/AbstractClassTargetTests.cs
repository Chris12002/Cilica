using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class AbstractClassTargetTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Closed_interface_mapping_keeps_the_abstract_slot_but_requires_a_concrete_body(bool missing)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("AbstractInterfaceSlots", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var contract = b.Type("Test", "IRead", attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
        const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;
        var declaration = b.Method("Read", [0x20, 1, 8, 8], attributes: flags | MethodAttributes.Abstract, implementation: MethodImplAttributes.IL);
        var parent = b.Type("Test", "Parent", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        b.Implements(parent, contract);
        var slot = b.Method("Read", [0x20, 1, 8, 8], attributes: flags | MethodAttributes.Abstract, implementation: MethodImplAttributes.IL);
        var child = b.Type("Test", "Child", baseType: parent);
        MethodDefinitionHandle implementation = default;
        if (!missing) implementation = b.Method("Read", [0x20, 1, 8, 8], attributes: flags & ~MethodAttributes.NewSlot,
            emit: il => { il.LoadArgument(1); il.OpCode(System.Reflection.Metadata.ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = resolver.ResolveClosedInterfaceMethodTarget(resolver.ResolveDeclaredMethod(input, declaration).Value!,
            new(resolver.ResolveType(input, child).Value!, SignatureList<TypeSignature>.Empty), library);
        Assert.True(result.Success == !missing, string.Join("; ", result.Diagnostics));
        if (missing) return;
        Assert.Equal(slot, result.Value!.Mapping.Handle); Assert.Equal(slot, result.Value.Slot!.Handle);
        Assert.Equal(implementation, result.Value.Implementation.Handle);
    }

    [Theory]
    [InlineData("concrete", true)] [InlineData("abstract", true)] [InlineData("reabstract", true)]
    [InlineData("missing", false)] [InlineData("newslot", false)] [InlineData("owner", false)]
    [InlineData("runtime", false)] [InlineData("body", false)] [InlineData("final", false)] [InlineData("legacy", false)]
    public void Closed_class_slots_distinguish_abstract_declarations_from_concrete_implementations(string shape, bool success)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("AbstractSlots" + shape, new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var parent = b.Type("Test", "Parent", baseType: obj, attributes: TypeAttributes.Public | (shape == "owner" ? 0 : TypeAttributes.Abstract));
        const MethodAttributes abstractSlot = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Abstract;
        var declaration = b.Method("Read", [0x20, 1, 8, 8], attributes: abstractSlot | MethodAttributes.NewSlot | (shape == "final" ? MethodAttributes.Final : 0),
            emit: shape == "body" ? il => { il.LoadArgument(1); il.OpCode(System.Reflection.Metadata.ILOpCode.Ret); } : null, implementation: shape == "runtime" ? MethodImplAttributes.Runtime : MethodImplAttributes.IL);
        var middle = b.Type("Test", "Middle", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var middleMethod = b.Method("Read", [0x20, 1, 8, 8], attributes: abstractSlot, implementation: MethodImplAttributes.IL);
        var child = b.Type("Test", "Child", baseType: middle);
        MethodDefinitionHandle implementation = default;
        if (shape != "missing") implementation = b.Method("Read", [0x20, 1, 8, 8],
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | (shape == "newslot" ? MethodAttributes.NewSlot : 0),
            emit: il => { il.LoadArgument(1); il.OpCode(System.Reflection.Metadata.ILOpCode.Ret); });
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var receiver = resolver.ResolveType(input, shape == "abstract" ? parent : shape == "reabstract" ? middle : child).Value!;
        if (shape == "legacy")
        {
            Assert.False(resolver.ResolveClassVirtualTarget(input, declaration, receiver).Success); return;
        }
        var resolved = resolver.ResolveDeclaredMethod(input, declaration).Value!;
        var budget = new MemberResolutionBudget();
        var result = resolver.ResolveClosedClassVirtualTarget(resolved, new(receiver, SignatureList<TypeSignature>.Empty), library, budget);
        Assert.True(success == result.Success, string.Join("; ", result.Diagnostics));
        if (!success) { Assert.NotEmpty(result.Diagnostics); return; }
        Assert.Equal(declaration, result.Value!.Slot.Handle);
        Assert.Equal(shape == "abstract" ? declaration : shape == "reabstract" ? middleMethod : implementation, result.Value.Implementation.Handle);
        int used = 65536 - budget.Remaining;
        Assert.True(resolver.ResolveClosedClassVirtualTarget(resolved, result.Value.Receiver, library, new(used)).Success);
        Assert.Contains(resolver.ResolveClosedClassVirtualTarget(resolved, result.Value.Receiver, library, new(used - 1)).Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }
}
