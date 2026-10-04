using System;
using System.Linq;

namespace Cilica.Compiler;

public sealed record ScalarManagedExceptionIrProgram(ScalarDelegateIrProgram Flow, ManagedExceptionTypes Exceptions)
{
    public const string Profile = "scalar-managed-exceptions-ir-v1";
}
public static class ScalarManagedExceptionIrValidator
{
    public static void Validate(ScalarManagedExceptionIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(program.Flow);
        ArgumentNullException.ThrowIfNull(program.Flow.Flow);
        var boxes = program.Flow.Flow;
        _ = ValueLayoutPlanner.Build(boxes.Values);
        var rules = new ManagedExceptionRules(program.Exceptions, boxes.Values.References);
        _ = ScalarBoxedIrValidator.ValidateAndPlan(boxes, delegates: true, exceptions: rules);
        _ = ManagedExceptionCastMessages.Build(boxes.Flow.Flow.Flow.Methods, boxes.Values.References, boxes.Values.Types);
        if (boxes.Flow.Flow.Bindings.Any(b => program.Exceptions.Types.Contains(b.Owner) && b.Kind != ObjectMethodKind.Static))
            throw new ArgumentException("Intrinsic exception shells cannot have ordinary instance bodies.", nameof(program));
    }
}
