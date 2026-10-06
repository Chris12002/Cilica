using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private bool ProtectedBaseConstructor(Work caller, Work target, int offset)
        {
            if (!integers64 || caller.Binding.Kind != ObjectMethodKind.Constructor || target.Binding.Kind != ObjectMethodKind.Constructor) return false;
            var signature = SnapshotClosed(caller.Resolved.DeclaringType, caller, offset).BaseType;
            ConstructedType? parent = signature switch {
                TypeSignature.Named named => new(named.Definition, SignatureList<TypeSignature>.Empty),
                TypeSignature.Generic generic => new(generic.Definition.Definition, generic.Arguments), _ => null };
            return parent is not null && ObjectMethodAccess.ProtectedBaseConstructor(caller.Binding, target.Binding, ClosedId(parent, caller, offset), true);
        }
    }
}
