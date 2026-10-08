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
    [InlineData("NarrowAllocationOnly", ObjectArrayElementKind.Boolean)] [InlineData("WideAllocationOnly", ObjectArrayElementKind.Int64)]
    public void Named_primitive_allocations_retain_scalar_layouts(string name, ObjectArrayElementKind kind)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ValueVectorProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var arrays = result.Program!.Flow.Flow.Flow.Types.References.Arrays;
        Assert.Contains(arrays, a => a.ArrayElement!.Kind == kind);
        Assert.DoesNotContain(arrays, a => a.ArrayElement!.Kind == ObjectArrayElementKind.Value);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CIL_value_arrays_copies_borrows_closed_fields_and_faults_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized, [("Copy",ValueVectorProbe.Copy), ("Generic",ValueVectorProbe.Generic),
            ("Faults",ValueVectorProbe.Faults), ("Empty",ValueVectorProbe.Empty),
            ("NarrowAllocationOnly",ValueVectorProbe.NarrowAllocationOnly), ("WideAllocationOnly",ValueVectorProbe.WideAllocationOnly)], typeof(ValueVectorProbe));
}
