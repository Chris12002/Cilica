using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class AbstractValueArgumentTests
{
    private static readonly string[] Names = ["Scalars", "Copies", "InternalGeneric", "NullOrder", "Empty", "NullOnly"];
    private static readonly Lazy<ManagedExceptionIrProgram[]> Programs = new(() => Names.Select(name => {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(AbstractValueArgumentProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }).ToArray());
    [Theory] [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public Task Copied_arguments_and_faults_match_dotnet(bool optimized, int group) => IntegerObjectTranslationTests.VerifyTranslated(optimized,
        Names.Select((name,i) => (name, Programs.Value[i], typeof(AbstractValueArgumentProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*3).Take(3).ToArray());
    [Fact]
    public void Abstract_signatures_expand_nested_leaves_and_remove_empty_values()
    {
        foreach (var program in Programs.Value)
        {
            var logical = program.Flow;
            Assert.Contains(logical.AbstractMethods, d => d.Parameters.Any(p => p.Kind == IrTypeKind.Value));
            var scalar = ValueScalarLowering.LowerExceptions(program);
            ScalarManagedExceptionIrValidator.Validate(scalar);
            var objects = scalar.Flow.Flow.Flow.Flow;
            foreach (var declaration in objects.AbstractMethods)
            {
                Assert.DoesNotContain(declaration.Parameters, p => p.Kind == IrTypeKind.Value);
                Assert.DoesNotContain(objects.Flow.Methods, m => IrMethodId.From(m.Source) == IrMethodId.From(declaration.Source));
            }
        }
        var wide = ValueScalarLowering.LowerExceptions(Programs.Value[0]).Flow.Flow.Flow.Flow.AbstractMethods.Single();
        Assert.Equal(new[] { IrTypeKind.Reference, IrTypeKind.Int32, IrTypeKind.Int64, IrTypeKind.Reference }, wide.Parameters.Select(p => p.Kind));
        var empty = ValueScalarLowering.LowerExceptions(Programs.Value[4]).Flow.Flow.Flow.Flow.AbstractMethods.Single();
        Assert.Equal(new[] { IrTypeKind.Reference, IrTypeKind.Int32 }, empty.Parameters.Select(p => p.Kind));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Scalar_validator_rejects_missing_or_wrong_expanded_leaves(bool missing)
    {
        var scalar = ValueScalarLowering.LowerExceptions(Programs.Value[0]); var objects = scalar.Flow.Flow.Flow.Flow;
        var declaration = objects.AbstractMethods.Single();
        var changed = objects with { AbstractMethods = [declaration with { Parameters = missing ? declaration.Parameters.RemoveAt(2) : declaration.Parameters.SetItem(2, IrType.Int32) }] };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with {
            Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = changed } } }
        }));
    }
    [Fact]
    public void Borrowed_abstract_arguments_remain_actionable()
    {
        var result = IntegerObjectTranslationTests.Translate("UnsupportedBorrow", fixture: typeof(AbstractValueArgumentProbe));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("value returns and borrows need later lowering", StringComparison.Ordinal));
    }
}
