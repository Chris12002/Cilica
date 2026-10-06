using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;

internal static class ObjectLexicalAccess
{
    internal static bool Same(ImmutableArray<LexicalTypeOwner> left, ImmutableArray<LexicalTypeOwner> right) =>
        !left.IsDefault && !right.IsDefault && left.SequenceEqual(right);

    internal static bool SameBinding(ObjectMethodBinding left, ObjectMethodBinding right) =>
        Same(left.LexicalOwners, right.LexicalOwners) && GenericInterfaceMethodRules.Same(left.GenericInterfaceMethod, right.GenericInterfaceMethod)
        && (left with { LexicalOwners = right.LexicalOwners, GenericInterfaceMethod = right.GenericInterfaceMethod }) == right;

    internal static bool SameStatic(ObjectStaticField left, ObjectStaticField right) =>
        Same(left.LexicalOwners, right.LexicalOwners) && (left with { LexicalOwners = right.LexicalOwners }) == right;

    internal static bool Valid(ObjectTypeId owner, ImmutableArray<LexicalTypeOwner> chain) => !chain.IsDefault
        && (chain.IsEmpty || chain.Length <= ObjectModelLimits.MaximumOwnerDepth && chain[0]?.Token == owner.Token
            && chain.All(o => o is not null && (o.Token >> 24) == 2 && (o.Token & 0xffffff) > 1 && o.GenericArity is >= 0 and <= 4096)
            && chain.Select(o => o.Token).Distinct().Count() == chain.Length
            && chain[..^1].All(o => o.Visibility is >= TypeAttributes.NestedPublic and <= TypeAttributes.NestedFamORAssem)
            && chain[^1].Visibility is TypeAttributes.Public or TypeAttributes.NotPublic);

    internal static bool Public(ImmutableArray<LexicalTypeOwner> chain) => !chain.IsDefaultOrEmpty
        && chain[^1].Visibility == TypeAttributes.Public && chain[..^1].All(o => o.Visibility == TypeAttributes.NestedPublic);

    internal static bool AssemblyVisible(ImmutableArray<LexicalTypeOwner> chain) => !chain.IsDefaultOrEmpty
        && chain[..^1].All(o => o.Visibility is TypeAttributes.NestedPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem);

    internal static bool Visible(ObjectMethodBinding caller, ObjectTypeId target, ImmutableArray<LexicalTypeOwner> chain)
    {
        if (caller.Owner == target) return true;
        if (chain.IsDefaultOrEmpty || !Valid(target, chain)) return false;
        if (Public(chain)) return true;
        if (caller.Owner.ContentHash != target.ContentHash || target.Specialization.Length != 0 || caller.Owner.Specialization.Length != 0
            || chain.Any(o => o.GenericArity != 0) || caller.LexicalOwners.Any(o => o.GenericArity != 0)) return false;
        for (int i = 0; i < chain.Length - 1; ++i)
        {
            switch (chain[i].Visibility)
            {
                case TypeAttributes.NestedPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem: break;
                case TypeAttributes.NestedPrivate:
                    int parent = chain[i + 1].Token;
                    if (caller.Owner.Token != parent && !caller.LexicalOwners.Any(o => o.Token == parent)) return false;
                    break;
                default: return false; // Protected nesting needs separate family checks.
            }
        }
        return true;
    }

    // Private member scope extends into nested type declarations, not sibling or
    // enclosing declarations. Closed generic owner ancestry needs separate evidence.
    internal static bool WithinDeclaringType(ObjectMethodBinding caller, ObjectMethodBinding target)
    {
        if (caller.Owner.ContentHash != target.Owner.ContentHash
            || caller.Owner.Specialization.Length != 0 || target.Owner.Specialization.Length != 0
            || caller.LexicalOwners.IsDefaultOrEmpty || target.LexicalOwners.IsDefaultOrEmpty
            || !Valid(caller.Owner, caller.LexicalOwners) || !Valid(target.Owner, target.LexicalOwners)
            || caller.LexicalOwners.Any(o => o.GenericArity != 0) || target.LexicalOwners.Any(o => o.GenericArity != 0)) return false;
        for (int i = 1; i < caller.LexicalOwners.Length; ++i)
            if (caller.LexicalOwners[i].Token == target.Owner.Token)
                return Same(caller.LexicalOwners[i..], target.LexicalOwners);
        return false;
    }

    internal static void Consistent(IEnumerable<(ObjectTypeId Owner, ImmutableArray<LexicalTypeOwner> Chain)> declarations)
    {
        var facts = new Dictionary<(string Hash, int Token), (TypeAttributes Visibility, int Arity, int Parent)>();
        int work = 0;
        foreach (var (owner, chain) in declarations)
        {
            if (!Valid(owner, chain)) throw new ArgumentException("Invalid lexical declaring-type chain.");
            for (int i = 0; i < chain.Length; ++i)
            {
                if (++work > ObjectModelLimits.MaximumMetadataMembers) throw new ArgumentException("Lexical declaration work limit exceeded.");
                var node = chain[i]; var key = (owner.ContentHash, node.Token);
                var fact = (node.Visibility, node.GenericArity, i + 1 < chain.Length ? chain[i + 1].Token : 0);
                if (facts.TryGetValue(key, out var prior) && prior != fact) throw new ArgumentException("Contradictory lexical declaring-type facts.");
                facts[key] = fact;
            }
        }
    }
}
