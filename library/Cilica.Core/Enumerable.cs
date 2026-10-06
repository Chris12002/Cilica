// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned AnyAll.cs and Count.cs; see provenance.
using System.Collections;
using System.Collections.Generic;
namespace System.Linq
{
    public static partial class Enumerable
    {
        public static bool Any<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is ICollection<TSource> generic) return generic.Count != 0;
            if (source is ICollection legacy) return legacy.Count != 0;
            using (IEnumerator<TSource> e = source.GetEnumerator()) return e.MoveNext();
        }
        public static bool Any<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is TSource[] array)
            {
                for (int index = 0; index < array.Length; index++) if (predicate(array[index])) return true;
            }
            else
            {
                foreach (TSource item in source) if (predicate(item)) return true;
            }
            return false;
        }
        public static bool All<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            if (source is TSource[] array)
            {
                for (int index = 0; index < array.Length; index++) if (!predicate(array[index])) return false;
            }
            else
            {
                foreach (TSource item in source) if (!predicate(item)) return false;
            }
            return true;
        }
        public static int Count<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            int count = 0;
            if (source is TSource[] array)
            {
                for (int index = 0; index < array.Length; index++) if (predicate(array[index])) count++;
            }
            else
            {
                foreach (TSource item in source) if (predicate(item)) count = checked(count + 1);
            }
            return count;
        }
        public static int Count<TSource>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source is ICollection<TSource> generic) return generic.Count;
            if (source is ICollection legacy) return legacy.Count;
            int count = 0;
            using (IEnumerator<TSource> e = source.GetEnumerator())
                while (e.MoveNext()) count = checked(count + 1);
            return count;
        }
    }
}
