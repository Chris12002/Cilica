// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned String.Concat(string[]) using existing UTF-16 construction.
namespace System
{
    public sealed partial class String
    {
        public static string Concat(string[] values)
        {
            if (values == null) throw new ArgumentNullException("values");
            int count = values.Length;
            if (count == 0) return "";
            if (count == 1) return values[0] ?? "";
            long total = 0;
            for (int i = 0; i < count; i++)
            {
                string value = values[i]; if (value != null) total += value.Length;
            }
            if (total > 2147483647L) throw new OutOfMemoryException("String length exceeded supported range.");
            int length = (int)total;
            if (length == 0) return "";
            var result = new int[length];
            int copied = 0;
            for (int i = 0; i < count; i++)
            {
                string value = values[i];
                if (value != null)
                {
                    int size = value.Length;
                    if (size > length - copied) return Cilica.Runtime.StringConcatenation.Snapshot(values);
                    for (int j = 0; j < size; j++) result[copied + j] = value[j];
                    copied += size;
                }
            }
            return copied == length ? Cilica.Runtime.StringConstruction.FromCodeUnits(result) : Cilica.Runtime.StringConcatenation.Snapshot(values);
        }
    }
}
namespace Cilica.Runtime
{
    internal static class StringConcatenation
    {
        internal static string Snapshot(string[] values)
        {
            int count = values.Length; var snapshot = new string[count];
            for (int i = 0; i < count; i++) snapshot[i] = values[i];
            return System.String.Concat(snapshot);
        }
    }
}
