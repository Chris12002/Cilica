using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ReferenceArrayTranslationTests
{
    [Theory]
    [InlineData("Generic")] [InlineData("FaultOrder")] [InlineData("Casts")] [InlineData("NullOnly")]
    public void Closed_reference_vectors_compose_with_values_and_managed_faults(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ReferenceArrays));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Flow.Flow.Flow.Types.References.Arrays, a => a.ArrayElement!.Kind == ObjectArrayElementKind.Reference);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ReferenceArrays)).Success);
    }

    [Theory]
    [InlineData("Nested")]
    public void Unimplemented_element_storage_still_produces_diagnostics(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ReferenceArrays));
        Assert.False(result.Success); Assert.Null(result.Program); Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Native_generic_vectors_and_covariant_fault_order_match_dotnet(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized,
            [(nameof(ReferenceArrays.Generic), ReferenceArrays.Generic), (nameof(ReferenceArrays.FaultOrder), ReferenceArrays.FaultOrder),
                (nameof(ReferenceArrays.Casts), ReferenceArrays.Casts), (nameof(ReferenceArrays.NullOnly), ReferenceArrays.NullOnly),
                (nameof(ReferenceArrays.WideElements), ReferenceArrays.WideElements)], typeof(ReferenceArrays));
}
