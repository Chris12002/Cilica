using System;

namespace Cilica.Compiler;

/// <summary>Scalar, synchronous borrows composed with existing object operations. Inline values require expansion first.</summary>
public sealed record ScalarLocationIrProgram(ObjectIrProgram Flow)
{
    public const string Profile = "scalar-location-ir-v3";
}

public static class ScalarLocationIrValidator
{
    public static void Validate(ScalarLocationIrProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ObjectIrValidator.Validate(program.Flow, locations: true);
    }
}
