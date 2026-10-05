using System.Reflection;

namespace Cilica.Compiler;

internal static class ObjectMethodAccess
{
    internal static bool Allowed(ObjectMethodBinding caller, ObjectMethodBinding target, bool sameAssemblyCalls)
    {
        if (target.TypeInitializerName is not null) return false;
        if (caller.Owner == target.Owner) return true;
        var access = target.Attributes & MethodAttributes.MemberAccessMask;
        return access == MethodAttributes.Public && target.OwnerExternallyVisible
            || sameAssemblyCalls && caller.Owner.ContentHash == target.Owner.ContentHash && target.OwnerAssemblyVisible
                && access is MethodAttributes.Public or MethodAttributes.Assembly or MethodAttributes.FamORAssem;
    }
}
