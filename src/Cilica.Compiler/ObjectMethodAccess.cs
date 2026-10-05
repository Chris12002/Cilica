using System.Reflection;

namespace Cilica.Compiler;

internal static class ObjectMethodAccess
{
    internal static bool Allowed(ObjectMethodBinding caller, ObjectMethodBinding target, bool sameAssemblyCalls)
    {
        if (target.TypeInitializerName is not null) return false;
        if (caller.Owner == target.Owner) return true;
        var access = target.Attributes & MethodAttributes.MemberAccessMask;
        if (access == MethodAttributes.Public && target.OwnerExternallyVisible) return true;
        return sameAssemblyCalls && caller.Owner.ContentHash == target.Owner.ContentHash
            && access is MethodAttributes.Public or MethodAttributes.Assembly or MethodAttributes.FamORAssem
            && (target.OwnerAssemblyVisible || ObjectLexicalAccess.Visible(caller, target.Owner, target.LexicalOwners));
    }
}
