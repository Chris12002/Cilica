using System.Collections.Generic;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<IrMethodId, List<GenericArrayUse>> _genericArrays = [];

        private void RecordGenericArray(Work work, MethodInstruction instruction, ObjectTypeId array, bool empty)
        {
            if (array.ArrayElement is not { Kind: ObjectArrayElementKind.Reference, Type: { } element }
                || !_snapshots.TryGetValue(element, out var layout) || layout.ExternallyVisible) return;
            int token = empty ? Token(work, instruction, 0x2b) : Token(work, instruction, 0x01, 0x02, 0x1b);
            // CheckOperandTypeUses already validated explicit names before binding.
            var uses = _sourceTypeUses[(work.Assembly.ContentHash, token)];
            var arguments = empty ? uses.MethodArguments : uses.Signatures;
            if (arguments.Length != 1 || arguments[0] is not TypeSignature.Variable variable) return;
            if (!_genericArrays.TryGetValue(work.Binding.Method, out var sites)) _genericArrays.Add(work.Binding.Method, sites = []);
            sites.Add(new(instruction.Offset, variable.IsMethod, variable.Index, empty));
        }

        private GenericArrayEvidence? GenericArrays(Work work)
        {
            if (!_genericArrays.TryGetValue(work.Binding.Method, out var sites)) return null;
            var context = ClosedGenericEvidence.Capture(work.Resolved.DeclaringType.Arguments, work.Resolved.MethodArguments);
            _ = context.Key(out int nodes);
            if (!_metadata.TryConsume(nodes + sites.Count)) Fail(work, "Generic array evidence exceeds the metadata work budget.", 0, DiagnosticIds.TranslationLimit);
            return new(context, [.. sites]);
        }
    }
}
