using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ValueVectorTranslationTests
{
    [Theory]
    [InlineData("Copy")] [InlineData("Generic")] [InlineData("Faults")] [InlineData("Empty")]
    public void Compiled_value_array_operations_retain_exact_layouts_and_checked_borrows(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ValueVectorProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Flow.Flow.Types.References.Arrays, a => a.ArrayElement!.Kind == ObjectArrayElementKind.Value);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ValueVectorProbe)).Success);
    }
    [Theory]
    [InlineData("NarrowAllocationOnly")] [InlineData("WideAllocationOnly")]
    public void Named_primitive_operands_do_not_masquerade_as_inline_values(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ValueVectorProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("primitive-width-specific", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CIL_value_arrays_copies_borrows_closed_fields_and_faults_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized, [("Copy",ValueVectorProbe.Copy), ("Generic",ValueVectorProbe.Generic),
            ("Faults",ValueVectorProbe.Faults), ("Empty",ValueVectorProbe.Empty)], typeof(ValueVectorProbe));
}
