using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DirectInterfaceValueTests
{
    [Theory]
    [InlineData("Equality")] [InlineData("Generic")]
    public void Direct_calls_retain_value_interface_declarations(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(DirectInterfaceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Flow.Flow.Types.Types, t => !t.Interfaces.IsEmpty);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(DirectInterfaceValues)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Direct_interface_value_bodies_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [("Equality", DirectInterfaceValues.Equality), ("Generic", DirectInterfaceValues.Generic)], typeof(DirectInterfaceValues));

    [Fact]
    public void Constrained_and_runtime_generic_extensions_remain_diagnostic()
    {
        foreach (var name in new[] { "Constrained", "RuntimeContract" })
        {
            var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(DirectInterfaceValues));
            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
        }
    }

    [Fact]
    public void Boxing_cannot_silently_drop_value_interface_relationships()
    {
        var result = IntegerObjectTranslationTests.Translate("Boxed", fixture: typeof(DirectInterfaceValues));
        Assert.False(result.Success);
        Assert.Contains("Boxing interface-bearing values", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Detached_box_operations_cannot_discard_interface_evidence()
    {
        var source = BoxedValueIrTests.Model();
        var scalar = ValueScalarLowering.LowerBoxes(source);
        var catalog = source.Flow.Flow.Types;
        var contract = new ObjectTypeId(catalog.ValueBase.ContentHash, 0x02000006);
        var references = catalog.References with { Integer64 = true, Roots = [.. catalog.References.Roots, contract],
            Interfaces = [new(new("fixture", contract.ContentHash, contract.Token, "IValue"), TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract, [])] };
        catalog = catalog with { References = references, Types = [catalog.Types[0] with { Interfaces = [contract] }] };
        _ = ValueLayoutPlanner.Build(catalog);
        var entry = source.Flow.Flow.Methods[0];
        var block = entry.Blocks[0];
        var defined = (IrStatement.Define)block.Statements[0];
        var constant = new IrStatement.Define(new IrValue(1, IrType.Int32, 0, new IrExpression.Constant(0)));
        var copiesOnly = source.Flow.Flow with { Types = catalog, Methods = [entry with { Blocks = [block with {
            Statements = [defined, constant], Terminator = new IrReturn(1, 1) }] }] };
        Assert.Contains("retains the value metadata catalog", Assert.Throws<ArgumentException>(() => ValueIrValidator.Validate(copiesOnly)).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => BoxedValueIrValidator.Validate(source with { Flow = source.Flow with { Flow = source.Flow.Flow with { Types = catalog } } }));
        scalar = scalar with { Values = catalog, Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Types = references } } } };
        Assert.Throws<ArgumentException>(() => ScalarBoxedIrValidator.Validate(scalar));
    }

    [Fact]
    public void Detached_value_interfaces_require_included_interface_types()
    {
        var result = IntegerObjectTranslationTests.Translate("Equality", fixture: typeof(DirectInterfaceValues));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var catalog = result.Program!.Flow.Flow.Flow.Types;
        var value = catalog.Types.Single(t => !t.Interfaces.IsEmpty);
        var forged = value with { Interfaces = [catalog.References.ObjectBase] };
        Assert.Throws<ArgumentException>(() => ValueLayoutPlanner.Build(catalog with { Types = catalog.Types.Replace(value, forged) }));
    }
}
