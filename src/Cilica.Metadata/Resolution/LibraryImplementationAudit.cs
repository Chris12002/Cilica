using System.Collections.Generic;
using System.Reflection.Metadata;
using Cilica.Metadata.Analysis;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private sealed partial class ReachabilityWalker
    {
        private readonly List<LibraryMemberUse> _libraryMembers = [];
        private readonly HashSet<LibraryMemberUse> _libraryMemberSet = [];

        // Evidence belongs to this walker, never mutable resolver-wide observation state.
        private ResolvedMethod ResolveAuditedMethod(ResolvedAssembly assembly, EntityHandle token, BindingContext? context = null)
        {
            if (_resolver._librarySource is null) return _resolver.Method(assembly, token, context);
            var result = _resolver._librarySource.ResolveLibraryMethodCore(_resolver._libraryPlan!, assembly, token, context, null);
            if (result.Value is not { } binding) throw new BindingFailureException(result.Diagnostics);
            if (binding.Contract != binding.Implementation)
                RecordLibraryMember(new(assembly, token, new ReachableEntity.Method(binding.Contract), new ReachableEntity.Method(binding.Implementation)));
            return binding.Implementation;
        }

        private ResolvedField ResolveAuditedField(ResolvedAssembly assembly, EntityHandle token, BindingContext? context = null)
        {
            if (_resolver._librarySource is null) return _resolver.Field(assembly, token, context);
            var result = _resolver._librarySource.ResolveLibraryFieldCore(_resolver._libraryPlan!, assembly, token, context, null);
            if (result.Value is not { } binding) throw new BindingFailureException(result.Diagnostics);
            if (binding.Contract != binding.Implementation)
                RecordLibraryMember(new(assembly, token, new ReachableEntity.Field(binding.Contract), new ReachableEntity.Field(binding.Implementation)));
            return binding.Implementation;
        }

        private void RecordLibraryMember(LibraryMemberUse use)
        {
            if (_libraryMemberSet.Contains(use)) return;
            if (_libraryMembers.Count >= _limits.MaximumEdges)
                throw Failure(DiagnosticIds.ReachabilityLimit, "Library member projection evidence exceeds the reachability edge limit.");
            _libraryMemberSet.Add(use); _libraryMembers.Add(use);
        }
    }
}
