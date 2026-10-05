using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ArgumentExceptionTranslationTests
{
    [Theory]
    [InlineData("Argument")] [InlineData("NullArgument")] [InlineData("RangeArgument")] [InlineData("Plain")] [InlineData("Caught")]
    public void Selected_exception_forms_keep_exact_resources_and_parameter_identity(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ArgumentExceptionProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Program!.Exceptions.ArgumentBase);
        Assert.Equal(4, result.Program.Exceptions.Resources.Length);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ArgumentExceptionProbe)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Exception_messages_parameters_and_catches_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(ArgumentExceptionProbe.Argument), ArgumentExceptionProbe.Argument), (nameof(ArgumentExceptionProbe.NullArgument), ArgumentExceptionProbe.NullArgument),
            (nameof(ArgumentExceptionProbe.RangeArgument), ArgumentExceptionProbe.RangeArgument), (nameof(ArgumentExceptionProbe.Plain), ArgumentExceptionProbe.Plain),
            (nameof(ArgumentExceptionProbe.Caught), ArgumentExceptionProbe.Caught)], typeof(ArgumentExceptionProbe));

    [Fact]
    public void Resource_identity_mutations_are_rejected()
    {
        var program = IntegerObjectTranslationTests.Translate("NullArgument", fixture: typeof(ArgumentExceptionProbe)).Program!;
        Assert.NotNull(program);
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with {
            Exceptions = program.Exceptions with { ArgumentBase = program.Exceptions.Base } }));
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with {
            Exceptions = program.Exceptions with { Resources = program.Exceptions.Resources.Add(program.Exceptions.Resources[0]) } }));
        var resource = program.Exceptions.Resources.First(r => r.Kind == ExceptionResourceKind.ArgumentNull);
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with {
            Exceptions = program.Exceptions with { Resources = program.Exceptions.Resources.Replace(resource, resource with { Type = program.Exceptions.Base }) } }));
    }

    [Fact]
    public void Actual_value_overload_remains_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("Unsupported", fixture: typeof(ArgumentExceptionProbe));
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
