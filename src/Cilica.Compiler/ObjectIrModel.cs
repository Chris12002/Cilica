using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata;

namespace Cilica.Compiler;

public enum ObjectMethodKind { Static, Instance, Constructor, ObjectBaseConstructor }

/// <summary>Detached declaration facts; instance signatures include the explicit receiver at parameter zero.</summary>
public sealed record ObjectMethodBinding(IrMethodId Method, ObjectTypeId Owner, ObjectMethodKind Kind,
    MethodAttributes Attributes, bool OwnerExternallyVisible);

/// <summary>Detached object operations and declaration bindings, validated before native lowering.</summary>
public sealed record ObjectIrProgram(ReferenceIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "object-operations-ir-v2";
}

public sealed record ObjectTranslationResult(ObjectIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
