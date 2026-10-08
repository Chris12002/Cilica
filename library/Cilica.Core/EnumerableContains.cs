// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded default-equality Contains adaptation; see provenance.
using System;
using System.Collections.Generic;
namespace System.Linq
{
    public static partial class Enumerable
    {
        public static bool Contains<T>(IEnumerable<T> source, T value, IEqualityComparer<T> comparer)
        {
            if (source == null) throw new ArgumentNullException("source");
            T[] storage = Cilica.Runtime.SequenceComparison<T>.Storage(source, out int count);
            if (storage != null)
            {
                for (int index = 0; index < count; ++index)
                    if (Cilica.Runtime.SequenceComparison<T>.Equal(storage[index], value, comparer)) return true;
                return false;
            }
            using (IEnumerator<T> cursor = source.GetEnumerator())
            {
                while (cursor.MoveNext())
                    if (Cilica.Runtime.SequenceComparison<T>.Equal(cursor.Current, value, comparer)) return true;
                return false;
            }
        }

        public static bool Contains<T>(IEnumerable<T> source, T value)
        {
            if (source is ICollection<T> collection) return collection.Contains(value);
            if (source is Cilica.Runtime.IQueryContains<T> query) return query.Contains(value);
            if (source == null) throw new ArgumentNullException("source");
            return Cilica.Runtime.QueryContains<T>.Iterate(source, value);
        }
    }
}
namespace Cilica.Runtime
{
    public interface IQueryContains<T> { bool Contains(T value); }
    // Array/list/Where and selected OfType projection specializations search
    // independently of their query cursor. Other iterators use ordinary traversal.
    public interface IQueryContainsTraversal<T> { IEnumerator<T> GetContainsEnumerator(); }
    internal static class QueryContains<T>
    {
        internal static bool Iterate(IEnumerable<T> source, T value)
        { using (IEnumerator<T> e = source.GetEnumerator()) return Read(e, value); }
        internal static bool Read(IEnumerator<T> e, T value)
        {
            while (e.MoveNext()) { T item = e.Current; if (KeyOperations<T>.Equal(ref item, value)) return true; }
            return false;
        }
    }
}
