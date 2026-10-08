// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned CollectionExtensions; see library/provenance.json.
namespace System.Collections.Generic
{
    public static class CollectionExtensions
    {
        public static TValue GetValueOrDefault<TKey,TValue>(IReadOnlyDictionary<TKey,TValue> dictionary,TKey key)
            => GetValueOrDefault(dictionary,key,default(TValue));

        public static TValue GetValueOrDefault<TKey,TValue>(IReadOnlyDictionary<TKey,TValue> dictionary,TKey key,TValue defaultValue)
        {
            if (dictionary == null) throw new ArgumentNullException("dictionary");
            return dictionary.TryGetValue(key,out TValue value) ? value : defaultValue;
        }
    }
}
