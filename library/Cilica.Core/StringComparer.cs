// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded ordinal adaptation; see library/provenance.json.
namespace System.Collections.Generic
{
    public interface IComparer<in T> { int Compare(T first, T second); }
    public interface IEqualityComparer<in T> { bool Equals(T first, T second); int GetHashCode(T value); }
}
namespace System
{
    public abstract class StringComparer : Collections.IComparer, Collections.IEqualityComparer,
        Collections.Generic.IComparer<string>, Collections.Generic.IEqualityComparer<string>
    {
        protected StringComparer() { }
        public static StringComparer OrdinalIgnoreCase => Cilica.Runtime.OrdinalIgnoreCaseComparerCache.Instance;
        public static StringComparer Ordinal => Cilica.Runtime.OrdinalComparerCache.Instance;
        public abstract int Compare(string first, string second);
        public abstract bool Equals(string first, string second);
        public abstract int GetHashCode(string value);
        // Nongeneric comparison/equality need separate semantics.
        public int Compare(object first, object second) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public bool Equals(object first, object second) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public int GetHashCode(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
    internal sealed class OrdinalCaseSensitiveComparer : StringComparer
    {
        public OrdinalCaseSensitiveComparer() { }
        public override int Compare(string first, string second) => String.CompareOrdinal(first, second);
        public override bool Equals(string first, string second) => first == second;
        public override int GetHashCode(string value)
        {
            if (value == null) throw new ArgumentNullException("obj");
            // Numeric hashes are implementation-specific. Share the owned ordinal
            // UTF-16 algorithm used by collection keys, without new intrinsics.
            return Cilica.Runtime.StringKeyOperations.Hash(ref value);
        }
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
}

namespace Cilica.Runtime
{
    internal static class OrdinalComparerCache
    {
        public static readonly System.StringComparer Instance = new System.OrdinalCaseSensitiveComparer();
    }
}
