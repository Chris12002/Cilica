using System.Collections.Generic;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<IrMethodId, List<GenericTypeTestUse>> _genericTypeTests = [];
        private void RecordGenericTypeTest(Work work, MethodInstruction instruction, bool cast)
        {
            int token = Token(work, instruction, 0x01, 0x02, 0x1b);
            var signatures = _sourceTypeUses[(work.Assembly.ContentHash, token)].Signatures;
            if (signatures.Length != 1) return;
            bool vector = signatures[0] is TypeSignature.Vector;
            var element = signatures[0] is TypeSignature.Vector array ? array.Element : signatures[0];
            if (element is not TypeSignature.Variable variable) return;
            if (!_genericTypeTests.TryGetValue(work.Binding.Method, out var sites)) _genericTypeTests.Add(work.Binding.Method, sites = []);
            sites.Add(new(instruction.Offset, variable.IsMethod, variable.Index, vector, cast));
        }
        private GenericTypeTestEvidence? GenericTypeTests(Work work)
        {
            if (!_genericTypeTests.TryGetValue(work.Binding.Method, out var sites)) return null;
            var context = ClosedGenericEvidence.Capture(work.Resolved.DeclaringType.Arguments, work.Resolved.MethodArguments);
            _ = context.Key(out int nodes);
            if (!_metadata.TryConsume(nodes + sites.Count)) Fail(work, "Generic type-test evidence exceeds the metadata work budget.", 0, DiagnosticIds.TranslationLimit);
            return new(context, [.. sites]);
        }
    }
}
