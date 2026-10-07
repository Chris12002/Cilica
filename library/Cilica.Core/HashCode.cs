// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned HashCode.Combine<T1,T2>; see ../provenance.json.
namespace System
{
    public struct HashCode
    {
        public static int Combine<T1,T2>(T1 value1,T2 value2)
        {
            // Generic arguments arrive by value. Hash in declaration order, and
            // borrow only these local copies if a value override mutates its receiver.
            int first = Cilica.Runtime.KeyOperations<T1>.IsNull(value1) ? 0 : Cilica.Runtime.KeyOperations<T1>.Hash(ref value1);
            int second = Cilica.Runtime.KeyOperations<T2>.IsNull(value2) ? 0 : Cilica.Runtime.KeyOperations<T2>.Hash(ref value2);
            return Cilica.Runtime.PairHash.Combine(first, second);
        }
    }
}
