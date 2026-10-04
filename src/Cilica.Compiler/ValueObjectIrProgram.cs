using System;
using System.Collections.Immutable;

namespace Cilica.Compiler;

/// <summary>Inline copies, vectors and static borrows composed with checked class construction and fields.</summary>
public sealed record ValueObjectIrProgram(ManagedLocationIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "value-objects-ir-v1";
}

public static class ValueObjectIrValidator
{
    public static void Validate(ValueObjectIrProgram program) => _ = ValidateAndPlan(program);
    internal static ValueIrRules ValidateAndPlan(ValueObjectIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return ValueArrayIrValidator.ValidateComposition(program.Flow, program.Bindings, classes: true);
    }
}
