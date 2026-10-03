using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class MixedDispatchTests
{
    internal static ObjectIrProgram CrossImage()
    {
        const MethodAttributes ctorFlags = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        const MethodAttributes virtualFlags = MethodAttributes.Public | MethodAttributes.Virtual;
        var core = new ResolutionAssemblyBuilder(new("MixedCore", new Version(1, 0, 0, 0)));
        var obj = core.Type("System", "Object");
        var objCtor = core.Method(".ctor", [0x20, 0, 1], attributes: ctorFlags, emit: il => il.OpCode(ILOpCode.Ret));
        core.Type("Example", "Parent", baseType: obj);
        core.Method(".ctor", [0x20, 0, 1], attributes: ctorFlags,
            emit: il => { il.LoadArgument(0); il.Call(objCtor); il.OpCode(ILOpCode.Ret); });
        core.Method("Read", [0x20, 0, 8], attributes: virtualFlags | MethodAttributes.NewSlot,
            emit: il => { il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret); });
        core.Type("Example", "IRead", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        core.Method("Read", [0x20, 0, 8], attributes: virtualFlags | MethodAttributes.NewSlot | MethodAttributes.Abstract);
        var app = new ResolutionAssemblyBuilder(new("MixedApp", new Version(1, 0, 0, 0)));
        var reference = app.Reference(new("MixedCore", new Version(1, 0, 0, 0)));
        var parent = app.TypeReference("Example", "Parent", reference);
        var contract = app.TypeReference("Example", "IRead", reference);
        var baseCtor = app.Member(parent, ".ctor", 0x20, 0, 1);
        var classRead = app.Member(parent, "Read", 0x20, 0, 8);
        var interfaceRead = app.Member(contract, "Read", 0x20, 0, 8);
        var child = app.Type("Example", "Child", baseType: parent); app.Implements(child, contract);
        var constructor = app.Method(".ctor", [0x20, 0, 1], attributes: ctorFlags,
            emit: il => { il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); });
        app.Method("Read", [0x20, 0, 8], attributes: virtualFlags,
            emit: il => { il.LoadConstantI4(31); il.OpCode(ILOpCode.Ret); });
        var explicitBody = app.Method("IRead.Read", [0x20, 0, 8],
            attributes: MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
            emit: il => { il.LoadConstantI4(19); il.OpCode(ILOpCode.Ret); });
        app.MethodImplementation(child, explicitBody, interfaceRead);
        app.Type("Example", "Driver");
        var entry = app.Method("Run", [0, 0, 8], locals: [7, 1, 0x12, (byte)(MetadataTokens.GetRowNumber(child) * 4)], emit: il =>
        {
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.StoreLocal(0);
            il.LoadLocal(0); il.OpCode(ILOpCode.Callvirt); il.Token(classRead);
            il.LoadLocal(0); il.OpCode(ILOpCode.Callvirt); il.Token(interfaceRead);
            il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        var corePath = files.Write("core.dll", core.Build()); var appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var image = resolver.OpenInput(appPath).Value!; var runtime = resolver.OpenInput(corePath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Dispatch, m => m.Declaration.ContentHash == runtime.ContentHash
            && m.Targets.Any(t => t.Implementation.ContentHash == image.ContentHash));
        Assert.Equal(runtime.ContentHash, Assert.Single(result.Program.InterfaceDispatch!.Declarations).Source.ContentHash);
        return result.Program;
    }

    [Theory]
    [InlineData("private", true)]
    [InlineData("public", true)]
    [InlineData("unused_body", true)]
    [InlineData("class_mapping", false)]
    [InlineData("mixed_rows", false)]
    [InlineData("unimplemented", false)]
    [InlineData("duplicate", false)]
    [InlineData("signature", false)]
    [InlineData("wrong_owner", false)]
    [InlineData("private_contract", false)]
    public void Class_only_calls_validate_every_mapping_without_reading_unused_interface_IL(string shape, bool success)
    {
        const MethodAttributes ctor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        const MethodAttributes virtualMethod = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot;
        var builder = new ResolutionAssemblyBuilder(new("MixedMappings", new Version(1, 0, 0, 0)));
        var obj = builder.Type("System", "Object");
        var objectCtor = builder.Method(".ctor", [0x20, 0, 1], attributes: ctor, emit: il => il.OpCode(ILOpCode.Ret));
        var contract = builder.Type("", "IRead", attributes: TypeAttributes.Interface | TypeAttributes.Abstract
            | (shape == "private_contract" ? TypeAttributes.NotPublic : TypeAttributes.Public));
        var declaration = builder.Method("Read", [0x20, 0, 8], attributes: virtualMethod | MethodAttributes.Abstract);
        builder.Type("", "Other", baseType: obj);
        var foreign = builder.Method("Read", [0x20, 0, 8], attributes: virtualMethod, emit: il => { il.LoadConstantI4(9); il.OpCode(ILOpCode.Ret); });
        var parent = builder.Type("", "Parent", baseType: obj);
        if (shape != "unimplemented") builder.Implements(parent, contract);
        var ordinary = builder.Method("Read", [0x20, 0, 8], attributes: virtualMethod,
            emit: il => { il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret); });
        var mapped = builder.Method("Mapped", shape == "signature" ? [0x20, 0, 1] : [0x20, 0, 8],
            attributes: shape == "public" ? virtualMethod : MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
            emit: il => { if (shape != "signature") il.LoadConstantI4(1); if (shape == "unused_body") il.OpCode(ILOpCode.Neg); il.OpCode(ILOpCode.Ret); });
        builder.MethodImplementation(parent, shape == "wrong_owner" ? foreign : mapped, shape == "class_mapping" ? ordinary : declaration);
        if (shape is "duplicate" or "mixed_rows") builder.MethodImplementation(parent, mapped, shape == "duplicate" ? declaration : ordinary);
        var constructor = builder.Method(".ctor", [0x20, 0, 1], attributes: ctor,
            emit: il => { il.LoadArgument(0); il.Call(objectCtor); il.OpCode(ILOpCode.Ret); });
        // The mapping is inherited, so validation must inspect ancestors as well.
        var child = builder.Type("", "Child", baseType: parent);
        builder.Type("", "Driver", baseType: obj);
        var entry = builder.Method("Run", [0, 0, 8], emit: il =>
        { il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Callvirt); il.Token(ordinary); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("mappings.dll", builder.Build())).Value!;
        var receiver = resolver.ResolveType(image, child).Value!;
        var budget = new MemberResolutionBudget();
        var exact = resolver.ResolveClassVirtualTarget(image, ordinary, receiver, budget);
        Assert.Equal(success, exact.Success);
        var translated = ObjectTranslator.Translate(resolver, image, entry, image);
        Assert.Equal(success, translated.Success);
        if (!success)
        {
            Assert.Null(exact.Value); Assert.NotEmpty(exact.Diagnostics); Assert.Null(translated.Program);
            Assert.Contains("Reachable via:", Assert.Single(translated.Diagnostics).Message, StringComparison.Ordinal);
            return;
        }
        Assert.Equal(ordinary, exact.Value!.Implementation.Handle);
        int consumed = 65536 - budget.Remaining;
        var sufficient = new MemberResolutionBudget(consumed);
        Assert.True(resolver.ResolveClassVirtualTarget(image, ordinary, receiver, sufficient).Success);
        Assert.Equal(0, sufficient.Remaining);
        var shortBudget = resolver.ResolveClassVirtualTarget(image, ordinary, receiver, new(consumed - 1));
        Assert.Null(shortBudget.Value);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(shortBudget.Diagnostics).Id);
        Assert.DoesNotContain(translated.Program!.Flow.Methods, m => m.Source.Token == MetadataTokens.GetToken(mapped));
        ObjectCppEmitter.EmitProgram(translated.Program);
    }

    internal static ObjectIrProgram Translate(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(MixedDispatchProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(MixedDispatchProbe).GetMethod(name)!.MetadataToken), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Program!;
    }

    [Theory]
    [InlineData(nameof(MixedDispatchProbe.ClassOnly))]
    [InlineData(nameof(MixedDispatchProbe.ClassFirst))]
    [InlineData(nameof(MixedDispatchProbe.InterfaceFirst))]
    [InlineData(nameof(MixedDispatchProbe.Allocating))]
    public void Mixed_closures_validate_without_turning_explicit_bodies_into_public_calls(string name)
    {
        var program = Translate(name);
        Assert.NotEmpty(program.Dispatch);
        ObjectIrValidator.Validate(program);
        ObjectCppEmitter.EmitProgram(program);
        if (name == nameof(MixedDispatchProbe.ClassOnly))
        {
            Assert.Null(program.InterfaceDispatch);
            Assert.DoesNotContain(program.Bindings, b => (b.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private);
        }
        else
        {
            Assert.NotNull(program.InterfaceDispatch);
            Assert.Contains(program.Bindings, b => (b.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private);
        }
        if (name == nameof(MixedDispatchProbe.Allocating))
        {
            var late = program.Flow.Types.Types.Single(t => t.Source.Token == typeof(MixedFactoryLate).MetadataToken);
            Assert.Contains(program.Dispatch.SelectMany(m => m.Targets), t => t.Receiver == ObjectTypeId.From(late.Source)
                && t.Implementation.Token == typeof(MixedFactoryLate).GetMethod(nameof(MixedFactoryLate.Make))!.MetadataToken);
            Assert.Contains(program.InterfaceDispatch!.Dispatch.SelectMany(m => m.Targets), t => t.Receiver == ObjectTypeId.From(late.Source));
        }
    }
}
