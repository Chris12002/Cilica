using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

/// <summary>A bodyless interface declaration; parameter zero is its exact interface receiver.</summary>
public sealed record ObjectInterfaceDeclaration(MethodSource Source, ObjectTypeId Owner, string Name,
    MethodAttributes Attributes, bool OwnerExternallyVisible, ImmutableArray<IrType> Parameters, IrType ReturnType);

/// <summary>Class method facts needed for mapping/slot validation, without IL or executable IR.</summary>
public sealed record ObjectDispatchMethod(ObjectMethodBinding Binding, string Name,
    ImmutableArray<IrType> Parameters, IrType ReturnType)
{
    public bool ArrayHelper { get; init; }
}

public sealed record ObjectInterfaceTarget(ObjectTypeId Receiver, IrMethodId Mapping, IrMethodId Implementation, bool IsExplicit);
public sealed record ObjectInterfaceDispatch(IrMethodId Declaration, ImmutableArray<ObjectInterfaceTarget> Targets);

/// <summary>Detached metadata-backed dispatch preparation. It does not grant executable access or validate IL.</summary>
public sealed record ObjectInterfaceDispatchPlan(ObjectTypeCatalog Types,
    ImmutableArray<ObjectInterfaceDeclaration> Declarations, ImmutableArray<ObjectDispatchMethod> Methods,
    ImmutableArray<ObjectInterfaceDispatch> Dispatch)
{
    public const string Profile = "interface-dispatch-plan-v2";
    public ValueTypeCatalog? Values { get; init; }
}

public sealed record ObjectInterfacePlanResult(ObjectInterfaceDispatchPlan? Plan, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Plan is not null;
}
