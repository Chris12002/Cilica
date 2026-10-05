using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class InterfaceVarianceTranslationTests
{
    [Theory]
    [InlineData("Test")] [InlineData("Assignments")] [InlineData("Nested")] [InlineData("Strings")]
    public void Closed_variance_retains_exact_arguments_and_native_type_checks(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(VariantContracts));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Flow.Flow.Types.References.Interfaces, i => !i.Arguments.IsEmpty);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Native_nested_variance_and_array_covariance_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [(nameof(VariantContracts.Test), VariantContracts.Test), (nameof(VariantContracts.Assignments), VariantContracts.Assignments),
                (nameof(VariantContracts.Nested), VariantContracts.Nested), (nameof(VariantContracts.Strings), VariantContracts.Strings)], typeof(VariantContracts));

    [Fact]
    public void Unmodeled_String_interface_implementations_remain_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate(nameof(VariantContracts.StringInterface), fixture: typeof(VariantContracts));
        Assert.False(result.Success); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("selected String", StringComparison.Ordinal)
            || d.Message.Contains("additional flags", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("capability")] [InlineData("direction")] [InlineData("reference")] [InlineData("missing")] [InlineData("budget")]
    public void Detached_closed_argument_facts_are_validated(string mutation)
    {
        var result = IntegerObjectTranslationTests.Translate(nameof(VariantContracts.Assignments), fixture: typeof(VariantContracts));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var catalog = result.Program!.Flow.Flow.Flow.Types.References;
        catalog = JsonSerializer.Deserialize<ObjectTypeCatalog>(JsonSerializer.Serialize(catalog))!;
        ObjectModelValidator.Validate(catalog);
        var contract = catalog.Interfaces.First(i => i.Arguments.Any(a => a.Reference is not null && a.Variance == GenericParameterAttributes.Covariant));
        var argument = contract.Arguments[0];
        var changed = mutation switch {
            "direction" => contract with { Arguments = [argument with { Variance = GenericParameterAttributes.VarianceMask }] },
            "reference" => contract with { Arguments = [argument, argument with { Reference = null }] },
            "missing" => contract with { Arguments = [argument with { Reference = new(new string('f', 64), 0x02000002) }] },
            "budget" => contract with { Arguments = [.. Enumerable.Repeat(argument, ObjectModelLimits.MaximumInterfaceArguments + 1)] },
            _ => contract };
        catalog = catalog with { Integer64 = mutation != "capability", Interfaces = catalog.Interfaces.Replace(contract, changed) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
    }
}
