using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cilica.Metadata;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DelegateTranslationTests
{
    internal static DelegateValueTranslationResult Translate(string name = "Execute")
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(DelegateProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(DelegateProbe).GetMethod(name)!.MetadataToken);
        return ValueTranslator.TranslateDelegates(resolver, input, entry, library);
    }
    [Theory]
    [InlineData("Execute")]
    [InlineData("NullCapture")]
    [InlineData("NullOnly")]
    public void Delegate_metadata_and_CIL_lower_to_checked_scalar_operations(string name)
    {
        var result = Translate(name); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow.Flow;
        Assert.NotEmpty(flow.Types.References.Delegates);
        Assert.DoesNotContain(flow.Methods, m => m.Source.Name.EndsWith("::Invoke", StringComparison.Ordinal));
        Assert.Contains(flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.CallDelegate);
        ScalarDelegateIrValidator.Validate(ValueScalarLowering.LowerDelegates(program));
    }
    [Fact]
    public void Multicast_requires_an_explicit_supported_lowering()
    {
        var result = Translate("UnsupportedMulticast"); Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
    [Theory]
    [InlineData("ctor-signature")]
    [InlineData("invoke-body")]
    [InlineData("invoke-signature")]
    [InlineData("unpaired")]
    [InlineData("branch-constructor")]
    [InlineData("virtual-pointer")]
    [InlineData("target-signature")]
    [InlineData("private-type")]
    public void Invalid_delegate_declarations_and_pointer_patterns_are_diagnostics(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("DelegateInvalid" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var multicast = b.Type("System", "MulticastDelegate", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        b.Type("Test", "Callable", baseType: multicast, attributes: TypeAttributes.Sealed | (kind == "private-type" ? TypeAttributes.NotPublic : TypeAttributes.Public));
        var constructor = b.Method(".ctor", [0x20, 2, 1, 0x1c, kind == "ctor-signature" ? (byte)8 : (byte)0x18],
            attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        var invoke = b.Method("Invoke", [0x20, 1, 8, kind == "invoke-signature" ? (byte)0x1c : (byte)8],
            attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            emit: kind == "invoke-body" ? il => { il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); } : null);
        b.Type("Test", "Driver", baseType: obj);
        var target = b.Method("Target", [0, 1, kind == "target-signature" ? (byte)1 : (byte)8, 8],
            emit: il => { if (kind != "target-signature") il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var entry = b.Method("Execute", [0, 0, 8], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull);
            il.OpCode(kind == "virtual-pointer" ? ILOpCode.Ldvirtftn : ILOpCode.Ldftn); il.Token(target);
            if (kind == "unpaired") il.OpCode(ILOpCode.Nop);
            if (kind == "branch-constructor") { var label = il.DefineLabel(); il.Branch(ILOpCode.Br_s, label); il.MarkLabel(label); }
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.LoadConstantI4(1);
            il.OpCode(ILOpCode.Callvirt); il.Token(invoke); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var input = resolver.OpenInput(files.Write("delegate.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateDelegates(resolver, input, entry, input);
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }

}
