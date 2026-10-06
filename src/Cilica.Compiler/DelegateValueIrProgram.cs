using System;
using System.Collections.Immutable;

namespace Cilica.Compiler;

/// <summary>Single-target delegates composed with values/boxes. Captures are managed references, never native pointers.</summary>
public sealed record DelegateValueIrProgram(ManagedLocationIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "delegate-values-ir-v2";
    public ObjectInterfaceDispatchPlan? InterfaceDispatch { get; init; }
    public ImmutableArray<ObjectVirtualDispatch> Dispatch { get; init; } = [];
}

public static class DelegateValueIrValidator
{
    public static void Validate(DelegateValueIrProgram program) => _ = ValidateAndPlan(program);

    internal static ValueIrRules ValidateAndPlan(DelegateValueIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return ValueArrayIrValidator.ValidateComposition(program.Flow, program.Bindings, classes: true, boxing: true, delegates: true,
            interfaces: program.InterfaceDispatch, dispatch: program.Dispatch);
    }
}
