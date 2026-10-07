using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class EnumArrayInterfaceProbe
    {
        public static int Reads(int a, int b)
        {
            IList<ArrayEnum> values = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b };
            values[0] = (ArrayEnum)b;
            return unchecked((int)values[0] + (int)((IReadOnlyList<ArrayEnum>)values)[1] + values.Count);
        }
        public static int Aliases(int a, int b)
        {
            object array = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b };
            var integers = (IList<int>)array; var other = (IList<OtherArrayEnum>)array;
            integers[0] = b; other[1] = (OtherArrayEnum)a;
            return unchecked((int)other[0] + integers[1] + (array is IReadOnlyList<OtherArrayEnum> ? 7 : -100));
        }
        public static int IntegerArray(int a, int b)
        {
            object array = new int[] { a, b }; var values = (IList<ArrayEnum>)array;
            values[1] = (ArrayEnum)a;
            return unchecked((int)values[0] + (int)values[1] + (array is IReadOnlyList<ArrayEnum> ? 13 : -100));
        }
        public static int Search(int a, int b)
        {
            IList<ArrayEnum> values = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b, (ArrayEnum)a };
            return values.IndexOf((ArrayEnum)b) + (values.Contains((ArrayEnum)a) ? 17 : -100);
        }
        public static int Copy(int a, int b)
        {
            ICollection<ArrayEnum> source = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b };
            var destination = (ArrayEnum[])(object)new OtherArrayEnum[3]; source.CopyTo(destination, 1);
            return unchecked((int)destination[1] + (int)destination[2] + (source.IsReadOnly ? 19 : -100));
        }
        public static int GenericBoxes(int a, int b)
        {
            var array = new int[] { a, b };
            var e = ((IEnumerable<ArrayEnum>)(object)array).GetEnumerator(); e.MoveNext();
            object box = ((IEnumerator)e).Current;
            return unchecked((int)e.Current + (int)(ArrayEnum)box + (box is ArrayEnum ? 23 : -100) + (box is int ? -100 : 29));
        }
        public static int IntegerBoxes(int a, int b)
        {
            var array = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b };
            var e = ((IEnumerable<int>)(object)array).GetEnumerator(); e.MoveNext();
            object box = ((IEnumerator)e).Current;
            return unchecked(e.Current + (int)box + (box is int ? 31 : -100) + (box is ArrayEnum ? -100 : 37));
        }
        public static int ActualBoxes(int a, int b)
        {
            var array = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b };
            var view = (IEnumerable<OtherArrayEnum>)(object)array;
            var generic = view.GetEnumerator(); generic.MoveNext();
            var actual = ((IEnumerable)view).GetEnumerator(); actual.MoveNext();
            object first = ((IEnumerator)generic).Current; object second = actual.Current;
            return unchecked((int)(OtherArrayEnum)first + (int)(ArrayEnum)second
                + (first is OtherArrayEnum ? 41 : -100) + (second is ArrayEnum ? 43 : -100));
        }
        public static int States(int a, int b)
        {
            var values = new ArrayEnum[] { (ArrayEnum)a }; var e = ((IEnumerable<ArrayEnum>)values).GetEnumerator(); int result = 0;
            try { return (int)e.Current; } catch (InvalidOperationException) { result += 1; }
            e.MoveNext(); values[0] = (ArrayEnum)b; result = unchecked(result + (int)e.Current);
            if (e.MoveNext() || e.MoveNext()) return -101;
            try { return (int)e.Current; } catch (InvalidOperationException) { result += 3; }
            ((IEnumerator)e).Reset(); e.MoveNext(); return unchecked(result + (int)e.Current);
        }
        public static int Faults(int a, int b)
        {
            IList<ArrayEnum> values = a < 0 ? null : new ArrayEnum[1];
            try { values[b] = (ArrayEnum)a; values.Add((ArrayEnum)b); return -100; }
            catch (NullReferenceException) { return 1; }
            catch (ArgumentOutOfRangeException) { return 3; }
            catch (NotSupportedException) { return 7; }
        }
        public static int Clear(int a, int b)
        {
            var values = new ArrayEnum[] { (ArrayEnum)a, (ArrayEnum)b }; ((IList)values).Clear();
            return unchecked((int)values[0] + (int)values[1]);
        }
        public static int UnsupportedObjectAlias(int a, int b)
        {
            object array = new int[] { a, b }; var cursor = ((IEnumerable<ArrayEnum>)array).GetEnumerator();
            cursor.MoveNext(); return (int)(ArrayEnum)((IEnumerator)cursor).Current;
        }
        public static int Empty(int a, int b)
        {
            var one = ((IEnumerable<ArrayEnum>)new ArrayEnum[0]).GetEnumerator();
            var alias = ((IEnumerable<ArrayEnum>)(object)new int[0]).GetEnumerator();
            var other = ((IEnumerable<OtherArrayEnum>)(object)new ArrayEnum[0]).GetEnumerator();
            return (ReferenceEquals(one, alias) ? 1 : -100) + (ReferenceEquals(one, other) ? -100 : 3) + (one.MoveNext() ? -100 : 7);
        }
    }
}
