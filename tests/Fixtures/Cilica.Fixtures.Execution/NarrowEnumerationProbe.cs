using System;
using System.Collections;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public static class NarrowEnumerationProbe
    {
        public static int Bytes(int a, int b)
        {
            var array = new byte[3]; array[0] = (byte)a; array[1] = (byte)b; array[2] = 255;
            int result = 0;
            foreach (object value in (IEnumerable)array)
                result = unchecked(result * 31 + (value is byte item ? item : -1000));
            return result;
        }
        public static int Characters(int a, int b)
        {
            var array = new char[5]; array[0] = (char)a; array[1] = (char)b; array[2] = '\uffff'; array[3] = '\ud800'; array[4] = '\udfff';
            int result = 0;
            foreach (object value in (IEnumerable)array)
                result = unchecked(result * 31 + (value is char item ? item : -1000));
            return result;
        }
        public static int Booleans(int a, int b)
        {
            var array = new bool[3]; array[0] = a < 0; array[1] = b != 0; array[2] = true;
            int result = 0;
            foreach (object value in (IEnumerable)array)
                result = result * 3 + (value is bool item ? item ? 2 : 1 : 0);
            return result;
        }
        public static int FreshBoxes(int a, int b)
        {
            var array = new byte[1]; array[0] = (byte)a;
            var cursor = ((IEnumerable)array).GetEnumerator(); cursor.MoveNext();
            object first = cursor.Current; object second = cursor.Current;
            array[0] = (byte)b;
            object third = cursor.Current;
            return unchecked((ReferenceEquals(first, second) ? 10000 : 0) + (first is byte x ? x : -1000)
                + (third is byte y ? y * 257 : -2000) + (first is int ? 20000 : 0) + (first is char ? 40000 : 0));
        }
        public static int CursorState(int a, int b)
        {
            var array = new char[1]; array[0] = (char)a;
            var cursor = ((IEnumerable)array).GetEnumerator(); int result = 0;
            try { object value = cursor.Current; result = 1000; } catch (InvalidOperationException) { result = 1; }
            if (cursor.MoveNext()) result += cursor.Current is char c ? c : -1000;
            array[0] = (char)b;
            if (!cursor.MoveNext()) result += 2;
            if (!cursor.MoveNext()) result += 4;
            try { object value = cursor.Current; result += 1000; } catch (InvalidOperationException) { result += 8; }
            cursor.Reset(); if (cursor.MoveNext()) result += cursor.Current is char c ? c : -1000;
            return result;
        }
        public static int EmptyAndNull(int a, int b)
        {
            IEnumerable array = a < 0 ? null : new bool[0]; int result = 0;
            try
            {
                var cursor = array.GetEnumerator(); if (!cursor.MoveNext()) result = 1;
                try { object value = cursor.Current; result = 1000; } catch (InvalidOperationException) { result += 2; }
                cursor.Reset(); if (!cursor.MoveNext()) result += 4;
                result += cursor is IDisposable ? 100 : 0;
            }
            catch (NullReferenceException) { result = 19; }
            return result;
        }
        public static int Mixed(int a, int b)
        {
            var bytes = new byte[1]; bytes[0] = (byte)a;
            var chars = new char[1]; chars[0] = (char)b;
            var booleans = new bool[1]; booleans[0] = a < b;
            IEnumerable[] sources = new IEnumerable[3]; sources[0] = bytes; sources[1] = chars; sources[2] = booleans;
            int result = 0;
            foreach (var source in sources)
                foreach (object value in source)
                    result = unchecked(result + (value is byte x ? x : value is char y ? y : value is bool z ? z ? 17 : 19 : -1000));
            return result;
        }
        public static int UnsupportedTypedQuery(int a, int b) => ((IEnumerable)new byte[1]).OfType<byte>().Count();
        public static int UnsupportedSignedSource(int a, int b)
        {
            int count = 0; foreach (object value in (IEnumerable)new sbyte[1]) ++count; return count;
        }
        public static int UnsupportedUInt16Source(int a, int b)
        {
            int count = 0; foreach (object value in (IEnumerable)new ushort[1]) ++count; return count;
        }
        public static int QueryCount(int a, int b)
        {
            var array = new byte[2]; array[0] = (byte)a; array[1] = (byte)b;
            IEnumerable source = array;
            return source.OfType<object>().Count() * 100 + source.OfType<string>().Count() * 10;
        }
    }
}
