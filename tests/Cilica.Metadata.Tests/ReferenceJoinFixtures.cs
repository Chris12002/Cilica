using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

internal static class ReferenceJoinFixtures
{
    private const TypeAttributes Contract = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
    internal static byte[] Ref(TypeDefinitionHandle type)
    {
        var blob = new BlobBuilder(); blob.WriteByte(0x12);
        blob.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) << 2); return blob.ToArray();
    }
    private static MethodDefinitionHandle Source(ResolutionAssemblyBuilder b, string name, TypeDefinitionHandle type) =>
        b.Method(name, [0, 0, .. Ref(type)], emit: il => { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ret); });

    internal static ObjectFlowFixtures.Fixture Create(string shape, string order = "ABC")
    {
        var b = new ResolutionAssemblyBuilder(new("ReferenceJoins" + shape + order, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var root = b.Type("Example", "IRoot", attributes: Contract);
        var left = b.Type("Example", "ILeft", attributes: Contract); b.Implements(left, root);
        var right = b.Type("Example", "IRight", attributes: Contract); b.Implements(right, root);
        var extra = b.Type("Example", "IExtra", attributes: Contract);
        var a = b.Type("Example", "A", baseType: obj); b.Implements(a, left); b.Implements(a, extra);
        var c = b.Type("Example", "C", baseType: obj); b.Implements(c, root);
        var other = b.Type("Example", "B", baseType: obj); b.Implements(other, right); b.Implements(other, extra);
        b.Type("Example", "Driver", baseType: obj);
        TypeDefinitionHandle[] types = shape switch
        {
            "parents" => [left, right, root],
            "class_interface" => [a, left, root],
            "object" or "narrowing" => [a, obj, c],
            _ => [a, other, c],
        };
        var sources = types.Select((t, i) => Source(b, "Source" + i, t)).ToArray();
        var accept = b.Method("Accept", [0, 1, 8, .. Ref(shape == "object" ? obj : root)], emit: il =>
        { il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret); });
        var entry = b.Method("Run", [0, 1, 8, 8], locals: [7, 1, 8], emit: il =>
        {
            var first = il.DefineLabel(); var second = il.DefineLabel(); var join = il.DefineLabel();
            var back = il.DefineLabel(); var end = il.DefineLabel(); var forward = il.DefineLabel();
            il.LoadArgument(0); il.StoreLocal(0);
            if (shape == "loop")
            {
                il.Call(sources[order[0] - 'A']); il.Branch(ILOpCode.Br, join);
                il.MarkLabel(back); il.OpCode(ILOpCode.Pop);
            }
            il.LoadLocal(0); il.Branch(ILOpCode.Brfalse, first);
            if (shape != "ambiguous")
            {
                il.LoadLocal(0); il.LoadConstantI4(1); il.Branch(ILOpCode.Beq, second);
                il.Call(sources[order[2] - 'A']); il.Branch(ILOpCode.Br, join);
                il.MarkLabel(second);
            }
            il.Call(sources[order[1] - 'A']); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(first); il.Call(sources[order[0] - 'A']); il.Branch(ILOpCode.Br, join);
            il.MarkLabel(join);
            if (shape == "loop")
            {
                il.LoadLocal(0); il.Branch(ILOpCode.Brfalse, end);
                il.LoadLocal(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(0);
                il.Branch(ILOpCode.Br, back); il.MarkLabel(end);
            }
            // Forwarding must carry the complete intersection, not coarse Object.
            il.Branch(ILOpCode.Br, forward); il.MarkLabel(forward);
            il.Call(accept); il.OpCode(ILOpCode.Ret);
        });
        return new(b, entry);
    }

    internal static ObjectFlowFixtures.Fixture Budget(bool excess)
    {
        var b = new ResolutionAssemblyBuilder(new("JoinBudget" + excess, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); var parent = obj; var leaf = obj;
        for (int i = 0; i < 64; i++) { parent = leaf; leaf = b.Type("Example", "Level" + i, baseType: parent); }
        b.Type("Example", "Driver", baseType: obj);
        var first = Source(b, "First", parent); var second = Source(b, "Second", leaf);
        int width = excess ? 64 : 4;
        var entry = b.Method("Run", [0, 1, 8, 8], maxStack: width, emit: il =>
        {
            var other = il.DefineLabel(); var join = il.DefineLabel();
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue, other);
            il.Call(first); for (int i = 1; i < width; i++) il.OpCode(ILOpCode.Dup);
            il.Branch(ILOpCode.Br, join); il.MarkLabel(other);
            il.Call(second); for (int i = 1; i < width; i++) il.OpCode(ILOpCode.Dup);
            il.Branch(ILOpCode.Br, join); il.MarkLabel(join);
            for (int i = 0; i < 32; i++) { var hop = il.DefineLabel(); il.Branch(ILOpCode.Br, hop); il.MarkLabel(hop); }
            for (int i = 0; i < width; i++) il.OpCode(ILOpCode.Pop);
            il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret);
        });
        return new(b, entry);
    }

    internal static ObjectIrProgram LiveLoop()
    {
        var b = new ResolutionAssemblyBuilder(new("LiveReferenceJoin", new Version(1, 0, 0, 0)));
        TypeReferenceHandle Import(Type type)
        {
            var name = type.Assembly.GetName(); var key = name.GetPublicKeyToken();
            return b.TypeReference(type.Namespace!, type.Name, b.Reference(new(name.Name!, name.Version!,
                publicKeyToken: key is { Length: > 0 } ? Convert.ToHexString(key) : null)));
        }
        var obj = Import(typeof(object)); var left = Import(typeof(JoinContractLeft));
        var right = Import(typeof(JoinContractRight)); var contract = Import(typeof(IJoinRead));
        var node = Import(typeof(ProbeNode));
        var firstCtor = b.Member(left, ".ctor", 0x20, 1, 1, 8);
        var nextCtor = b.Member(right, ".ctor", 0x20, 1, 1, 8);
        var nodeCtor = b.Member(node, ".ctor", 0x20, 1, 1, 8);
        var read = b.Member(contract, "Read", 0x20, 1, 8, 8);
        b.Type("Example", "Driver", baseType: obj);
        var entry = b.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, 8], emit: il =>
        {
            var loop = il.DefineLabel(); var end = il.DefineLabel();
            il.LoadArgument(0); il.StoreLocal(0);
            il.LoadArgument(1); il.OpCode(ILOpCode.Newobj); il.Token(firstCtor); il.Branch(ILOpCode.Br, loop);
            il.MarkLabel(loop);
            // Keep the joined reference on the stack across a fresh allocation.
            il.LoadLocal(0); il.OpCode(ILOpCode.Newobj); il.Token(nodeCtor); il.OpCode(ILOpCode.Pop);
            il.LoadLocal(0); il.LoadConstantI4(0); il.Branch(ILOpCode.Ble, end);
            il.OpCode(ILOpCode.Pop); il.LoadArgument(1); il.OpCode(ILOpCode.Newobj); il.Token(nextCtor);
            il.LoadLocal(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(0); il.Branch(ILOpCode.Br, loop);
            il.MarkLabel(end); il.LoadArgument(1); il.OpCode(ILOpCode.Callvirt); il.Token(read); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            Path.GetDirectoryName(typeof(ReferenceJoinProbe).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(files.Write("loop.dll", b.Build())).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Methods.Single(m => IrMethodId.From(m.Source) == result.Program.Flow.Entry)
            .Blocks.SelectMany(block => block.Inputs), t => t.Class?.Token == typeof(IJoinRead).MetadataToken
                && t.Class.ContentHash != image.ContentHash);
        return result.Program;
    }
}
