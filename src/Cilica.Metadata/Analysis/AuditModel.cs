using System.Collections.Immutable;

namespace Cilica.Metadata.Analysis;

/// <summary>Detached evidence: safe to retain after disposing the resolver.</summary>
public sealed record AuditAssembly(int Id, string Identity, string ContentHash, bool IsReferenceAssembly, bool IsInput);
public sealed record AuditRuntimeType(string Primitive, int Assembly, string Token);
public sealed record AuditNode(int Id, int Assembly, string Kind, string Token, string Name,
    string Instantiation, int? FirstReason, bool IsExternalMember);
public sealed record AuditReport(string Input, ImmutableArray<string> Selection, ReachabilityLimits Limits,
    ImmutableArray<AuditAssembly> Assemblies, ImmutableArray<AuditNode> Nodes, ImmutableArray<int> Roots,
    ImmutableArray<ReachabilityEdge> Edges, ImmutableArray<FeatureObservation> Features,
    ImmutableArray<ReachabilityIssue> Issues)
{
    public int? RuntimeLibrary { get; init; }
    public ImmutableArray<AuditRuntimeType> RuntimeMappings { get; init; } = [];
    public ImmutableArray<int> ContractAssemblies { get; init; } = [];
    public ImmutableArray<int> DeferredNodes { get; init; } = [];
    public bool IsContractInventory => !ContractAssemblies.IsEmpty;
    public string AnalysisProfile => IsContractInventory ? ContractProfile : Profile;
    public string AnalysisScope => IsContractInventory ? ContractScope : Scope;
    public bool HasErrors => !Issues.IsEmpty;
    public string Status => HasErrors ? "incomplete" : IsContractInventory ? "contract-inventory" : "bounded-graph-analyzed";
    public string GeneralFilterStatus
    {
        get
        {
            foreach (var feature in Features)
                if (feature.Feature == "filter-general") return "observed";
            return HasErrors || IsContractInventory ? "unresolved" : "not-observed-in-bounded-graph";
        }
    }

    public const string Profile = "explicit-metadata-closure-v5";
    public const string ContractProfile = "explicit-library-contracts-v1";
    public const string ContractScope = "Conservative application IL inventory with explicitly selected assembly identities and content hashes as library boundaries. " +
        "Library declarations, signatures and ancestry are retained; bodies, layout fields and initializers are not expanded. " +
        "Library callbacks and implementation dependencies remain unresolved. This is neither an implementation closure nor a compatibility or trusted-library claim.";
    public const string Scope = "Conservative IL scan; receivers are included constructed named types and selected-library array shapes, not all possible external inputs. " +
        "Feature names are review signals, not exhaustive semantic detection or support claims. " +
        "An audit does not clear ADR 0004: the trial ported-library implementation closure is still required.";
}
