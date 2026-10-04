using System.Collections.Immutable;
using Cilica.Metadata.Resolution;

namespace Cilica.Metadata.Analysis;

public abstract record ReachableEntity
{
    public sealed record Type(ConstructedType Definition) : ReachableEntity;
    /// <summary>Structural array shape plus the explicitly selected System.Array base.</summary>
    public sealed record ArrayReceiver(TypeSignature Shape, ConstructedType BaseType) : ReachableEntity;
    public sealed record Method(ResolvedMethod Definition) : ReachableEntity;
    public sealed record Field(ResolvedField Definition) : ReachableEntity;
}

/// <param name="FirstReason">Index of the first incoming edge, or null for an explicit root.</param>
public sealed record ReachabilityNode(int Id, ReachableEntity Entity, int? FirstReason);

public enum DependencyKind
{
    SelectedTypeMember, DeclaringType, Signature, BaseType, Interface, FieldLayout,
    TypeInitializer, ModuleInitializer, Call, Constructor, FieldAccess, TypeOperand,
    MethodAddress, MetadataToken, Local, CatchType, GenericConstraint,
    DispatchDeclaration, ConservativeDispatch, ConstrainedType, RuntimeType,
}

/// <param name="ReceiverType">Type-node ID supporting a conservative dispatch edge; null for ordinary edges.</param>
/// <param name="ConstraintType">Closed named type-node ID from constrained.; null for unconstrained edges.</param>
public sealed record ReachabilityEdge(int From, int To, DependencyKind Kind, int? ILOffset,
    int? ReceiverType = null, int? ConstraintType = null);
public sealed record ReachabilityIssue(int? Node, int? ILOffset, Diagnostic Diagnostic);
public sealed record RuntimeTypeMapping(System.Reflection.Metadata.PrimitiveTypeCode Primitive, ResolvedType Definition);
public sealed record FeatureObservation(int Node, int? ILOffset, string Feature, string Evidence);

/// <summary>
/// A conservative metadata dependency graph, not an executable compatibility claim.
/// Issues preserve unsupported dispatch and other analysis gaps.
/// Node IDs and first reasons are deterministic for the same images and root set.
/// </summary>
public sealed record ReachabilityReport(ImmutableArray<ReachabilityNode> Nodes,
    ImmutableArray<int> Roots, ImmutableArray<ReachabilityEdge> Edges, ImmutableArray<ReachabilityIssue> Issues)
{
    public ImmutableArray<RuntimeTypeMapping> RuntimeMappings { get; init; } = [];
    public ImmutableArray<ResolvedAssembly> ContractAssemblies { get; init; } = [];
    /// <summary>Declarations whose implementation/layout expansion was explicitly deferred.</summary>
    public ImmutableArray<int> DeferredNodes { get; init; } = [];
    public bool HasUnresolvedDependencies => !Issues.IsEmpty || !ContractAssemblies.IsEmpty;
    public ImmutableArray<FeatureObservation> Features { get; init; } = [];
}

public sealed record ReachabilityLimits(int MaximumNodes = 10000, int MaximumEdges = 50000,
    int MaximumMethodInstances = 128, int MaximumBodyBytes = 1048576, int MaximumTotalBodyBytes = 16777216,
    int MaximumDispatchPairs = 100000);
