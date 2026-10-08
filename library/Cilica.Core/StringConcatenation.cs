// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned String.Concat overloads using existing UTF-16 construction.
namespace System
{
    public sealed partial class String
    {
        public static string Join(string separator, Collections.Generic.IEnumerable<string> values)
        {
            if (values is Collections.Generic.List<string> list)
            {
                string[] storage = list.QueryStorage(out int count);
                return Cilica.Runtime.StringConcatenation.JoinStorage(separator, storage, count);
            }
            if (values is string[] array)
                return Cilica.Runtime.StringConcatenation.JoinStorage(separator, array, array.Length);
            if (values == null) throw new ArgumentNullException("values");
            using (Collections.Generic.IEnumerator<string> cursor = values.GetEnumerator())
            {
                if (!cursor.MoveNext()) return "";
                string first = cursor.Current;
                if (!cursor.MoveNext()) return first ?? "";
                var result = new Text.StringBuilder();
                result.Append(first);
                do
                {
                    Cilica.Runtime.StringConcatenation.Length((long)result.Length + (separator == null ? 0 : separator.Length));
                    result.Append(separator);
                    string next = cursor.Current;
                    Cilica.Runtime.StringConcatenation.Length((long)result.Length + (next == null ? 0 : next.Length));
                    result.Append(next);
                }
                while (cursor.MoveNext());
                return result.ToString();
            }
        }
        public static string Concat(string first, string second, string third)
        {
            if (IsNullOrEmpty(first)) return Concat(second, third);
            if (IsNullOrEmpty(second)) return Concat(first, third);
            if (IsNullOrEmpty(third)) return Concat(first, second);
            long total = (long)first.Length + second.Length + third.Length;
            var result = new int[Cilica.Runtime.StringConcatenation.Length(total)];
            int copied = Cilica.Runtime.StringConcatenation.Copy(result, 0, first);
            copied = Cilica.Runtime.StringConcatenation.Copy(result, copied, second);
            Cilica.Runtime.StringConcatenation.Copy(result, copied, third);
            return Cilica.Runtime.StringConstruction.FromCodeUnits(result);
        }
        public static string Concat(string first, string second, string third, string fourth)
        {
            if (IsNullOrEmpty(first)) return Concat(second, third, fourth);
            if (IsNullOrEmpty(second)) return Concat(first, third, fourth);
            if (IsNullOrEmpty(third)) return Concat(first, second, fourth);
            if (IsNullOrEmpty(fourth)) return Concat(first, second, third);
            long total = (long)first.Length + second.Length + third.Length + fourth.Length;
            var result = new int[Cilica.Runtime.StringConcatenation.Length(total)];
            int copied = Cilica.Runtime.StringConcatenation.Copy(result, 0, first);
            copied = Cilica.Runtime.StringConcatenation.Copy(result, copied, second);
            copied = Cilica.Runtime.StringConcatenation.Copy(result, copied, third);
            Cilica.Runtime.StringConcatenation.Copy(result, copied, fourth);
            return Cilica.Runtime.StringConstruction.FromCodeUnits(result);
        }
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
            int length = Cilica.Runtime.StringConcatenation.Length(total);
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
        internal static string JoinStorage(string separator, string[] values, int count)
        {
            if (count == 0) return "";
            if (count == 1) return values[0] ?? "";
            int separatorLength = separator == null ? 0 : separator.Length;
            long total = (long)(count - 1) * separatorLength;
            Length(total);
            for (int i = 0; i < count; ++i)
            {
                string value = values[i];
                if (value != null) { total += value.Length; Length(total); }
            }
            int length = (int)total;
            if (length == 0) return "";
            var result = new int[length];
            int copied = 0;
            for (int i = 0; i < count; ++i)
            {
                string value = values[i];
                if (value != null)
                {
                    if (value.Length > length - copied) return JoinSnapshot(separator, values, count);
                    copied = Copy(result, copied, value);
                }
                if (i < count - 1 && separatorLength != 0)
                {
                    if (separatorLength > length - copied) return JoinSnapshot(separator, values, count);
                    copied = Copy(result, copied, separator);
                }
            }
            return copied == length ? StringConstruction.FromCodeUnits(result) : JoinSnapshot(separator, values, count);
        }
        private static string JoinSnapshot(string separator, string[] values, int count)
        {
            var snapshot = new string[count];
            for (int i = 0; i < count; ++i) snapshot[i] = values[i];
            return JoinStorage(separator, snapshot, count);
        }
        internal static int Length(long total)
        {
            if (total > 2147483647L) throw new System.OutOfMemoryException("String length exceeded supported range.");
            return (int)total;
        }
        internal static int Copy(int[] destination, int offset, string value)
        {
            int length = value.Length;
            for (int i = 0; i < length; i++) destination[offset + i] = value[i];
            return offset + length;
        }
        internal static string Snapshot(string[] values)
        {
            int count = values.Length; var snapshot = new string[count];
            for (int i = 0; i < count; i++) snapshot[i] = values[i];
            return System.String.Concat(snapshot);
        }
    }
}
