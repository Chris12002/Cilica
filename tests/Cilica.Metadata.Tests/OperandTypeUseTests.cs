using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class OperandTypeUseTests
{
    [Fact]
    public void Closed_snapshots_preserve_declared_variables_and_explicit_operand_names()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("OperandOrigins", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var hidden = b.Type("Test", "Hidden", baseType: obj, attributes: TypeAttributes.NotPublic);
        var carrier = b.Type("Test", "Carrier`1", baseType: obj, arity: 1);
        var field = b.Field("Value", 6, 0x13, 0);
        b.Method("Copy", [0, 1, 0x13, 0, 0x13, 0], locals: [7, 1, 0x13, 0], emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var closedCarrier = b.Specification(0x15, 0x12, checked((byte)(MetadataTokens.GetRowNumber(carrier) << 2)), 1, 0x12, checked((byte)(MetadataTokens.GetRowNumber(hidden) << 2)));
        var call = b.Member(closedCarrier, "Copy", 0, 1, 0x13, 0, 0x13, 0);
        var variable = b.Specification(0x13, 0);
        var generic = b.Method("Identity", [0x10, 1, 1, 0x1e, 0, 0x1e, 0], arity: 1, emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        var forwarded = b.Instantiate(generic, 0x0a, 1, 0x13, 0);
        var image = resolver.OpenInput(files.Write("origins.dll", b.Build())).Value!;
        var caller = resolver.ResolveMethod(image, call).Value!;
        Assert.NotNull(caller);
        var input = resolver.ReadClosedMethodInput(caller, library, new());
        Assert.True(input.Success, string.Join("; ", input.Diagnostics));
        Assert.IsType<TypeSignature.Named>(Assert.Single(input.Value!.Signature.Parameters));
        Assert.Equal(new TypeSignature.Variable(false, 0), Assert.Single(input.Value.DeclarationSignature.Parameters));
        Assert.IsType<TypeSignature.Named>(Assert.Single(input.Value.Locals));
        Assert.Equal(new TypeSignature.Variable(false, 0), Assert.Single(input.Value.DeclarationLocals));
        var variableUse = resolver.ReadOperandTypeUses(caller, variable, new());
        var explicitUse = resolver.ReadOperandTypeUses(caller, hidden, new());
        Assert.Equal(new TypeSignature.Variable(false, 0), Assert.Single(variableUse.Value!.Signatures));
        var explicitName = Assert.IsType<TypeSignature.Named>(Assert.Single(explicitUse.Value!.Signatures));
        var substituted = Assert.IsType<TypeSignature.Named>(resolver.ResolveClosedTypeOperand(caller, variable).Value);
        Assert.Equal(explicitName.Definition, substituted.Definition);
        var member = resolver.ReadOperandTypeUses(caller, call, new()).Value!;
        Assert.IsType<TypeSignature.Named>(Assert.Single(Assert.IsType<TypeSignature.Generic>(member.Signatures[0]).Arguments));
        Assert.Equal(new TypeSignature.Variable(false, 0), member.Signatures[1]);
        Assert.Equal(new TypeSignature.Variable(false, 0), member.Signatures[2]);
        var fieldUse = resolver.ReadOperandTypeUses(caller, field, new()).Value!;
        Assert.Equal(new TypeSignature.Variable(false, 0), fieldUse.Signatures[1]);
        var genericUse = resolver.ReadOperandTypeUses(caller, forwarded, new(2));
        Assert.True(genericUse.Success, string.Join("; ", genericUse.Diagnostics));
        Assert.Equal(new TypeSignature.Variable(false, 0), genericUse.Value!.Signatures[0]);
        Assert.Contains(genericUse.Value.Signatures, s => s == new TypeSignature.Variable(true, 0));
        var shortBudget = resolver.ReadOperandTypeUses(caller, forwarded, new(1));
        Assert.False(shortBudget.Success);
        Assert.Contains(shortBudget.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
        Assert.False(resolver.ReadOperandTypeUses(caller, MetadataTokens.AssemblyReferenceHandle(1), new()).Success);
    }
}
