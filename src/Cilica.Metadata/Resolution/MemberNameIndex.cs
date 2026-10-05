using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private static IEnumerable<MethodDefinitionHandle> MethodCandidates(ResolvedType owner, string name, MemberResolutionBudget? budget)
    {
        var reader = owner.Assembly.Image.Reader;
        var members = reader.GetTypeDefinition(owner.Handle).GetMethods();
        if (budget is null) return members;
        if (!budget.MethodNames.TryGetValue(owner, out var names))
        {
            InspectMember(budget); // Charge even empty owners, bounding index count.
            names = new(StringComparer.Ordinal);
            foreach (var handle in members)
            {
                InspectMember(budget);
                string key = reader.GetString(reader.GetMethodDefinition(handle).Name);
                if (!names.TryGetValue(key, out var matches)) names.Add(key, matches = new());
                matches.Add(handle);
            }
            budget.MethodNames.Add(owner, names);
        }
        return names.TryGetValue(name, out var candidates) ? candidates : Array.Empty<MethodDefinitionHandle>();
    }

    private static IEnumerable<FieldDefinitionHandle> FieldCandidates(ResolvedType owner, string name, MemberResolutionBudget? budget)
    {
        var reader = owner.Assembly.Image.Reader;
        var members = reader.GetTypeDefinition(owner.Handle).GetFields();
        if (budget is null) return members;
        if (!budget.FieldNames.TryGetValue(owner, out var names))
        {
            InspectMember(budget);
            names = new(StringComparer.Ordinal);
            foreach (var handle in members)
            {
                InspectMember(budget);
                string key = reader.GetString(reader.GetFieldDefinition(handle).Name);
                if (!names.TryGetValue(key, out var matches)) names.Add(key, matches = new());
                matches.Add(handle);
            }
            budget.FieldNames.Add(owner, names);
        }
        return names.TryGetValue(name, out var candidates) ? candidates : Array.Empty<FieldDefinitionHandle>();
    }
}
