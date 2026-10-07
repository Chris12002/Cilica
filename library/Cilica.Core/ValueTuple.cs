// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned ValueTuple<T1,T2>. See ../provenance.json.
namespace System.Collections
{
    public interface IEqualityComparer { bool Equals(object first, object second); int GetHashCode(object value); }
    public interface IComparer { int Compare(object first, object second); }
    public interface IStructuralEquatable { bool Equals(object other, IEqualityComparer comparer); int GetHashCode(IEqualityComparer comparer); }
    public interface IStructuralComparable { int CompareTo(object other, IComparer comparer); }
}
namespace System.Runtime.CompilerServices
{
    public interface ITuple { int Length { get; } object this[int index] { get; } }
}
namespace System
{
    public struct ValueTuple<T1, T2> : IEquatable<ValueTuple<T1, T2>>, Collections.IStructuralEquatable,
        Collections.IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2>>, Runtime.CompilerServices.ITuple
    {
        public T1 Item1;
        public T2 Item2;
        public ValueTuple(T1 item1, T2 item2) { Item1 = item1; Item2 = item2; }
        public bool Equals(ValueTuple<T1, T2> other)
        {
            // Default equality comparers receive values. A mutable struct's
            // equality method must not mutate the tuple's original field.
            T1 first = Item1;
            if (!Cilica.Runtime.KeyOperations<T1>.Equal(ref first, other.Item1)) return false;
            T2 second = Item2;
            return Cilica.Runtime.KeyOperations<T2>.Equal(ref second, other.Item2);
        }
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode()
        {
            // Unlike equality, constrained hashing targets the actual fields.
            int left = Cilica.Runtime.KeyOperations<T1>.IsNull(Item1) ? 0 : Cilica.Runtime.KeyOperations<T1>.Hash(ref Item1);
            int right = Cilica.Runtime.KeyOperations<T2>.IsNull(Item2) ? 0 : Cilica.Runtime.KeyOperations<T2>.Hash(ref Item2);
            return Cilica.Runtime.PairHash.Combine(left, right);
        }
        bool Collections.IStructuralEquatable.Equals(object other, Collections.IEqualityComparer comparer) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        int Collections.IStructuralEquatable.GetHashCode(Collections.IEqualityComparer comparer) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        int Collections.IStructuralComparable.CompareTo(object other, Collections.IComparer comparer) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        int IComparable.CompareTo(object other) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public int CompareTo(ValueTuple<T1, T2> other) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public override string ToString() => Cilica.Runtime.UnsupportedLibraryMember<string>.Invoke();
        int Runtime.CompilerServices.ITuple.Length => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        object Runtime.CompilerServices.ITuple.this[int index] => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
    }

    // Five-component storage and typed equality use the same generic value contract.
    public struct ValueTuple<T1, T2, T3, T4, T5> : IEquatable<ValueTuple<T1, T2, T3, T4, T5>>, Collections.IStructuralEquatable,
        Collections.IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4, T5>>, Runtime.CompilerServices.ITuple
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
        { Item1 = item1; Item2 = item2; Item3 = item3; Item4 = item4; Item5 = item5; }
        public bool Equals(ValueTuple<T1, T2, T3, T4, T5> other)
        {
            // Each default comparer receives its component by value. Read the
            // next field only after the previous comparison, preserving callbacks.
            T1 first = Item1;
            if (!Cilica.Runtime.KeyOperations<T1>.Equal(ref first, other.Item1)) return false;
            T2 second = Item2;
            if (!Cilica.Runtime.KeyOperations<T2>.Equal(ref second, other.Item2)) return false;
            T3 third = Item3;
            if (!Cilica.Runtime.KeyOperations<T3>.Equal(ref third, other.Item3)) return false;
            T4 fourth = Item4;
            if (!Cilica.Runtime.KeyOperations<T4>.Equal(ref fourth, other.Item4)) return false;
            T5 fifth = Item5;
            return Cilica.Runtime.KeyOperations<T5>.Equal(ref fifth, other.Item5);
        }
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        bool Collections.IStructuralEquatable.Equals(object other, Collections.IEqualityComparer comparer) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        int Collections.IStructuralEquatable.GetHashCode(Collections.IEqualityComparer comparer) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        int Collections.IStructuralComparable.CompareTo(object other, Collections.IComparer comparer) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        int IComparable.CompareTo(object other) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public int CompareTo(ValueTuple<T1, T2, T3, T4, T5> other) => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        public override string ToString() => Cilica.Runtime.UnsupportedLibraryMember<string>.Invoke();
        int Runtime.CompilerServices.ITuple.Length => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
        object Runtime.CompilerServices.ITuple.this[int index] => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
    }
}
