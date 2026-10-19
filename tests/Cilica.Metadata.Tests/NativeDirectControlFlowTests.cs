using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeDirectControlFlowTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task Both_release_drivers_preserve_values_borrows_roots_and_caught_faults(int direct)
    {
        (string Name, Func<int, int, int> Oracle)[] probes = [
            (nameof(WideObjects.Heap), WideObjects.Heap), (nameof(WideObjects.Values), WideObjects.Values),
            (nameof(WideObjects.Exceptions), WideObjects.Exceptions), (nameof(WideObjects.Unsigned), WideObjects.Unsigned)];
        await IntegerObjectTranslationTests.VerifyTranslated(true, probes.Select(p => {
            var translated = IntegerObjectTranslationTests.Translate(p.Name);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            Assert.Contains("goto cilica_step_0u;", ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program!)).Text, StringComparison.Ordinal);
            return (p.Name, translated.Program!, p.Oracle);
        }).ToArray(), nativeFlags: ["-DCILICA_DIRECT_CONTROL_FLOW=" + direct]);
    }
    [Fact]
    public async Task Leave_without_a_local_handler_retains_exception_driver()
    {
        var original = ManagedExceptionIrTests.Model();
        var flow = original.Flow.Flow.Flow;
        var method = flow.Methods[0] with {
            Locals = [], ExceptionRegions = [],
            Blocks = [new(0, 0, [], [], new IrLeave(0, new(1, []))),
                new(1, 1, [], [new IrStatement.Define(new(0, IrType.Int32, 1, new IrExpression.Constant(7)))], new IrReturn(2, 0))]
        };
        var program = original with { Flow = original.Flow with { Flow = new(flow with { Methods = [method] }) } };
        await IntegerObjectTranslationTests.VerifyTranslated(true, [("LeaveOnly", program, (_, _) => 7)],
            pairs: [(0, 0)], nativeFlags: ["-DCILICA_DIRECT_CONTROL_FLOW=1"]);
    }
}
