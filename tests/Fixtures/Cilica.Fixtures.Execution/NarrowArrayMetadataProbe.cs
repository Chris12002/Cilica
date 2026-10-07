using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class NarrowArrayMetadataProbe
    {
        public static int Count(int a, int b)
        {
            ICollection bytes = new byte[3]; ICollection chars = new char[5];
            return unchecked(bytes.Count + chars.Count + a + b);
        }
        public static int GenericCount(int a, int b)
        {
            ICollection<byte> bytes = new byte[3]; IReadOnlyCollection<char> chars = new char[5];
            object alias = bytes;
            return unchecked(bytes.Count + chars.Count + (alias is ICollection<sbyte> ? 1 : 0) + a + b);
        }
        public static int Flags(int a, int b)
        {
            byte[] bytes = new byte[1]; char[] chars = new char[1];
            IList first = bytes; IList second = chars;
            ICollection<byte> generic = bytes;
            return unchecked((first.IsFixedSize ? 1 : 0) + (second.IsFixedSize ? 2 : 0)
                + (first.IsReadOnly ? 4 : 0) + (second.IsReadOnly ? 8 : 0)
                + (first.IsSynchronized ? 16 : 0) + (generic.IsReadOnly ? 32 : 0) + a + b);
        }
        public static int Identity(int a, int b)
        {
            byte[] bytes = new byte[1]; char[] chars = new char[1];
            ICollection first = bytes; ICollection second = chars;
            object saved = first.SyncRoot;
            for (int i = 0; i < 8; i++) { var garbage = new char[7]; garbage[0] = (char)i; }
            return unchecked((ReferenceEquals(saved, bytes) ? 1 : 0) + (ReferenceEquals(second.SyncRoot, chars) ? 2 : 0)
                + (ReferenceEquals(saved, second.SyncRoot) ? 4 : 0) + a + b);
        }
        public static int Null(int a, int b)
        {
            ICollection values = a < 0 ? null : new byte[3];
            try { return unchecked(values.Count + b); } catch (NullReferenceException) { return 19; }
        }
        public static int UnsupportedGet(int a,int b) { IList<char> values = new char[1]; return values[0]; }
        public static int UnsupportedCopy(int a,int b) { ICollection<byte> values = new byte[1]; values.CopyTo(new byte[1], 0); return a; }
        public static int UnsupportedEnumeration(int a,int b) { IEnumerable<char> values = new char[1]; return values.GetEnumerator().MoveNext() ? a : b; }
        public static int UnsupportedClear(int a,int b) { IList values = new byte[1]; values.Clear(); return a; }
        public static int UnsupportedSignedView(int a,int b) { ICollection<sbyte> values = (ICollection<sbyte>)(object)new byte[1]; return values.Count; }
    }
}
