using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Tests;

internal static class ObjectFlowFixtures
{
    internal sealed record Fixture(ResolutionAssemblyBuilder Builder, MethodDefinitionHandle Root);
    internal static Fixture Create(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("ObjectFlow" + kind, new Version(1, 0, 0, 0)));
        var objectBase = b.Type("System", "Object");
        const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var baseCtor = b.Method(".ctor", [0x20, 0, 1], attributes: flags, emit: il => il.OpCode(ILOpCode.Ret));
        var node = b.Type("Test", "Node", baseType: objectBase);
        byte nodeEncoded = checked((byte)(MetadataTokens.GetRowNumber(node) << 2));
        var field = b.FieldWithAttributes("Value", FieldAttributes.Public, 6, 8);
        var ctor = b.Method(".ctor", [0x20, 1, 1, 8], attributes: flags, emit: il =>
        {
            il.LoadArgument(0); il.Call(baseCtor); var done = il.DefineLabel();
            if (kind == "ctor_branch") { il.LoadArgument(1); il.Branch(ILOpCode.Brtrue_s, done); }
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Stfld); il.Token(field);
            il.MarkLabel(done); il.OpCode(ILOpCode.Ret);
        });
        var identity = b.Method("Identity", [0, 1, 0x12, nodeEncoded, 0x12, nodeEncoded], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var other = b.Type("Test", "Other", baseType: objectBase);
        var otherCtor = b.Method(".ctor", [0x20, 0, 1], attributes: flags, emit: il => { il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); });
        b.Type("Test", "Driver", baseType: objectBase);
        MethodDefinitionHandle root;
        if (kind is "budget" or "budget_excess")
            root = b.Method("Run", [0, 1, 8, 8], maxStack: 1801, emit: il =>
            {
                const int width = 1800;
                var hops = new System.Reflection.Metadata.Ecma335.LabelHandle[32];
                for (int i = 0; i < hops.Length; i++) hops[i] = il.DefineLabel();
                var end = il.DefineLabel();
                for (int i = 0; i < width; i++) il.OpCode(ILOpCode.Ldnull);
                il.Branch(ILOpCode.Br, hops[0]);
                for (int i = 0; i < hops.Length; i++)
                {
                    il.MarkLabel(hops[i]);
                    if (i == 1 && kind == "budget_excess") for (int n = 0; n < 5000; n++) il.OpCode(ILOpCode.Nop);
                    if (i + 1 < hops.Length) il.Branch(ILOpCode.Br, hops[i + 1]);
                }
                il.LoadArgument(0); il.Branch(ILOpCode.Brfalse, end);
                for (int i = 0; i < width; i++) il.OpCode(ILOpCode.Pop);
                il.LoadConstantI4(7); il.OpCode(ILOpCode.Newobj); il.Token(ctor);
                for (int i = 1; i < width; i++) il.OpCode(ILOpCode.Dup);
                il.Branch(ILOpCode.Br, hops[0]);
                il.MarkLabel(end); for (int i = 0; i < width; i++) il.OpCode(ILOpCode.Pop);
                il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
            });
        else if (kind == "swap")
            root = b.Method("Run", [0, 3, 8, 8, 8, 8], locals: [7, 3, 8, 0x12, nodeEncoded, 0x12, nodeEncoded], emit: il =>
            {
                var loop = il.DefineLabel(); var end = il.DefineLabel();
                il.LoadArgument(0); il.StoreLocal(0);
                il.LoadArgument(1); il.OpCode(ILOpCode.Newobj); il.Token(ctor);
                il.LoadArgument(2); il.OpCode(ILOpCode.Newobj); il.Token(ctor);
                il.Branch(ILOpCode.Br_s, loop);
                il.MarkLabel(loop); il.LoadLocal(0); il.Branch(ILOpCode.Brfalse_s, end);
                il.StoreLocal(2); il.StoreLocal(1); il.LoadLocal(2); il.LoadLocal(1);
                il.LoadLocal(0); il.OpCode(ILOpCode.Newobj); il.Token(ctor); il.OpCode(ILOpCode.Pop);
                il.LoadLocal(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(0); il.Branch(ILOpCode.Br_s, loop);
                il.MarkLabel(end); il.StoreLocal(2); il.StoreLocal(1);
                il.LoadLocal(1); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.LoadConstantI4(3); il.OpCode(ILOpCode.Mul);
                il.LoadLocal(2); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
            });
        else if (kind is "widen_loop" or "widen_call")
            root = b.Method("Run", [0, 1, 8, 8], locals: [7, 1, 8], emit: il =>
            {
                var loop = il.DefineLabel(); var end = il.DefineLabel();
                il.LoadArgument(0); il.StoreLocal(0); il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br_s, loop);
                il.MarkLabel(loop); if (kind == "widen_call") il.Call(identity);
                il.LoadLocal(0); il.Branch(ILOpCode.Brfalse_s, end);
                il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); il.OpCode(ILOpCode.Newobj); il.Token(ctor);
                il.LoadLocal(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.StoreLocal(0); il.Branch(ILOpCode.Br_s, loop);
                il.MarkLabel(end); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.OpCode(ILOpCode.Ret);
            });
        else
            root = b.Method("Run", [0, 1, 8, 8], emit: il =>
            {
                var taken = il.DefineLabel(); var join = il.DefineLabel();
                if (kind == "starg") { il.LoadConstantI4(0); il.OpCode(ILOpCode.Starg_s); il.CodeBuilder.WriteByte(0); }
                il.LoadArgument(0); il.Branch(ILOpCode.Brtrue_s, taken);
                il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Br_s, join);
                il.MarkLabel(taken);
                if (kind == "mixed") il.LoadConstantI4(3);
                else if (kind == "depth") { il.LoadConstantI4(0); il.LoadConstantI4(0); }
                else { il.LoadConstantI4(7); il.OpCode(ILOpCode.Newobj); il.Token(ctor); }
                il.Branch(ILOpCode.Br_s, join); il.MarkLabel(join);
                if (kind is "siblings" or "bad_sibling_use")
                {
                    // The first join widens null to Node; a backedge later widens it to Object.
                    var finish = il.DefineLabel();
                    if (kind == "bad_sibling_use") il.Call(identity);
                    il.LoadArgument(0); il.Branch(ILOpCode.Brfalse_s, finish);
                    il.OpCode(ILOpCode.Pop); il.OpCode(ILOpCode.Newobj); il.Token(otherCtor); il.Branch(ILOpCode.Br_s, join);
                    il.MarkLabel(finish);
                }
                if (kind == "ordered")
                {
                    var smaller = il.DefineLabel(); il.OpCode(ILOpCode.Ldnull); il.Branch(ILOpCode.Blt_un_s, smaller);
                    il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); il.MarkLabel(smaller); il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
                }
                else { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ceq); il.OpCode(ILOpCode.Ret); }
            });
        return new(b, root);
    }
}
