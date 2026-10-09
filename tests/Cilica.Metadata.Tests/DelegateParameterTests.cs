using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class DelegateParameterTests
{
    private static readonly string[] Names = ["Interface", "Class", "Boolean", "Action", "Instance", "Null", "ArrayStore", "Inline"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(DelegateParameterProbe), Names));
    [Theory] [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Compatible_reference_parameters_preserve_calls_and_faults(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(DelegateParameterProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Fact]
    public async Task A_delegate_cannot_bind_a_method_that_requires_a_narrower_reference()
    {
        var original = (await Programs.Value)[0]; var flow = original.Flow.Flow.Flow;
        IrMethod Change(IrMethod method, IrMethodId target)
        {
            var block = method.Blocks.Single(b => b.Values.Any(v => v.Expression is IrExpression.NewDelegate));
            var statement = block.Statements.OfType<IrStatement.Define>().Single(d => d.Value.Expression is IrExpression.NewDelegate);
            var capture = (IrExpression.NewDelegate)statement.Value.Expression;
            var changed = statement with { Value = statement.Value with { Expression = capture with { Target = target } } };
            return method with { Blocks = method.Blocks.Replace(block, block with { Statements = block.Statements.Replace(statement, changed) }) };
        }
        IrMethod Caller(System.Collections.Immutable.ImmutableArray<IrMethod> methods) => methods.Single(m => m.Blocks.Any(b => b.Values.Any(v => v.Expression is IrExpression.NewDelegate)));
        IrMethodId Target(System.Collections.Immutable.ImmutableArray<IrMethod> methods) => IrMethodId.From(methods.Single(m => m.Source.Name.EndsWith("::Narrow", StringComparison.Ordinal)).Source);
        var method = Caller(flow.Methods);
        var invalid = original with { Flow = original.Flow with { Flow = original.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, Change(method, Target(flow.Methods))) } } } };
        Assert.Contains("expression, parameter or operand", Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid)).Message, StringComparison.Ordinal);
        var scalar = ValueScalarLowering.LowerExceptions(original); var objects = scalar.Flow.Flow.Flow.Flow; method = Caller(objects.Flow.Methods);
        var changedObjects = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(method, Change(method, Target(objects.Flow.Methods))) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = changedObjects } } } };
        Assert.Contains("expression, parameter or operand", Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar)).Message, StringComparison.Ordinal);
    }
}
