using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GenericInterfaceMethodTargetTests
{
    [Theory]
    [InlineData("implicit", true)] [InlineData("explicit", true)] [InlineData("inherited", true)]
    [InlineData("hidden", true)] [InlineData("reimplemented", true)] [InlineData("overload", true)]
    [InlineData("nonfinal", false)] [InlineData("reuse", false)] [InlineData("arity", false)]
    [InlineData("open", false)] [InlineData("signature", false)] [InlineData("decl-constraint", false)]
    [InlineData("body-constraint", false)] [InlineData("legacy", false)] [InlineData("class", false)] [InlineData("foreign", false)]
    public void Closed_generic_interface_methods_preserve_exact_final_slot_identity(string shape, bool success)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("GenericInterface" + shape, new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var contract = b.Type("Test", "IEcho", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        byte[] signature = [0x30, 1, 1, 0x1e, 0, 0x1e, 0];
        var declaration = b.Method("Echo", signature, arity: 1,
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot,
            implementation: MethodImplAttributes.IL,
            genericAttributes: shape == "decl-constraint" ? GenericParameterAttributes.ReferenceTypeConstraint : GenericParameterAttributes.None);
        var parent = b.Type("Test", "Parent", baseType: obj); b.Implements(parent, contract);
        var flags = (shape == "explicit" ? MethodAttributes.Private : MethodAttributes.Public) | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
        if (shape == "nonfinal") flags &= ~MethodAttributes.Final;
        if (shape == "reuse") flags &= ~MethodAttributes.NewSlot;
        var implementation = b.Method(shape == "explicit" ? "Test.IEcho.Echo" : "Echo", signature, arity: 1, attributes: flags,
            emit: il => { il.LoadArgument(1); il.OpCode(ILOpCode.Ret); },
            genericAttributes: shape == "body-constraint" ? GenericParameterAttributes.ReferenceTypeConstraint : GenericParameterAttributes.None);
        if (shape == "explicit") b.MethodImplementation(parent, implementation, declaration);
        if (shape == "overload") b.Method("Echo", [0x30, 1, 1, 8, 8], arity: 1, attributes: flags,
            emit: il => { il.LoadArgument(1); il.OpCode(ILOpCode.Ret); });
        var receiver = parent;
        if (shape is "inherited" or "hidden" or "reimplemented")
        {
            receiver = b.Type("Test", "Child", baseType: parent);
            if (shape == "reimplemented") b.Implements(receiver, contract);
            if (shape != "inherited")
            {
                var hidden = b.Method("Echo", signature, arity: 1, attributes: flags, emit: il => { il.LoadArgument(1); il.OpCode(ILOpCode.Ret); });
                if (shape == "reimplemented") implementation = hidden;
            }
        }
        var spec = b.Instantiate(declaration, 0x0a, 1, 8);
        var path = files.Write("input.dll", b.Build());
        var image = resolver.OpenInput(path).Value!;
        var resolved = resolver.ResolveMethod(image, shape == "open" ? declaration : spec).Value!;
        if (shape == "arity") resolved = resolved with { MethodArguments = SignatureList<TypeSignature>.Empty };
        if (shape == "signature") resolved = resolved with { Signature = resolved.Signature with { ReturnType = new TypeSignature.Primitive(PrimitiveTypeCode.Int64) } };
        var type = resolver.ResolveType(image, receiver).Value!;
        if (shape == "legacy") { Assert.False(resolver.ResolveInterfaceMethodTarget(image, declaration, type).Success); return; }
        if (shape == "class")
        {
            var method = resolver.ResolveMethod(image, implementation).Value!;
            Assert.False(resolver.ResolveClosedClassVirtualTarget(method, new(type, SignatureList<TypeSignature>.Empty), library).Success); return;
        }
        if (shape == "foreign")
        {
            using var other = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
            var foreignImage = other.OpenInput(path).Value!;
            var foreign = new TypeSignature.Named(other.ResolveType(foreignImage, parent).Value!, SignatureTypeKind.Class);
            resolved = resolved with { MethodArguments = new([foreign]),
                Signature = resolved.Signature with { ReturnType = foreign, Parameters = new([foreign]) } };
            Assert.Throws<ArgumentException>(() => resolver.ResolveClosedInterfaceMethodTarget(resolved,
                new(type, SignatureList<TypeSignature>.Empty), library));
            return;
        }
        var budget = new MemberResolutionBudget();
        var result = resolver.ResolveClosedInterfaceMethodTarget(resolved, new(type, SignatureList<TypeSignature>.Empty), library, budget);
        Assert.True(result.Success == success, string.Join("; ", result.Diagnostics));
        if (!success) { Assert.NotEmpty(result.Diagnostics); return; }
        Assert.Equal(implementation, result.Value!.Implementation.Handle);
        Assert.Equal(result.Value.Implementation, result.Value.Mapping); Assert.Equal(result.Value.Mapping, result.Value.Slot);
        Assert.Equal(resolved.MethodArguments, result.Value.Implementation.MethodArguments);
        Assert.Equal(resolved.Signature, result.Value.Implementation.Signature);
        int used = 65536 - budget.Remaining;
        Assert.True(resolver.ResolveClosedInterfaceMethodTarget(resolved, result.Value.Receiver, library, new(used)).Success);
        Assert.Contains(resolver.ResolveClosedInterfaceMethodTarget(resolved, result.Value.Receiver, library, new(used - 1)).Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }
    [Theory]
    [InlineData(false, false, 8)] [InlineData(true, false, 8)]
    [InlineData(false, true, 10)] [InlineData(true, true, 10)]
    [InlineData(false, true, 14)] [InlineData(true, true, 14)]
    public void Owner_and_method_arguments_remain_distinct(bool explicitBody, bool inherited, byte argument)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("GenericOwnerMethods", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var contract = b.Type("Test", "ISelect`1", arity: 1,
            attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        // U Select<U>(T ownerValue, U methodValue): both parameter spaces matter.
        byte[] signature = [0x30, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0];
        var declaration = b.Method("Select", signature, arity: 1,
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot,
            implementation: MethodImplAttributes.IL);
        var parent = b.Type("Test", "Parent`1", baseType: obj, arity: 1);
        var openContract = b.Specification(0x15, 0x12, (byte)(MetadataTokens.GetRowNumber(contract) << 2), 1, 0x13, 0);
        b.Implements(parent, openContract);
        var body = b.Method(explicitBody ? "Test.ISelect.Select" : "Select", signature, arity: 1,
            attributes: (explicitBody ? MethodAttributes.Private : MethodAttributes.Public)
                | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
            emit: il => { il.LoadArgument(2); il.OpCode(ILOpCode.Ret); });
        if (explicitBody) b.MethodImplementation(parent, body, b.Member(openContract, "Select", signature));
        var receiver = parent;
        if (inherited)
        {
            var openParent = b.Specification(0x15, 0x12, (byte)(MetadataTokens.GetRowNumber(parent) << 2), 1, 0x13, 0);
            receiver = b.Type("Test", "Child`1", baseType: openParent, arity: 1);
        }
        var closedContract = b.Specification(0x15, 0x12, (byte)(MetadataTokens.GetRowNumber(contract) << 2), 1, 14);
        var spec = b.Instantiate(b.Member(closedContract, "Select", signature), 0x0a, 1, argument);
        var path = files.Write("input.dll", b.Build());
        var image = resolver.OpenInput(path).Value!;
        var method = resolver.ResolveMethod(image, spec).Value!;
        var type = new ConstructedType(resolver.ResolveType(image, receiver).Value!, new([new TypeSignature.Primitive(PrimitiveTypeCode.String)]));
        var budget = new MemberResolutionBudget();
        var result = resolver.ResolveClosedInterfaceMethodTarget(method, type, library, budget);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(body, result.Value!.Implementation.Handle);
        Assert.Equal(method.Signature, result.Value.Implementation.Signature);
        Assert.Equal(method.MethodArguments, result.Value.Implementation.MethodArguments);
        Assert.Equal(type.Arguments, result.Value.Implementation.DeclaringType.Arguments);
        Assert.Equal(explicitBody, result.Value.IsExplicit);
        int used = 65536 - budget.Remaining;
        Assert.True(resolver.ResolveClosedInterfaceMethodTarget(method, type, library, new(used)).Success);
        Assert.Contains(resolver.ResolveClosedInterfaceMethodTarget(method, type, library, new(used - 1)).Diagnostics,
            d => d.Id == DiagnosticIds.TranslationLimit);
    }

}
