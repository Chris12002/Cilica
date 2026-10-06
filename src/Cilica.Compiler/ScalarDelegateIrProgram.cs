using System;

namespace Cilica.Compiler;

/// <summary>Checked delegate operations on the scalar boxed/location backend.</summary>
public sealed record ScalarDelegateIrProgram(ScalarBoxedIrProgram Flow)
{
    public const string Profile = "scalar-delegates-ir-v2";
}

public static class ScalarDelegateIrValidator
{
    public static void Validate(ScalarDelegateIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        _ = ScalarBoxedIrValidator.ValidateAndPlan(program.Flow, delegates: true);
    }
}
