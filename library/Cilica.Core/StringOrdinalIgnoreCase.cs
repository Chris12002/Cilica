// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Scalar adaptation of Ordinal.cs / OrdinalCasing.Icu.cs; see ../provenance.json.
namespace System
{
    internal sealed class OrdinalIgnoreCaseComparer : StringComparer
    {
        public OrdinalIgnoreCaseComparer() { }
        public override int Compare(string first, string second) => Cilica.Runtime.OrdinalIgnoreCaseText.Compare(first, second);
        public override bool Equals(string first, string second) => Cilica.Runtime.OrdinalIgnoreCaseText.Equal(first, second);
        public override int GetHashCode(string value)
        {
            if (value == null) throw new ArgumentNullException("obj");
            return Cilica.Runtime.OrdinalIgnoreCaseText.Hash(value);
        }
        public override bool Equals(object other) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public override int GetHashCode() => Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke();
    }
}
namespace Cilica.Runtime
{
    internal static class OrdinalIgnoreCaseComparerCache
    {
        public static readonly System.StringComparer Instance = new System.OrdinalIgnoreCaseComparer();
    }
}
