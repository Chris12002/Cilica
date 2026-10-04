using System.Collections.Immutable;
using System.Linq;

namespace Cilica.Metadata.Resolution;

/// <summary>Caller-selected definitions, pinned by their resolver-owned image snapshots.</summary>
public sealed record LibraryTypeMapping(ResolvedType Contract, ResolvedType Implementation);

/// <summary>Detached evidence; tokens are meaningful only with the full image identity and hash.</summary>
public sealed record LibraryTypeIdentity(AssemblyIdentity Assembly, string ContentHash, int Token);
public sealed record LibraryTypeBinding(LibraryTypeIdentity Contract, LibraryTypeIdentity Implementation,
    string Namespace, ImmutableArray<string> Names, int GenericArity);

/// <summary>
/// Validated, session-owned type correspondence. It grants no intrinsic permissions
/// and does not change ordinary resolution, member binding, audits or compilation.
/// Bindings are detached evidence and can be retained after the resolver is disposed.
/// </summary>
public sealed class LibraryBindingPlan
{
    public const string Profile = "explicit-library-type-bindings-v1";
    public const int MaximumMappings = 1024;

    internal LibraryBindingPlan(AssemblyResolver owner, ImmutableDictionary<ResolvedType, ResolvedType> mappings,
        ImmutableArray<LibraryTypeBinding> bindings)
    {
        Owner = owner;
        Mappings = mappings;
        Bindings = bindings;
        ContractImages = mappings.Keys.Select(t => t.Assembly).DistinctBy(a => a.Identity)
            .ToImmutableDictionary(a => a.Identity, a => a.ContentHash);
        SelectedImages = mappings.SelectMany(p => new[] { p.Key.Assembly, p.Value.Assembly }).DistinctBy(a => a.Identity)
            .ToImmutableDictionary(a => a.Identity, a => a.ContentHash);
    }

    internal AssemblyResolver Owner { get; }
    internal ImmutableDictionary<ResolvedType, ResolvedType> Mappings { get; }
    internal ImmutableDictionary<AssemblyIdentity, string> ContractImages { get; }
    internal ImmutableDictionary<AssemblyIdentity, string> SelectedImages { get; }
    public ImmutableArray<LibraryTypeBinding> Bindings { get; }
}
