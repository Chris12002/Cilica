using System.Collections.Generic;
using System.Reflection;
using Cilica.Metadata.Analysis;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private sealed partial class ReachabilityWalker
    {
        // One queued entry per ready site, not one per possible pair. Cursors
        // traverse the append-only receiver list once for each unsealed site.
        private readonly List<int> _receivers = [];
        private readonly Queue<DispatchSite> _readyDispatch = [];
        private readonly List<DispatchSite> _waitingDispatch = [];
        private int _dispatchPairCount;

        private void RegisterReceiver(int id)
        {
            _receivers.Add(id);
            // Every waiting site has consumed all previously included receivers.
            // Waking it once handles any further types added before its next turn.
            foreach (DispatchSite site in _waitingDispatch) _readyDispatch.Enqueue(site);
            _waitingDispatch.Clear();
        }

        private void RegisterDispatch(DispatchSite site)
        {
            if (_stopped) return;
            _dispatchSites.Add(site);
            if (site.Constraint is { } exact && (TypeDefinition(exact).Attributes & TypeAttributes.Sealed) != 0)
                site.ExactReceiver = site.ConstraintId;
            if (site.ExactReceiver is not null || _receivers.Count > 0) _readyDispatch.Enqueue(site);
            else _waitingDispatch.Add(site);
        }

        private void ExpandDispatchPair()
        {
            if (!_readyDispatch.TryDequeue(out DispatchSite? site)) return;
            if (_dispatchPairCount >= _limits.MaximumDispatchPairs)
            {
                Stop(site.Caller, site.Offset, "Dispatch receiver/call-site pair limit reached.");
                return;
            }
            int receiverId = site.ExactReceiver ?? _receivers[site.NextReceiver++];
            _dispatchPairCount++;
            Guard(site.Caller, site.Offset, () =>
            {
                switch (_nodes[receiverId].Entity)
                {
                    case ReachableEntity.Type receiver: Dispatch(site, receiver.Definition, receiverId); break;
                    case ReachableEntity.ArrayReceiver array: DispatchArray(site, array, receiverId); break;
                }
            });
            // Advance even after a located binding failure. Another receiver may
            // still supply evidence; the failure remains in the partial report.
            if (_stopped || site.ExactReceiver is not null) return;
            if (site.NextReceiver < _receivers.Count) _readyDispatch.Enqueue(site);
            else _waitingDispatch.Add(site);
        }
    }
}
