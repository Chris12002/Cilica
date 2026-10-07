using System.Collections.Immutable;
using System.Reflection;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;

internal static class ObjectStaticFieldAccess
{
    internal static bool Allowed(ObjectMethodBinding caller, ObjectTypeId owner, FieldAttributes attributes,
        bool externallyVisible, ImmutableArray<LexicalTypeOwner> chain, bool sameAssembly)
    {
        var access = attributes & FieldAttributes.FieldAccessMask;
        if (access == FieldAttributes.PrivateScope) return false;
        if (caller.Owner == owner) return true;
        if (access == FieldAttributes.Public)
            return externallyVisible || sameAssembly && ObjectLexicalAccess.Visible(caller, owner, chain);
        if (!sameAssembly || caller.Owner.ContentHash != owner.ContentHash
            || access is not (FieldAttributes.Assembly or FieldAttributes.FamORAssem)
            || chain.IsDefaultOrEmpty || caller.LexicalOwners.IsDefaultOrEmpty
            || !ObjectLexicalAccess.Valid(owner, chain) || !ObjectLexicalAccess.Valid(caller.Owner, caller.LexicalOwners)) return false;
        return ObjectLexicalAccess.AssemblyVisible(chain) || ObjectLexicalAccess.Visible(caller, owner, chain);
    }
}
