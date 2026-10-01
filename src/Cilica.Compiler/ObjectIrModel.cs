using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata;

namespace Cilica.Compiler;

public enum ObjectMethodKind { Static, Instance, Constructor, ObjectBaseConstructor }

/// <summary>Detached declaration facts; instance signatures include the explicit receiver at parameter zero.</summary>
public sealed record ObjectMethodBinding(IrMethodId Method, ObjectTypeId Owner, ObjectMethodKind Kind,
    MethodAttributes Attributes, bool OwnerExternallyVisible);

/// <summary>Object operation translation only. Allocation, ownership and rooting are not lowered to native code.</summary>
public sealed record ObjectIrProgram(ReferenceIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "object-operations-ir-v1";
}

public sealed record ObjectTranslationResult(ObjectIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
