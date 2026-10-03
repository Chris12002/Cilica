using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectStringTranslationTests
{
    internal static ObjectTranslationResult Authored(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(StringProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        return ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(StringProbe).GetMethod(name)!.MetadataToken), runtime);
    }

    [Theory]
    [InlineData("Length")]
    [InlineData("CodeUnit")]
    [InlineData("Equal")]
    [InlineData("NotEqual")]
    [InlineData("StaticEquals")]
    [InlineData("Concat")]
    [InlineData("NullLength")]
    [InlineData("NullCodeUnit")]
    [InlineData("NullConcat")]
    [InlineData("Fields")]
    [InlineData("Cast")]
    [InlineData("Test")]
    [InlineData("Joined")]
    [InlineData("Dispatch")]
    [InlineData("Virtual")]
    public void Authored_string_programs_translate_without_importing_framework_bodies(string name)
    {
        var result = Authored(name); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        Assert.NotNull(program.Flow.Types.String);
        Assert.DoesNotContain(program.Flow.Types.Types, t => ObjectTypeId.From(t.Source) == program.Flow.Types.String);
        Assert.DoesNotContain(program.Bindings, b => b.Owner == program.Flow.Types.String);
        ObjectIrValidator.Validate(program); ObjectRootAssignment.Assign(program);
        Assert.Throws<NotSupportedException>(() => ObjectCppEmitter.EmitProgram(program));
    }

    [Theory]
    [InlineData("Unsupported")]
    [InlineData("UnsupportedConcat")]
    public void Unsupported_members_report_the_call_site(string name)
    {
        var result = Authored(name); Assert.Null(result.Program);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("Unsupported String member", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("IL_", diagnostic.Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Length", 0)]
    [InlineData("Concat", 0)]
    [InlineData("NullLength", 0)]
    [InlineData("NullConcat", 0)]
    [InlineData("Fields", 0)]
    [InlineData("CodeUnit", -1)]
    [InlineData("CodeUnit", 0)]
    [InlineData("CodeUnit", 1)]
    [InlineData("CodeUnit", 2)]
    [InlineData("CodeUnit", 4)]
    [InlineData("CodeUnit", 5)]
    [InlineData("NullCodeUnit", -1)]
    [InlineData("NullCodeUnit", 0)]
    public void Literal_and_intrinsic_operand_translation_matches_authored_dotnet(string name, int index)
    {
        var method = typeof(StringProbe).GetMethod(name)!;
        int[] arguments = method.GetParameters().Length == 0 ? [] : [index];
        var evaluator = new ObjectTranslationConformanceTests.Evaluator(Authored(name).Program!);
        int expected = 0;
        var error = Record.Exception(() => expected = (int)method.Invoke(null, arguments.Cast<object>().ToArray())!);
        if (error is TargetInvocationException fault)
            Assert.Equal(fault.InnerException!.GetType(), Record.Exception(() => evaluator.Run(arguments))?.GetType());
        else { Assert.Null(error); Assert.Equal(expected, evaluator.Run(arguments)); }
    }

    [Fact]
    public void Literal_payload_preserves_raw_UTF16_and_limits_are_checked_before_copying()
    {
        var b = new ResolutionAssemblyBuilder(new("LiteralInput", new Version(1, 0, 0, 0)));
        var literal = b.String("A\0\ud800X\udfff");
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("literal.dll", b.Build())).Value!;
        var result = resolver.ReadUserString(image, MetadataTokens.GetToken(literal), 5);
        Assert.True(result.Success); Assert.Equal(new ushort[] { 65, 0, 0xd800, 88, 0xdfff }, result.Value!.CodeUnits);
        Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(resolver.ReadUserString(image, MetadataTokens.GetToken(literal), 4).Diagnostics).Id);
        foreach (int token in new[] { 0, 0x70000000, 0x700fffff, 0x01000001 })
            Assert.False(resolver.ReadUserString(image, token, 10).Success);
        var translated = Authored("Length").Program!;
        var payload = translated.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values)
            .Select(v => v.Expression).OfType<IrExpression.StringLiteral>().Single();
        Assert.Equal(result.Value.CodeUnits, payload.CodeUnits);
    }
}
