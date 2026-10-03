using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata;

namespace Cilica.Compiler;

public enum ObjectMethodKind { Static, Instance, Constructor, ObjectBaseConstructor }

/// <summary>Detached declaration facts; instance signatures include the explicit receiver at parameter zero.</summary>
public sealed record ObjectMethodBinding(IrMethodId Method, ObjectTypeId Owner, ObjectMethodKind Kind,
    MethodAttributes Attributes, bool OwnerExternallyVisible)
{
    public ObjectVirtualSlot? VirtualSlot { get; init; }
    public string? VirtualName { get; init; }
}

public sealed record ObjectVirtualSlot(IrMethodId Method, ObjectTypeId Owner);
public sealed record ObjectVirtualTarget(ObjectTypeId Receiver, IrMethodId Implementation);
public sealed record ObjectVirtualDispatch(IrMethodId Declaration, ImmutableArray<ObjectVirtualTarget> Targets);
public static class ObjectDispatchLimits
{
    public const int MaximumTargets = 65536;
}

/// <summary>Detached object operations and declaration bindings, validated before native lowering.</summary>
public sealed record ObjectIrProgram(ReferenceIrProgram Flow, ImmutableArray<ObjectMethodBinding> Bindings)
{
    public const string Profile = "object-operations-ir-v8";
    public ImmutableArray<ObjectVirtualDispatch> Dispatch { get; init; } = [];
    public ObjectInterfaceDispatchPlan? InterfaceDispatch { get; init; }
}

public sealed record ObjectTranslationResult(ObjectIrProgram? Program, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null;
}
