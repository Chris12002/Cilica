using System;
using System.Collections.Immutable;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Rejects unmodeled runtime array contracts before nominal-only type tests can silently return false.</summary>
internal static class ObjectArrayInterfaces
{
    internal static int Check(AssemblyResolver resolver, ResolvedAssembly library,
        ImmutableArray<ObjectInterface> contracts, int maximumWork, MemberResolutionBudget shared)
    {
        maximumWork = Math.Min(maximumWork, shared.Remaining);
        if (maximumWork <= 0) throw Error(DiagnosticIds.TranslationLimit, "Array interface metadata budget exhausted.");
        var budget = new MemberResolutionBudget(maximumWork);
        bool Mentioned(ResolvedType type) => contracts.Any(c => c.Source.ContentHash == type.Assembly.ContentHash
            && c.Source.Token == System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(type.Handle));
        ResolvedType? Optional(ResolutionResult<ResolvedType> result)
        {
            if (!budget.TryConsume(1)) throw Error(DiagnosticIds.TranslationLimit, "Array interface metadata budget exhausted.");
            if (result.Value is not null) return result.Value;
            if (result.Diagnostics.All(d => d.Id == DiagnosticIds.TypeNotFound)) return null;
            throw new ObjectStringType.Failure(result.Diagnostics[0]);
        }
        var array = Optional(resolver.ResolveRuntimeArrayBase(library));
        if (array is not null)
        {
            var inherited = resolver.ReadIntrinsicInterfaceDefinitions(array, budget);
            if (inherited.Value is not { } value) throw new ObjectStringType.Failure(inherited.Diagnostics[0]);
            if (value.Types.Any(Mentioned)) throw Unsupported();
        }
        // These vector contracts are supplied by the runtime rather than ordinary
        // InterfaceImpl rows on System.Array. Resolve against the selected image;
        // an application type with the same name receives no special treatment.
        foreach (string name in new[] { "IEnumerable`1", "ICollection`1", "IList`1", "IReadOnlyCollection`1", "IReadOnlyList`1" })
            if (Optional(resolver.ResolveType(library, "System.Collections.Generic", name)) is { } type && Mentioned(type))
                throw Unsupported();
        int used = maximumWork - budget.Remaining;
        if (!shared.TryConsume(used)) throw Error(DiagnosticIds.TranslationLimit, "Array interface metadata budget exhausted.");
        return used;
    }
    private static ObjectStringType.Failure Unsupported() => Error(DiagnosticIds.ExecutionUnsupported,
        "Array interface conversions and dispatch require explicit runtime helper contracts before execution.");
    private static ObjectStringType.Failure Error(string id, string message) => new(new(id, DiagnosticSeverity.Error, message));
}
