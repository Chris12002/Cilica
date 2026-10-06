using System;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>Checks detached owner/method argument spaces without granting access to their types.</summary>
internal static class GenericInterfaceMethodRules
{
    internal static bool Identity(ClosedGenericEvidence? evidence, IrMethodId method, ObjectTypeId owner, ref int work)
    {
        if (evidence is null) return method.Specialization == owner.Specialization;
        if (evidence.Methods.IsDefaultOrEmpty) return false;
        string key = evidence.Key(out int nodes);
        work = checked(work + nodes);
        return work <= ObjectModelLimits.MaximumMetadataMembers && key == method.Specialization
            && new ClosedGenericEvidence(evidence.Types, []).Key() == owner.Specialization;
    }

    internal static string? Arguments(ClosedGenericEvidence? evidence) => evidence is null ? null
        : new ClosedGenericEvidence([], evidence.Methods).Key();

    internal static bool Same(ClosedGenericEvidence? left, ClosedGenericEvidence? right) =>
        left is null ? right is null : right is not null && left.Key() == right.Key();
}
