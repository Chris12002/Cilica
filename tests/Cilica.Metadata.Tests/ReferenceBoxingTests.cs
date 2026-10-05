using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ReferenceBoxingTests
{
    [Theory]
    [InlineData("Identity")] [InlineData("Contracts")] [InlineData("Arrays")] [InlineData("Faults")] [InlineData("ContractFaults")]
    public void Closed_reference_boxing_does_not_create_a_value_box(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ReferenceBoxing));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!;
        Assert.DoesNotContain(program.Flow.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values), v => v.Expression is IrExpression.BoxValue);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ReferenceBoxing)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Reference_identity_nulls_and_cast_faults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [("Identity", ReferenceBoxing.Identity), ("Contracts", ReferenceBoxing.Contracts), ("Arrays", ReferenceBoxing.Arrays), ("Faults", ReferenceBoxing.Faults),
            ("ContractFaults", ReferenceBoxing.ContractFaults)], typeof(ReferenceBoxing));

    [Fact]
    public void Unbox_cannot_produce_a_reference_type_borrow()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var builder = new ResolutionAssemblyBuilder(new("ReferenceUnbox", new Version(1, 0, 0, 0)));
        var operand = builder.TypeReference("System", "Object", builder.Reference(runtime.Identity));
        builder.Type("", "Probe");
        var root = builder.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Unbox); il.Token(operand); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, image, root, runtime);
        Assert.False(result.Success);
        Assert.Contains("unbox requires a boxed value type", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }
}
