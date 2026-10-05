using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ConstrainedValueTranslationTests
{
    [Theory]
    [InlineData("Mutation")] [InlineData("Generic")]
    public void Constrained_body_selection_retains_checked_receivers_and_legacy_gates(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ConstrainedValueProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        Assert.DoesNotContain(result.Program!.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values),
            v => v.Expression is IrExpression.BoxValue);
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ConstrainedValueProbe)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Constrained_value_calls_preserve_mutation_collection_and_exception_order(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized, [("Mutation", ConstrainedValueProbe.Mutation),
            ("Generic", ConstrainedValueProbe.Generic)], typeof(ConstrainedValueProbe));

    [Fact]
    public void Unimplemented_boxing_fallback_remains_actionable()
    {
        var result = IntegerObjectTranslationTests.Translate("Fallback", fixture: typeof(ConstrainedValueProbe));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("boxing", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Closed_interface_CIL_calls_the_direct_body_without_boxing_the_receiver(bool optimized)
    {
        var result = Authored("interface"); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("Interface", result.Program!, ConstrainedValueProbe.InterfaceOracle)]);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("tail")] [InlineData("call")] [InlineData("branch-call")]
    [InlineData("reference-stack")] [InlineData("private-interface")]
    public void Invalid_prefixes_receivers_and_private_body_access_remain_rejected(string shape)
    {
        var result = Authored(shape);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
        if (shape == "private-interface")
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("Calls require", StringComparison.Ordinal));
    }

    private static ManagedExceptionTranslationResult Authored(string shape)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            Path.GetDirectoryName(typeof(ConstrainedValueProbe).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var fixture = resolver.OpenInput(typeof(ConstrainedValueProbe).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("ConstrainedCalls", new Version(1, 0, 0, 0)));
        var obj = b.TypeReference("System", "Object", b.Reference(library.Identity));
        var source = b.Reference(fixture.Identity);
        bool interfaceCall = shape is "interface" or "private-interface";
        bool closedGeneric = shape == "interface";
        EntityHandle value = b.TypeReference("Cilica.Fixtures.Execution", closedGeneric ? "DirectConstrainedValue`1" : "ExplicitConstrainedValue", source);
        byte[] valueSignature;
        if (closedGeneric)
        {
            valueSignature = [0x15, 0x11, checked((byte)((MetadataTokens.GetRowNumber(value) << 2) | 1)), 1, 8];
            value = b.Specification(valueSignature);
        }
        else valueSignature = [0x11, checked((byte)((MetadataTokens.GetRowNumber(value) << 2) | 1))];
        var hash = b.Member(obj, "GetHashCode", 0x20, 0, 8);
        EntityHandle call = hash;
        EntityHandle field = default;
        if (interfaceCall)
        {
            var contract = b.TypeReference("Cilica.Fixtures.Execution", "IDirectConstrainedValue`1", source);
            var closed = b.Specification(0x15, 0x12, checked((byte)((MetadataTokens.GetRowNumber(contract) << 2) | 1)), 1, 8);
            call = b.Member(closed, "Change", 0x20, 1, 0x13, 0, 0x13, 0);
            field = closedGeneric ? b.Member(value, "Item", 6, 0x13, 0) : b.Member(value, "Number", 6, 8);
        }
        b.Type("Test", "Program", baseType: obj);
        var entry = b.Method("Run", [0, 2, 8, 8, 8], locals: [7, 1, .. valueSignature], emit: il => {
            if (interfaceCall)
            {
                il.LoadLocalAddress(0); il.LoadArgument(0); il.OpCode(ILOpCode.Stfld); il.Token(field);
            }
            if (shape == "reference-stack") il.OpCode(ILOpCode.Ldnull); else il.LoadLocalAddress(0);
            if (interfaceCall) il.LoadArgument(1);
            var target = il.DefineLabel();
            if (shape == "branch-call") il.Branch(ILOpCode.Br_s, target);
            if (shape == "tail") il.OpCode(ILOpCode.Tail);
            if (shape == "duplicate") { il.OpCode(ILOpCode.Constrained); il.Token(value); }
            il.OpCode(ILOpCode.Constrained); il.Token(value);
            il.MarkLabel(target); il.OpCode(shape == "call" ? ILOpCode.Call : ILOpCode.Callvirt); il.Token(call);
            if (interfaceCall) { il.LoadLocalAddress(0); il.OpCode(ILOpCode.Ldfld); il.Token(field); il.OpCode(ILOpCode.Add); }
            il.OpCode(ILOpCode.Ret);
        });
        var image = resolver.OpenInput(files.Write("ConstrainedCalls.dll", b.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(resolver, image, entry, library);
    }
}
