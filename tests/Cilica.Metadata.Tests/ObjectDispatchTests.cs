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

public sealed class ObjectDispatchTests
{
    private const MethodAttributes Constructor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    internal static ObjectIrProgram Translate(string name = nameof(ClassDispatchProbe.Run))
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ClassDispatchProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ClassDispatchProbe).GetMethod(name)!.MetadataToken), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }

    [Theory]
    [InlineData(nameof(ClassDispatchProbe.Run))]
    [InlineData(nameof(ClassDispatchProbe.ArgumentOrder))]
    [InlineData(nameof(ClassDispatchProbe.Allocating))]
    [InlineData(nameof(ClassDispatchProbe.Recursive))]
    public void Class_dispatch_maps_are_complete_and_emittable(string name)
    {
        var program = Translate(name);
        Assert.NotEmpty(program.Dispatch);
        ObjectIrValidator.Validate(program);
        Assert.Contains("_dispatch_table", ObjectCppEmitter.EmitProgram(program).Text, StringComparison.Ordinal);
        foreach (var map in program.Dispatch)
        {
            Assert.Equal(map.Targets.Length, map.Targets.Select(t => t.Receiver).Distinct().Count());
            foreach (var target in map.Targets)
                Assert.Equal(program.Bindings.Single(b => b.Method == map.Declaration).VirtualSlot,
                    program.Bindings.Single(b => b.Method == target.Implementation).VirtualSlot);
        }
        if (name == nameof(ClassDispatchProbe.Allocating))
        {
            var late = program.Flow.Types.Types.Single(t => t.Source.Token == typeof(VirtualNodeLate).MetadataToken);
            int lateMake = typeof(VirtualNodeLate).GetMethod(nameof(VirtualNodeLate.Make))!.MetadataToken;
            Assert.Contains(program.Dispatch.SelectMany(m => m.Targets), t => t.Receiver == ObjectTypeId.From(late.Source) && t.Implementation.Token == lateMake);
            Assert.Contains(program.Flow.Methods, m => m.Source.Token == lateMake);
            Assert.Contains(program.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements), s => s is IrStatement.Invoke { VirtualDispatch: true });
        }
    }

    [Theory]
    [InlineData("null_check")]
    [InlineData("dispatch_flag")]
    [InlineData("fault_edge")]
    public void Void_virtual_calls_cannot_bypass_dispatch_or_fault_validation(string damage)
    {
        var program = Translate(nameof(ClassDispatchProbe.Allocating));
        var method = program.Flow.Methods.First(m => m.Blocks.SelectMany(b => b.Statements).Any(s => s is IrStatement.Invoke { VirtualDispatch: true }));
        var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.Invoke { VirtualDispatch: true }));
        var original = block.Statements.OfType<IrStatement.Invoke>().First(s => s.VirtualDispatch);
        var changed = damage switch { "null_check" => original with { NullCheckReceiver = false },
            "dispatch_flag" => original with { VirtualDispatch = false }, _ => original with { Exceptional = (IrExceptionTarget)99 } };
        program = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Replace(method,
            method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(original, changed) }) }) } };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program));
    }

    [Fact]
    public void Unsupported_override_bodies_fail_with_the_virtual_inclusion_path()
    {
        var builder = new ResolutionAssemblyBuilder(new("UnsupportedOverride", new Version(1, 0, 0, 0)));
        var objectBase = builder.Type("System", "Object");
        var objectCtor = builder.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il => il.OpCode(ILOpCode.Ret));
        var parent = builder.Type("Example", "Parent", baseType: objectBase);
        var baseCtor = builder.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        { il.LoadArgument(0); il.Call(objectCtor); il.OpCode(ILOpCode.Ret); });
        var declaration = builder.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: il =>
        { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        builder.Type("Example", "Leaf", baseType: parent);
        var leafCtor = builder.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        { il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); });
        var unsupported = builder.Method("Read", [0x20, 0, 8], attributes: Virtual, emit: il =>
        { il.LoadConstantI4(2); il.OpCode(ILOpCode.Conv_i8); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(2); il.OpCode(ILOpCode.Ret); });
        builder.Type("Example", "Driver", baseType: objectBase);
        var root = builder.Method("Run", [0, 0, 8], emit: il =>
        { il.OpCode(ILOpCode.Newobj); il.Token(leafCtor); il.OpCode(ILOpCode.Callvirt); il.Token(declaration); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("unsupported.dll", builder.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, root, image);
        Assert.Null(result.Program);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("conv.i8", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("::Run", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(MetadataTokens.GetToken(unsupported).ToString("x8", System.Globalization.CultureInfo.InvariantCulture), diagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Cross_image_slot_and_body_tokens_retain_distinct_identities() => ObjectIrValidator.Validate(CrossImage());

    internal static ObjectIrProgram CrossImage()
    {
        var identity = new AssemblyIdentity("DispatchCore", new Version(1, 0, 0, 0));
        var core = new ResolutionAssemblyBuilder(identity);
        var objectBase = core.Type("System", "Object");
        var objectCtor = core.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il => il.OpCode(ILOpCode.Ret));
        core.Type("Example", "SameName", baseType: objectBase);
        core.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        { il.LoadArgument(0); il.Call(objectCtor); il.OpCode(ILOpCode.Ret); });
        var declaration = core.Method("Read", [0x20, 0, 8], attributes: Virtual | MethodAttributes.NewSlot, emit: il =>
        { il.LoadConstantI4(11); il.OpCode(ILOpCode.Ret); });
        var app = new ResolutionAssemblyBuilder(new("DispatchApp", new Version(1, 0, 0, 0)));
        var reference = app.Reference(identity);
        var objectRef = app.TypeReference("System", "Object", reference);
        var parentRef = app.TypeReference("Example", "SameName", reference);
        var baseCtor = app.Member(parentRef, ".ctor", 0x20, 0, 1);
        var baseRead = app.Member(parentRef, "Read", 0x20, 0, 8);
        app.Type("Example", "Padding", baseType: objectRef);
        app.Method("Padding", [0, 0, 1], emit: il => il.OpCode(ILOpCode.Ret));
        app.Type("Example", "SameName", baseType: parentRef);
        var leafCtor = app.Method(".ctor", [0x20, 0, 1], attributes: Constructor, emit: il =>
        { il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); });
        var replacement = app.Method("Read", [0x20, 0, 8], attributes: Virtual, emit: il =>
        { il.LoadConstantI4(42); il.OpCode(ILOpCode.Ret); });
        app.Type("Example", "Driver", baseType: objectRef);
        var entry = app.Method("Run", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Newobj); il.Token(leafCtor); il.OpCode(ILOpCode.Callvirt); il.Token(baseRead);
            il.OpCode(ILOpCode.Newobj); il.Token(leafCtor); il.Call(baseRead); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs");
        var runtime = resolver.OpenInput(corePath).Value!; var image = resolver.OpenInput(appPath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(declaration, replacement);
        var map = Assert.Single(result.Program!.Dispatch);
        Assert.Equal(runtime.ContentHash, map.Declaration.ContentHash);
        Assert.Contains(map.Targets, t => t.Receiver.ContentHash == image.ContentHash && t.Implementation.ContentHash == image.ContentHash
            && t.Implementation.Token == map.Declaration.Token);
        var namesakes = result.Program.Flow.Types.Types.Where(t => t.Source.Token == 0x02000003).ToArray();
        Assert.Equal(2, namesakes.Length); Assert.Equal(namesakes[0].Source.Name, namesakes[1].Source.Name);
        Assert.NotEqual(namesakes[0].Source.ContentHash, namesakes[1].Source.ContentHash);
        return result.Program;
    }

    [Theory]
    [InlineData("missing_map")]
    [InlineData("extra_map")]
    [InlineData("duplicate_map")]
    [InlineData("default_targets")]
    [InlineData("empty_targets")]
    [InlineData("missing_receiver")]
    [InlineData("duplicate_receiver")]
    [InlineData("unknown_receiver")]
    [InlineData("unknown_body")]
    [InlineData("wrong_body")]
    [InlineData("slot_owner")]
    [InlineData("slot_identity")]
    [InlineData("missing_slot")]
    [InlineData("name")]
    [InlineData("final")]
    [InlineData("missing_null_check")]
    [InlineData("missing_dispatch_flag")]
    [InlineData("missing_fault_edge")]
    public void Forged_dispatch_IR_is_rejected_before_source_is_returned(string damage)
    {
        var program = Translate(); var originalMap = program.Dispatch[0]; var map = originalMap;
        var methods = program.Flow.Methods; var bindings = program.Bindings;
        switch (damage)
        {
            case "missing_map": program = program with { Dispatch = program.Dispatch.Remove(map) }; break;
            case "duplicate_map": program = program with { Dispatch = program.Dispatch.Add(map) }; break;
            case "extra_map":
                var unused = bindings.First(b => b.VirtualSlot is not null && program.Dispatch.All(m => m.Declaration != b.Method));
                program = program with { Dispatch = program.Dispatch.Add(map with { Declaration = unused.Method }) }; break;
            case "default_targets": map = map with { Targets = default }; break;
            case "empty_targets": map = map with { Targets = [] }; break;
            case "missing_receiver": map = map with { Targets = map.Targets.RemoveAt(0) }; break;
            case "duplicate_receiver": map = map with { Targets = map.Targets.Add(map.Targets[0]) }; break;
            case "unknown_receiver": map = map with { Targets = map.Targets.SetItem(0, map.Targets[0] with { Receiver = new(new string('a', 64), 0x02000100) }) }; break;
            case "unknown_body": map = map with { Targets = map.Targets.SetItem(0, map.Targets[0] with { Implementation = new(new string('a', 64), 0x06000100) }) }; break;
            case "wrong_body": map = map with { Targets = map.Targets.SetItem(0, map.Targets[0] with { Implementation = program.Flow.Entry }) }; break;
            case "slot_owner" or "slot_identity" or "missing_slot" or "name" or "final":
                var binding = bindings.Single(b => b.Method == map.Declaration);
                var changed = damage switch
                {
                    "slot_owner" => binding with { VirtualSlot = binding.VirtualSlot! with { Owner = program.Flow.Types.ObjectBase } },
                    "slot_identity" => binding with { VirtualSlot = binding.VirtualSlot! with { Method = binding.Method with { Token = 0x04000001 } } },
                    "missing_slot" => binding with { VirtualSlot = null },
                    "name" => binding with { VirtualName = "wrong" },
                    _ => binding with { Attributes = binding.Attributes | System.Reflection.MethodAttributes.Final },
                };
                program = program with { Bindings = bindings.Replace(binding, changed) }; break;
            default:
                var method = methods.First(m => m.Blocks.SelectMany(b => b.Values).Any(v => v.Expression is IrExpression.Call { VirtualDispatch: true }));
                var block = method.Blocks.First(b => b.Values.Any(v => v.Expression is IrExpression.Call { VirtualDispatch: true }));
                var original = block.Statements.OfType<IrStatement.Define>().First(d => d.Value.Expression is IrExpression.Call { VirtualDispatch: true });
                var call = (IrExpression.Call)original.Value.Expression;
                var modified = damage == "missing_fault_edge" ? original with { Exceptional = null }
                    : original with { Value = original.Value with { Expression = damage == "missing_null_check" ? call with { NullCheckReceiver = false } : call with { VirtualDispatch = false } } };
                program = program with { Flow = program.Flow with { Methods = methods.Replace(method,
                    method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(original, modified) }) }) } };
                break;
        }
        if (map != originalMap)
            program = program with { Dispatch = program.Dispatch.Replace(program.Dispatch[0], map) };
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program));
    }
}
