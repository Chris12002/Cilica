using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

/// <summary>Caller-owned work ledger for member binding and related metadata snapshots. Not thread-safe.</summary>
public sealed class MemberResolutionBudget
{
    private int _remaining;
    private readonly MemberResolutionBudget? _parent;
    // A ledger also owns its bounded lookup work. Only successful complete name
    // scans are retained; signatures, mappings and closed contexts are not cached.
    internal Dictionary<ResolvedType, Dictionary<string, List<MethodDefinitionHandle>>> MethodNames { get; } = new();
    internal Dictionary<ResolvedType, Dictionary<string, List<FieldDefinitionHandle>>> FieldNames { get; } = new();
    internal Dictionary<(LibraryBindingPlan Plan, ResolvedAssembly Image, MethodDefinitionHandle Method),
        (ResolvedType Owner, MethodDefinitionHandle Method, MethodSignature Signature)> LibraryMethods { get; } = new();
    public MemberResolutionBudget(int maximumMembers = 65536)
    {
        if (maximumMembers is <= 0 or > 65536) throw new ArgumentOutOfRangeException(nameof(maximumMembers));
        _remaining = maximumMembers;
    }
    private MemberResolutionBudget(MemberResolutionBudget parent, int maximum)
    {
        _parent = parent; _remaining = maximum;
        MethodNames = parent.MethodNames; FieldNames = parent.FieldNames; LibraryMethods = parent.LibraryMethods;
    }
    internal MemberResolutionBudget Limit(int maximum) => maximum is > 0 and <= 65536
        ? new(this, Math.Min(maximum, Remaining)) : throw new ArgumentOutOfRangeException(nameof(maximum));
    public int Remaining => _parent is null ? _remaining : Math.Min(_remaining, _parent.Remaining);
    public bool TryConsume(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count > Remaining || _parent is not null && !_parent.TryConsume(count)) return false;
        _remaining -= count; return true;
    }
}
