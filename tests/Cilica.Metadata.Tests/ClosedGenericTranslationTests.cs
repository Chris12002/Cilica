using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ClosedGenericTranslationTests
{
    internal static BoxedValueTranslationResult Translate(string method = "Execute", bool enabled = true)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ClosedGenericProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var entry = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ClosedGenericProbe).GetMethod(method)!.MetadataToken);
        return enabled ? ValueTranslator.TranslateGenerics(resolver, input, entry, library)
            : ValueTranslator.TranslateBoxes(resolver, input, entry, library);
    }

    [Fact]
    public void Closed_calls_preserve_original_provenance_and_distinct_specializations()
    {
        var result = Translate(); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var methods = result.Program!.Flow.Flow.Methods;
        var identity = methods.Where(m => m.Source.Token == typeof(ClosedGenericProbe).GetMethod("Identity")!.MetadataToken).ToArray();
        Assert.Equal(2, identity.Length);
        Assert.Equal(identity[0].Source.ContentHash, identity[1].Source.ContentHash);
        Assert.NotEqual(IrMethodId.From(identity[0].Source), IrMethodId.From(identity[1].Source));
        Assert.All(identity, m => Assert.Equal(64, m.Source.Specialization.Length));
        var recursive = Assert.Single(methods.Where(m => m.Source.Token == typeof(ClosedGenericProbe).GetMethod("Recurse")!.MetadataToken));
        Assert.Contains(recursive.Blocks.SelectMany(b => b.Values), v => v.Expression is IrExpression.Call call && call.Target == IrMethodId.From(recursive.Source));
        ScalarBoxedIrValidator.Validate(ValueScalarLowering.LowerBoxes(result.Program));
    }

    [Theory]
    [InlineData("Constraint", true, "constraints")]
    [InlineData("Expansion", true, "64")]
    [InlineData("Identity", true, "concrete")]
    [InlineData("Execute", false, "Generic")]
    public void Unsupported_or_unbounded_instantiations_return_diagnostics(string method, bool enabled, string message)
    {
        var result = Translate(method, enabled);
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.OrdinalIgnoreCase));
        if (method == "Expansion") Assert.Contains(result.Diagnostics, d => d.Message.Contains("Reachable via:", StringComparison.Ordinal));
    }

    [Fact]
    public void Specialization_keys_distinguish_order_scope_and_open_arguments()
    {
        var integer = new TypeSignature.Primitive(PrimitiveTypeCode.Int32);
        var obj = new TypeSignature.Primitive(PrimitiveTypeCode.Object);
        Assert.NotEqual(ClosedGenericKey.Create(new([integer]), new([obj])), ClosedGenericKey.Create(new([obj]), new([integer])));
        Assert.NotEqual(ClosedGenericKey.Create(new([integer]), new([])), ClosedGenericKey.Create(new([]), new([integer])));
        Assert.Throws<ArgumentException>(() => ClosedGenericKey.Create(new([]), new([new TypeSignature.Variable(true, 0)])));
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Native_symbol_keys_are_validated(string key)
    {
        var source = new MethodSource("test", new string('a', 64), 0x06000001, "test") { Specialization = key };
        var method = new IrMethod(source, [], IrType.Int32, [new IrBlock(0, 0, [], [new IrStatement.Define(new IrValue(0, IrType.Int32, 0, new IrExpression.Constant(0)))], new IrReturn(0, 0))], []);
        Assert.Throws<ArgumentException>(() => CppEmitter.Emit(method));
    }
}
