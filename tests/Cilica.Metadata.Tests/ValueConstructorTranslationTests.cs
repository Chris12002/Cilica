using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ValueConstructorTranslationTests
{
    [Theory]
    [InlineData("Boxed")] [InlineData("Generic")] [InlineData("Existing")] [InlineData("Borrow")] [InlineData("Empty")]
    public void Struct_constructors_use_writable_this_locations_and_lower_to_private_static_calls(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ValueConstructors));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Bindings, b => b.Kind == ObjectMethodKind.ValueConstructor);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ValueConstructors)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Constructed_structs_match_dotnet_under_collection_and_faults(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [(nameof(ValueConstructors.Boxed), ValueConstructors.Boxed), (nameof(ValueConstructors.Generic), ValueConstructors.Generic),
                (nameof(ValueConstructors.Existing), ValueConstructors.Existing), (nameof(ValueConstructors.Borrow), ValueConstructors.Borrow),
                (nameof(ValueConstructors.Empty), ValueConstructors.Empty)], typeof(ValueConstructors));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Readonly_initialization_rejects_foreign_receivers_and_missing_fields(bool missingField)
    {
        var result = IntegerObjectTranslationTests.Translate(nameof(ValueConstructors.Borrow), fixture: typeof(ValueConstructors));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var program = result.Program!; var flow = program.Flow.Flow.Flow;
        var constructor = program.Flow.Bindings.Single(b => b.Kind == ObjectMethodKind.ValueConstructor);
        var changed = flow.Methods.Select(m => IrMethodId.From(m.Source) != constructor.Method ? m : m with {
            Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(s => missingField && s is IrStatement.Define { Value.Expression: IrExpression.LocationField location } field
                ? field with { Value = field.Value with { Expression = location with { Field = null! } } }
                : !missingField && s is IrStatement.Define { Value.Expression: IrExpression.Parameter { Index: 0 } } define
                    ? define with { Value = define.Value with { Expression = new IrExpression.Parameter(1) } } : s)] })] });
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = [.. changed] } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
    }
}
