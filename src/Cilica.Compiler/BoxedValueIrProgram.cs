using System;
using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Compiler;

/// <summary>Detached named-struct boxing, exact unbox copies and nonescaping payload borrows.</summary>
public sealed record BoxedValueIrProgram(ManagedLocationIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "boxed-value-ir-v8";
}

public static class BoxedValueIrValidator
{
    public static void Validate(BoxedValueIrProgram program) => _ = ValidateAndPlan(program);

    /// <summary>
    /// Derives only the used boxed payload layouts, sharing value paths and reference maps.
    /// These are logical payload slots, not native offsets or executable descriptors.
    /// Empty payloads still retain their exact type and require a fresh allocation on box.
    /// </summary>
    public static ImmutableArray<ValueTypePlan> PlanPayloads(BoxedValueIrProgram program)
    {
        var rules = ValidateAndPlan(program);
        var used = program.Flow.Flow.Methods.SelectMany(m => m.Blocks).SelectMany(b => b.Values)
            .Select(v => v.Expression switch
            {
                IrExpression.BoxValue b => b.Type,
                IrExpression.UnboxValue u => u.Type,
                IrExpression.UnboxLocation u => u.Type,
                IrExpression.IsInstance { BoxedTarget: true } t => t.Target,
                IrExpression.CastClass { BoxedTarget: true } t => t.Target,
                _ => null,
            }).Where(t => t is not null).ToHashSet();
        return [.. rules.Plans.Where(p => used.Contains(p.Type))];
    }

    internal static ValueIrRules ValidateAndPlan(BoxedValueIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return ValueArrayIrValidator.ValidateComposition(program.Flow, program.Bindings, classes: true, boxing: true);
    }
}
