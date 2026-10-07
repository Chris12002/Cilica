using System.Collections.Generic;
using System.Reflection;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private bool ProtectedStaticCall(Work caller, Work target, int offset)
        {
            if (!integers64 || target.Binding.Kind != ObjectMethodKind.Static
                || (target.Binding.Attributes & MethodAttributes.MemberAccessMask) is not (MethodAttributes.Family or MethodAttributes.FamORAssem or MethodAttributes.FamANDAssem)
                || !ObjectMethodAccess.ProtectedStatic(caller.Binding, target.Binding, true, true)
                || caller.Binding.Kind is ObjectMethodKind.ValueInstance or ObjectMethodKind.ValueConstructor or ObjectMethodKind.PrimitiveInstance) return false;
            var current = caller.Resolved.DeclaringType;
            var visited = new HashSet<ObjectTypeId>();
            while (true)
            {
                if (!visited.Add(ClosedId(current, caller, offset)) || visited.Count > ObjectModelLimits.MaximumTypes)
                    Fail(caller, "Protected static access exceeds the bounded acyclic base-class chain.", offset, DiagnosticIds.TranslationLimit);
                var signature = SnapshotClosed(current, caller, offset).BaseType;
                ConstructedType? parent = signature switch {
                    TypeSignature.Named named => new(named.Definition, SignatureList<TypeSignature>.Empty),
                    TypeSignature.Generic generic => new(generic.Definition.Definition, generic.Arguments), _ => null };
                if (parent is null) return false;
                if (ClosedId(parent, caller, offset) == target.Binding.Owner)
                {
                    // Keep the real class chain for independent detached IR validation.
                    IncludeReference(caller.Resolved.DeclaringType, caller, offset);
                    return true;
                }
                current = parent;
            }
        }
    }
}
