using System;

namespace Cilica.Metadata.Resolution;

/// <summary>Caller-owned work ledger for member binding and related metadata snapshots. Not thread-safe.</summary>
public sealed class MemberResolutionBudget
{
    private int _remaining;
    public MemberResolutionBudget(int maximumMembers = 65536)
    {
        if (maximumMembers is <= 0 or > 65536) throw new ArgumentOutOfRangeException(nameof(maximumMembers));
        _remaining = maximumMembers;
    }
    public int Remaining => _remaining;
    public bool TryConsume(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count > _remaining) return false;
        _remaining -= count; return true;
    }
}
