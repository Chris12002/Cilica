using System.Threading.Tasks;
using System.Linq;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ComposedStringTranslationTests
{
    [Theory]
    [InlineData("IntrinsicPrecedence")] [InlineData("CodeUnits")] [InlineData("ArgumentFault")] [InlineData("Concat")] [InlineData("Rooted")]
    public void String_operations_compose_without_framework_bodies(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ComposedStringProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.DoesNotContain(result.Program!.Flow.Bindings, b => b.Owner == result.Program.Flow.Flow.Flow.Types.References.String);
        ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program));
        Assert.False(IntegerObjectTranslationTests.Translate(name, enabled: false, fixture: typeof(ComposedStringProbe)).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Utf16_nulls_allocations_and_caught_faults_match_dotnet(bool optimized) => IntegerObjectTranslationTests.Verify(optimized,
        [(nameof(ComposedStringProbe.IntrinsicPrecedence), ComposedStringProbe.IntrinsicPrecedence), (nameof(ComposedStringProbe.CodeUnits), ComposedStringProbe.CodeUnits), (nameof(ComposedStringProbe.ArgumentFault), ComposedStringProbe.ArgumentFault),
            (nameof(ComposedStringProbe.Concat), ComposedStringProbe.Concat), (nameof(ComposedStringProbe.Rooted), ComposedStringProbe.Rooted)], typeof(ComposedStringProbe));

    [Fact]
    public void Concat_allocation_sites_survive_both_root_plans()
    {
        var program = IntegerObjectTranslationTests.Translate("Rooted", fixture: typeof(ComposedStringProbe)).Program!;
        var source = program.Flow.Flow.Flow;
        var sourcePlans = ValueRootAssignment.Assign(program).ToDictionary(p => p.Method);
        var scalarPlans = ObjectRootAssignment.AssignExceptions(ValueScalarLowering.LowerExceptions(program)).ToDictionary(p => p.Method);
        int count = 0;
        foreach (var method in source.Methods)
        foreach (var value in method.Blocks.SelectMany(b => b.Values).Where(v => v.Expression is IrExpression.StringConcat))
        {
            var id = IrMethodId.From(method.Source);
            Assert.Contains(sourcePlans[id].Safepoints, p => p.ILOffset == value.ILOffset && p.Kind == ObjectSafepointKind.Allocation);
            Assert.Contains(scalarPlans[id].Safepoints, p => p.ILOffset == value.ILOffset && p.Kind == ObjectSafepointKind.Allocation);
            ++count;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public void Other_overloads_remain_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("Unsupported", fixture: typeof(ComposedStringProbe));
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }
}
