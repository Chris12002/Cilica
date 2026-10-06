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
