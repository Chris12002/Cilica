// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from pinned String.IsNullOrWhiteSpace and Char.IsWhiteSpace.
namespace System
{
    public sealed partial class String
    {
        public static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null) return true;
            for (int i = 0; i < value.Length; i++)
            {
                if (!Cilica.Runtime.UnicodeWhitespace.Contains(value[i])) return false;
            }
            return true;
        }
    }
}
