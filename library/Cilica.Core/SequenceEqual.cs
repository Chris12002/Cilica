// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned LINQ SequenceEqual and span comparison.
using System.Collections.Generic;
namespace System.Linq
{
    public static partial class Enumerable
    {
        public static bool SequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second)
            => SequenceEqual(first, second, null);
        public static bool SequenceEqual<T>(IEnumerable<T> first, IEnumerable<T> second, IEqualityComparer<T> comparer)
        {
            if (first == null) throw new ArgumentNullException("first");
            if (second == null) throw new ArgumentNullException("second");
            if (first is ICollection<T> firstCollection && second is ICollection<T> secondCollection)
            {
                T[] left = Cilica.Runtime.SequenceComparison<T>.Storage(first, out int leftCount);
                if (left != null)
                {
                    T[] right = Cilica.Runtime.SequenceComparison<T>.Storage(second, out int rightCount);
                    if (right != null)
                    {
                        if (leftCount != rightCount) return false;
                        for (int i = 0; i < leftCount; i++)
                            if (!Cilica.Runtime.SequenceComparison<T>.Equal(left[i], right[i], comparer)) return false;
                        return true;
                    }
                }
                if (firstCollection.Count != secondCollection.Count) return false;
                if (firstCollection is IList<T> firstList && secondCollection is IList<T> secondList)
                {
                    int count = firstCollection.Count;
                    for (int i = 0; i < count; i++)
                        if (!Cilica.Runtime.SequenceComparison<T>.Equal(firstList[i], secondList[i], comparer)) return false;
                    return true;
                }
            }
            using (IEnumerator<T> left = first.GetEnumerator())
            using (IEnumerator<T> right = second.GetEnumerator())
            {
                while (left.MoveNext())
                    if (!(right.MoveNext() && Cilica.Runtime.SequenceComparison<T>.Equal(left.Current, right.Current, comparer))) return false;
                return !right.MoveNext();
            }
        }
    }
}
namespace Cilica.Runtime
{
    internal static class SequenceComparison<T>
    {
        internal static T[] Storage(IEnumerable<T> source, out int count)
        {
            if (source is T[] array && array.GetType() == typeof(T[])) { count = array.Length; return array; }
            if (source is List<T> list && list.GetType() == typeof(List<T>)) return list.QueryStorage(out count);
            count = 0; return null;
        }
        internal static bool Equal(T left, T right, IEqualityComparer<T> comparer)
            => comparer == null ? KeyOperations<T>.Equal(ref left, right) : comparer.Equals(left, right);
    }
}
