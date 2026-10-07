using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class WideDelegateTests
{
    private static readonly string[] Names = ["Static", "Capture", "Action", "ReferenceArgument", "ReferenceResult", "Generic", "Faults", "NullTarget"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(WideDelegateProbe), Names));
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    [InlineData(false,2)] [InlineData(true,2)] [InlineData(false,3)] [InlineData(true,3)]
    public async Task Wide_arguments_results_captures_and_faults_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name,programs[i],typeof(WideDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Theory]
    [InlineData("result")] [InlineData("argument")] [InlineData("fault")]
    public async Task Both_stages_reject_mismatched_wide_invocations(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        ImmutableArray<IrMethod> Alter(ImmutableArray<IrMethod> methods) => [..methods.Select(m => m with {
            Blocks = [..m.Blocks.Select(b => b with { Statements = [..b.Statements.Select(s => {
                if (s is not IrStatement.Define { Value.Expression: IrExpression.CallDelegate call } d) return s;
                return mutation switch {
                    "result" => d with { Value = d.Value with { Type = IrType.Int32 } },
                    "argument" => d with { Value = d.Value with { Expression = call with { Arguments = call.Arguments.SetItem(0,call.Delegate) } } },
                    _ => d with { Exceptional = null } };
            })] })] })];
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with {
            Flow = program.Flow.Flow with { Flow = flow with { Methods = Alter(flow.Methods) } } } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with { Flow = scalar.Flow with {
            Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Alter(objects.Flow.Methods) } } } } } }));
    }
}
