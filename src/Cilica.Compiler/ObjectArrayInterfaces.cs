using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Collections.Generic;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Authenticates runtime-provided vector contracts; other array interfaces remain diagnostic.</summary>
internal static class ObjectArrayInterfaces
{
    internal static int Check(AssemblyResolver resolver, ResolvedAssembly library,
        List<ObjectInterface> contracts, IReadOnlyDictionary<ObjectTypeId, ConstructedType> types, int maximumWork, MemberResolutionBudget shared, bool closed)
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
        var enumerable = Optional(resolver.ResolveType(library, "System.Collections", "IEnumerable"));
        var collection = Optional(resolver.ResolveType(library, "System.Collections", "ICollection"));
        var list = Optional(resolver.ResolveType(library, "System.Collections", "IList"));
        void Mark(ResolvedType? type, ObjectArrayInterfaceKind kind)
        {
            if (type is null) return;
            for (int index = 0; index < contracts.Count; ++index)
                if (contracts[index].Source.ContentHash == type.Assembly.ContentHash
                    && contracts[index].Source.Token == System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(type.Handle))
                {
                    if (!closed) throw Unsupported();
                    contracts[index] = contracts[index] with { ArrayContract = kind };
                }
        }
        Mark(collection, ObjectArrayInterfaceKind.NonGenericCollection);
        Mark(list, ObjectArrayInterfaceKind.NonGenericList);
        Mark(enumerable, ObjectArrayInterfaceKind.Enumerable);
        var array = Optional(resolver.ResolveRuntimeArrayBase(library));
        if (array is not null)
        {
            var inherited = resolver.ReadIntrinsicInterfaceDefinitions(array, budget);
            if (inherited.Value is not { } value) throw new ObjectStringType.Failure(inherited.Diagnostics[0]);
            if (value.Types.Any(t => Mentioned(t) && (!closed || t != enumerable && t != collection && t != list))) throw Unsupported();
        }
        // These vector contracts are supplied by the runtime rather than ordinary
        // InterfaceImpl rows on System.Array. Resolve against the selected image;
        // an application type with the same name receives no special treatment.
        foreach (var (name, kind) in new[] {
            ("IEnumerable`1", ObjectArrayInterfaceKind.GenericEnumerable), ("ICollection`1", ObjectArrayInterfaceKind.Collection),
            ("IList`1", ObjectArrayInterfaceKind.List), ("IReadOnlyCollection`1", ObjectArrayInterfaceKind.ReadOnlyCollection),
            ("IReadOnlyList`1", ObjectArrayInterfaceKind.ReadOnlyList) })
            Mark(Optional(resolver.ResolveType(library, "System.Collections.Generic", name)), kind);
        int used = maximumWork - budget.Remaining;
        if (!shared.TryConsume(used)) throw Error(DiagnosticIds.TranslationLimit, "Array interface metadata budget exhausted.");
        return used;
    }
    private static ObjectStringType.Failure Unsupported() => Error(DiagnosticIds.ExecutionUnsupported,
        "Array interface conversions and dispatch require explicit runtime helper contracts before execution.");
    private static ObjectStringType.Failure Error(string id, string message) => new(new(id, DiagnosticSeverity.Error, message));
}
