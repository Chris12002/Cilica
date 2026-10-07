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
        if (sameAssemblyCalls && access == MethodAttributes.Private && ObjectLexicalAccess.WithinDeclaringType(caller, target)) return true;
        return sameAssemblyCalls && caller.Owner.ContentHash == target.Owner.ContentHash
            && access is MethodAttributes.Public or MethodAttributes.Assembly or MethodAttributes.FamORAssem
            && (target.OwnerAssemblyVisible || ObjectLexicalAccess.Visible(caller, target.Owner, target.LexicalOwners));
    }
    internal static bool ProtectedStatic(ObjectMethodBinding caller, ObjectMethodBinding target, bool inherited, bool enabled)
    {
        if (!enabled || !inherited || target.Kind != ObjectMethodKind.Static || target.TypeInitializerName is not null
            || (target.Attributes & MethodAttributes.Static) == 0) return false;
        var access = target.Attributes & MethodAttributes.MemberAccessMask;
        bool sameImage = caller.Owner.ContentHash == target.Owner.ContentHash;
        return (access is MethodAttributes.Family or MethodAttributes.FamORAssem || access == MethodAttributes.FamANDAssem && sameImage)
            && (target.OwnerExternallyVisible || sameImage
                && (target.OwnerAssemblyVisible || ObjectLexicalAccess.Visible(caller, target.Owner, target.LexicalOwners)));
    }

    // The caller separately proves a direct call on its original this receiver.
    internal static bool ProtectedBaseConstructor(ObjectMethodBinding caller, ObjectMethodBinding target, ObjectTypeId? directBase, bool enabled)
    {
        if (!enabled || caller.Kind != ObjectMethodKind.Constructor || target.Kind != ObjectMethodKind.Constructor
            || directBase is null || target.Owner != directBase || target.TypeInitializerName is not null) return false;
        var access = target.Attributes & MethodAttributes.MemberAccessMask;
        bool sameImage = caller.Owner.ContentHash == target.Owner.ContentHash;
        return (access is MethodAttributes.Family or MethodAttributes.FamORAssem || access == MethodAttributes.FamANDAssem && sameImage)
            && (target.OwnerExternallyVisible || sameImage
                && (target.OwnerAssemblyVisible || ObjectLexicalAccess.Visible(caller, target.Owner, target.LexicalOwners)));
    }

}
