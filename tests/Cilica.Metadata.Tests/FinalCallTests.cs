using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class FinalCallTests
{
    private static readonly string[] Names = ["NullReturn", "NullVoid", "ArgumentFault", "Inherited", "Interface"];
    private static readonly Lazy<ManagedExceptionIrProgram[]> Programs = new(() => Names.Select(name => {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(FinalCallProbe));
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }).ToArray());
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Final_calls_preserve_null_order_and_inherited_target(bool optimized, int first)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, Programs.Value[i], typeof(FinalCallProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(first).Take(first == 0 ? 3 : 2).ToArray());
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void Final_class_calls_are_direct_and_require_final_evidence(int index)
    {
        var program = Programs.Value[index]; var logical = program.Flow;
        var scalar = ValueScalarLowering.LowerExceptions(program); var objects = scalar.Flow.Flow.Flow.Flow;
        Assert.Empty(logical.Dispatch); Assert.Empty(objects.Dispatch);
        var finalBindings = objects.Bindings.Where(b => (b.Attributes & MethodAttributes.Final) != 0).ToArray();
        Assert.NotEmpty(finalBindings);
        Assert.Contains(objects.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Statements), s => s is
            IrStatement.Invoke { NullCheckReceiver: true, VirtualDispatch: false } or
            IrStatement.Define { Value.Expression: IrExpression.Call { NullCheckReceiver: true, VirtualDispatch: false } });
        foreach (var binding in finalBindings)
        {
            var bindings = objects.Bindings.Replace(binding, binding with { Attributes = binding.Attributes & ~MethodAttributes.Final });
            Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = logical with { Bindings = bindings } }));
            Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalar with {
                Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with { Flow = objects with { Bindings = bindings } } } }
            }));
        }
    }
}
