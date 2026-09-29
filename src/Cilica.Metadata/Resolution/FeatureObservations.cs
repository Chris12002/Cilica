using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private sealed partial class ReachabilityWalker
    {
        private readonly HashSet<FeatureObservation> _features = [];

        private void Observe(int node, int? offset, string feature, string evidence) =>
            _features.Add(new(node, offset, feature, evidence));

        // These are name-based review signals, not trusted framework identities or
        // exhaustive semantic detection. A same-named application type can trigger them.
        private void ObserveType(int id, string ns, string name)
        {
            if (ns == "System" && name == "Decimal") Observe(id, null, "decimal", "System.Decimal type name.");
            if (ns == "System" && name is "Single" or "Double") Observe(id, null, "floating-point", "System floating-point type name.");
            if (ns == "System.Threading" || ns.StartsWith("System.Threading.", StringComparison.Ordinal))
                Observe(id, null, "threading", "System.Threading namespace name.");
            if (ns == "System.Threading.Tasks" || ns == "System.Runtime.CompilerServices"
                && (name.Contains("Await", StringComparison.Ordinal) || name.Contains("Async", StringComparison.Ordinal)))
                Observe(id, null, "async", "Task/awaiter/async infrastructure type name.");
            if (ns == "System.Reflection" || ns.StartsWith("System.Reflection.", StringComparison.Ordinal)
                || ns == "System" && name is "Type" or "Activator")
                Observe(id, null, "reflection", "Reflection-related type name.");
            if (ns == "System.Runtime.CompilerServices" && name == "Unsafe"
                || ns == "System.Runtime.InteropServices" && name is "Marshal" or "MemoryMarshal")
                Observe(id, null, "unsafe", "Unsafe/native-memory helper type name.");
        }

        private void ObserveDynamicMethod(int id, ResolvedMethod method)
        {
            MetadataReader reader = method.DeclaringType.Definition.Assembly.Image.Reader;
            TypeDefinition owner = reader.GetTypeDefinition(method.DeclaringType.Definition.Handle);
            string ns = reader.GetString(owner.Namespace);
            string type = reader.GetString(owner.Name);
            string name = reader.GetString(reader.GetMethodDefinition(method.Handle).Name);
            if (ns == "System.Reflection" || ns.StartsWith("System.Reflection.", StringComparison.Ordinal)
                || ns == "System" && (type == "Activator" || type == "Type" && name.StartsWith("Get", StringComparison.Ordinal)))
            {
                Observe(id, null, "reflection", "Reflection-related member name; dynamic targets are not resolved.");
                Issue(id, null, DiagnosticIds.ReachabilityIncomplete,
                    "Reflection-related member requires dynamic-target analysis outside this profile (name-based conservative signal).");
            }
        }

        private void ObserveInstruction(int id, Instruction instruction)
        {
            string name = instruction.Code.Name;
            if (name is "localloc" or "cpblk" or "initblk" or "calli" or "sizeof" or "mkrefany" or "refanyval" or "refanytype"
                || name.StartsWith("ldind.", StringComparison.Ordinal) || name.StartsWith("stind.", StringComparison.Ordinal))
                Observe(id, instruction.Offset, "unsafe", name + " instruction (may also arise from safe managed code).");
            if (name is "ldc.r4" or "ldc.r8" or "conv.r4" or "conv.r8" or "conv.r.un"
                or "ldind.r4" or "ldind.r8" or "stind.r4" or "stind.r8"
                or "ldelem.r4" or "ldelem.r8" or "stelem.r4" or "stelem.r8")
                Observe(id, instruction.Offset, "floating-point", name + " instruction.");
        }
    }
}
