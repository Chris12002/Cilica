using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class BoxedInterfaceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Boxed_interface_ancestry_variance_storage_and_faults_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [("Relations", BoxedInterfaces.Relations), ("Storage", BoxedInterfaces.Storage), ("Faults", BoxedInterfaces.Faults), ("ConfinedWithDispatch", BoxedInterfaces.ConfinedWithDispatch)], typeof(BoxedInterfaces));
    [Theory]
    [InlineData("UnsupportedDispatch")] [InlineData("MixedDispatch")] [InlineData("EscapedDispatch")]
    public void Boxed_interface_dispatch_stays_diagnostic(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(BoxedInterfaces));
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("interface", System.StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void Detached_scalar_validation_rechecks_confined_parameter_uses()
    {
        var translated = IntegerObjectTranslationTests.Translate("ConfinedWithDispatch", fixture: typeof(BoxedInterfaces));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var scalar = ValueScalarLowering.LowerExceptions(translated.Program!);
        var program = scalar.Flow.Flow.Flow.Flow;
        var method = program.Flow.Methods.Single(m => m.Source.Name.EndsWith("::ReadConfined", StringComparison.Ordinal));
        var block = method.Blocks.Single();
        int parameter = block.Values.Single(v => v.Expression is IrExpression.Parameter { Index: 0 }).Id;
        var changed = method with { Locals = method.Locals.Add(IrType.Reference(program.Flow.Types.ObjectBase)),
            Blocks = [block with { Statements = block.Statements.Add(new IrStatement.StoreLocal(block.Terminator.ILOffset, method.Locals.Length, parameter)) }] };
        var flow = program with { Flow = program.Flow with { Methods = program.Flow.Methods.Replace(method, changed) } };
        var boxed = scalar.Flow.Flow with { Flow = new(flow) };
        var result = scalar with { Flow = new(boxed) };
        Assert.Contains("Boxed interface", Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(result)).Message, StringComparison.Ordinal);
    }
}

