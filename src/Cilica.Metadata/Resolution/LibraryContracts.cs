using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cilica.Metadata.Analysis;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private ImmutableArray<ResolvedAssembly> ValidateContractAssemblies(ResolvedAssembly input,
        IEnumerable<ResolvedAssembly>? contracts)
    {
        if (contracts is null) return [];
        var selected = new Dictionary<(AssemblyIdentity, string), ResolvedAssembly>();
        foreach (var assembly in contracts)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            CheckOwner(assembly);
            if (assembly.Identity.Equals(input.Identity) && assembly.ContentHash == input.ContentHash)
                throw new ArgumentException("The input assembly cannot be a library contract boundary.", nameof(contracts));
            selected.TryAdd((assembly.Identity, assembly.ContentHash), assembly);
            if (selected.Count > 256) throw new ArgumentException("At most 256 library contract assemblies may be selected.", nameof(contracts));
        }
        return [.. selected.Values.OrderBy(a => a.Identity.DisplayName, StringComparer.Ordinal).ThenBy(a => a.ContentHash, StringComparer.Ordinal)];
    }

    private sealed partial class ReachabilityWalker
    {
        private readonly ImmutableArray<ResolvedAssembly> _contractAssemblies;
        private readonly HashSet<(AssemblyIdentity, string)> _contractImages;
        private readonly List<int> _deferredNodes = [];

        private bool IsContract(ResolvedAssembly assembly) => _contractImages.Contains((assembly.Identity, assembly.ContentHash));
        private bool IsContract(ReachableEntity entity) => IsContract(entity switch
        {
            ReachableEntity.Type type => type.Definition.Definition.Assembly,
            ReachableEntity.ArrayReceiver array => array.BaseType.Definition.Assembly,
            ReachableEntity.Method method => method.Definition.DeclaringType.Definition.Assembly,
            ReachableEntity.Field field => field.Definition.DeclaringType.Definition.Assembly,
            _ => throw new InvalidOperationException("Unknown reachable entity."),
        });
    }
}
