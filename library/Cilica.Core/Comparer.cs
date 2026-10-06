// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded default integer adaptation; see library/provenance.json.
namespace System.Collections.Generic
{
    public abstract class Comparer<T> : IComparer<T>, Collections.IComparer
    {
        protected Comparer() { }
        public static Comparer<T> Default => Cilica.Runtime.ComparerCache<T>.Get();
        public abstract int Compare(T first, T second);
        int Collections.IComparer.Compare(object first, object second) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
    public sealed class GenericComparer<T> : Comparer<T> where T : IComparable<T>
    {
        public GenericComparer() { }
        public override int Compare(T first, T second) => Cilica.Runtime.ComparerOperations<T>.Compare(ref first, second);
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
}
namespace Cilica.Runtime
{
    internal static class ComparerCache<T>
    {
        private static readonly System.Collections.Generic.Comparer<T> Instance = ComparerOperations<T>.Create();
        internal static System.Collections.Generic.Comparer<T> Get() => Instance;
    }
    internal static class ComparerOperations<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern System.Collections.Generic.Comparer<T> Create();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        internal static extern int Compare(ref T first, T second);
    }
    internal static class IntegerComparerFactory<T> where T : System.IComparable<T>
    {
        internal static System.Collections.Generic.Comparer<T> Create() => new System.Collections.Generic.GenericComparer<T>();
    }
}
