// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded exact IEquatable adaptation; see library/provenance.json.
namespace System.Collections.Generic
{
    public abstract class EqualityComparer<T> : IEqualityComparer<T>, Collections.IEqualityComparer
    {
        protected EqualityComparer() { }
        // Set only on the runtime-instance default singleton. Dictionary normalizes
        // value defaults without constructing unsupported default comparers for
        // unrelated reference keys or for custom comparer instances.
        private bool defaultValueComparer;
        internal bool IsDefaultValueComparer => defaultValueComparer;
        internal void MarkDefault() { defaultValueComparer = Cilica.Runtime.KeyOperations<T>.IsValueType(); }
        public static EqualityComparer<T> Default => Cilica.Runtime.EqualityComparerCache<T>.Get();
        public abstract bool Equals(T first, T second);
        public abstract int GetHashCode(T value);
        bool Collections.IEqualityComparer.Equals(object first, object second) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        int Collections.IEqualityComparer.GetHashCode(object value) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
    public sealed class GenericEqualityComparer<T> : EqualityComparer<T> where T : IEquatable<T>
    {
        public GenericEqualityComparer() { }
        public override bool Equals(T first, T second) => Cilica.Runtime.KeyOperations<T>.Equal(ref first, second);
        public override int GetHashCode(T value) => Cilica.Runtime.KeyOperations<T>.Hash(ref value);
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
}
namespace Cilica.Runtime
{
    internal static class EqualityComparerCache<T>
    {
        private static readonly System.Collections.Generic.EqualityComparer<T> Instance = Create();
        private static System.Collections.Generic.EqualityComparer<T> Create()
        {
            var comparer = EqualityComparerOperations<T>.Create();
            comparer.MarkDefault();
            return comparer;
        }
        internal static System.Collections.Generic.EqualityComparer<T> Get() => Instance;
    }
    internal static class EqualityComparerOperations<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern System.Collections.Generic.EqualityComparer<T> Create();
    }
    internal static class EquatableComparerFactory<T> where T : System.IEquatable<T>
    {
        internal static System.Collections.Generic.EqualityComparer<T> Create() => new System.Collections.Generic.GenericEqualityComparer<T>();
    }
}
