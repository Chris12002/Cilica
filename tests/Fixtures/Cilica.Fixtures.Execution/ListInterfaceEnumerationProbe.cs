using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ListInterfaceEnumerationProbe
    {
        public static IEnumerator<ValueVectorBox> CreateTracked()
        {
            var list = new List<ValueVectorBox>(); list.Add(new ValueVectorBox(17));
            return ((IEnumerable<ValueVectorBox>)list).GetEnumerator();
        }
        public static void EndTracked(IEnumerator<ValueVectorBox> cursor) { cursor.MoveNext(); cursor.MoveNext(); cursor.Dispose(); }
        public static int Tracked(int input, int mode) { var cursor = CreateTracked(); EndTracked(cursor); return input; }
        public static IEnumerator<ValueVectorBox> CreateEmptyTracked() => ((IEnumerable<ValueVectorBox>)new List<ValueVectorBox>()).GetEnumerator();
        public static void EndEmptyTracked(IEnumerator<ValueVectorBox> cursor) { cursor.MoveNext(); cursor.Dispose(); ((IEnumerator)cursor).Reset(); }
        public static int EmptyTracked(int input, int mode) { var cursor = CreateEmptyTracked(); EndEmptyTracked(cursor); return input; }
        public static IEnumerator<ValueVectorBox> CreateCurrentTracked()
        {
            var list = new List<ValueVectorBox>(); list.Add(new ValueVectorBox(17));
            var cursor = ((IEnumerable<ValueVectorBox>)list).GetEnumerator(); cursor.MoveNext(); list.Clear(); return cursor;
        }
        public static int ReadTracked(IEnumerator<ValueVectorBox> cursor) => cursor.Current.Number;
        public static int CurrentTracked(int input, int mode) { var cursor = CreateCurrentTracked(); return ReadTracked(cursor); }
        public static int Integers(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode); list.Add(19);
            IEnumerable<int> sequence = list; int result = 0;
            foreach (int value in sequence) result = unchecked(result * 31 + value);
            var legacy = ((IEnumerable)list).GetEnumerator();
            while (legacy.MoveNext()) result = unchecked(result * 17 + (int)legacy.Current);
            return result;
        }
        public static int Identity(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var first = ((IEnumerable<int>)list).GetEnumerator(); var second = ((IEnumerable<int>)list).GetEnumerator();
            var legacy = ((IEnumerable)list).GetEnumerator(); first.MoveNext();
            var copied = (List<int>.Enumerator)first;
            int result = first.GetType() == ((object)list.GetEnumerator()).GetType() ? 13 : -100;
            if ((object)first == second || (object)first == legacy) return -101;
            first.MoveNext(); copied.MoveNext(); second.MoveNext();
            return unchecked(result + first.Current * 31 + copied.Current * 17 + second.Current + list.Count);
        }
        private static int StateFault(IEnumerator cursor)
        {
            try { var ignored = cursor.Current; return -100; }
            catch (InvalidOperationException e) { return e.Message == "Enumeration has either not started or has already finished." ? 1 : -101; }
        }
        public static int Current(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var cursor = ((IEnumerable<int>)list).GetEnumerator(); var legacy = (IEnumerator)cursor;
            int result = cursor.Current + StateFault(legacy); cursor.MoveNext();
            object first = legacy.Current; object second = legacy.Current;
            result = unchecked(result + (int)first + (int)second + ((object)first == second ? -100 : 7));
            cursor.Dispose(); result = unchecked(result + cursor.Current);
            while (cursor.MoveNext()) { }
            result = unchecked(result + cursor.Current + StateFault(legacy));
            legacy.Reset(); result = unchecked(result + StateFault(legacy)); cursor.MoveNext();
            return unchecked(result + cursor.Current);
        }
        public static int Mutation(int input, int mode)
        {
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var cursor = ((IEnumerable<int>)list).GetEnumerator(); var legacy = (IEnumerator)cursor;
            cursor.MoveNext(); int result = cursor.Current;
            int kind = mode & 7;
            if (kind == 0) list.Add(8);
            else if (kind == 1) list[0] = 9;
            else if (kind == 2) list.RemoveAt(0);
            else if (kind == 3) list.Clear();
            else if (kind == 4) { while (cursor.MoveNext()) { } list.Add(11); }
            else if (kind == 5) list.Capacity = 17;
            else if (kind == 6) list.Remove(12345);
            else { list.Insert(0, 11); }
            result = unchecked(result + cursor.Current);
            try { if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException e) { result = unchecked(result + (e.Message == "Collection was modified; enumeration operation may not execute." ? 29 : -100)); }
            try { legacy.Reset(); if (cursor.MoveNext()) result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException e) { result = unchecked(result + (e.Message == "Collection was modified; enumeration operation may not execute." ? 43 : -101)); }
            return result;
        }
        public static int References(int input, int mode)
        {
            var list = new List<ValueVectorBox>(); var node = new ValueVectorBox(input); list.Add(node); list.Add(new ValueVectorBox(mode));
            var cursor = ((IEnumerable<ValueVectorBox>)list).GetEnumerator(); var legacy = (IEnumerator)cursor;
            if (cursor.Current != null) return -100; cursor.MoveNext();
            object first = legacy.Current; object second = legacy.Current;
            node.Number = unchecked(node.Number + 1);
            int result = (first == second && first == node ? 31 : -101) + cursor.Current.Number;
            list.Clear(); string pressure = string.Concat("collection", "pressure");
            result = unchecked(result + cursor.Current.Number + pressure.Length);
            return result;
        }
        public static int Values(int input, int mode)
        {
            var list = new List<BoxPair>(); var item = new BoxPair(); item.Number = input; item.Wide = (long)mode << 32; item.Text = "value"; list.Add(item);
            var cursor = ((IEnumerable<BoxPair>)list).GetEnumerator(); var legacy = (IEnumerator)cursor;
            cursor.MoveNext(); object first = legacy.Current; object second = legacy.Current;
            var copy = (BoxPair)first; copy.Number = mode; copy.Text = "changed";
            string pressure = string.Concat(copy.Text, "allocation");
            return unchecked(cursor.Current.Number + ((BoxPair)second).Number + (int)(cursor.Current.Wide >> 32)
                + cursor.Current.Text.Length + pressure.Length + (first == second ? -100 : 23));
        }
        public static int EmptyCache(int input, int mode)
        {
            var list = new List<int>(); var other = new List<int>();
            var first = ((IEnumerable<int>)list).GetEnumerator(); var second = ((IEnumerable<int>)other).GetEnumerator();
            var array = ((IEnumerable<int>)Array.Empty<int>()).GetEnumerator(); var legacy = ((IEnumerable)list).GetEnumerator();
            ((IEnumerator)first).Reset(); int result = first == second && first == array && (object)first == legacy ? 17 : -100;
            try { result += first.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration has not started. Call MoveNext." ? 3 : -101; }
            list.Add(input); other.Add(mode);
            if (first.MoveNext() || second.MoveNext()) return -102;
            try { result += (int)legacy.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration already finished." ? 5 : -103; }
            first.Dispose(); legacy.Reset();
            var later = ((IEnumerable<int>)list).GetEnumerator(); later.MoveNext();
            return unchecked(result + later.Current + (later.GetType() == first.GetType() ? -104 : 11));
        }
        public static int DefaultAndEmptyValue(int input, int mode)
        {
            IEnumerator<int> missing = default(List<int>.Enumerator); var legacy = (IEnumerator)missing;
            int result = missing.Current + StateFault(legacy);
            try { missing.MoveNext(); } catch (NullReferenceException) { result += 3; }
            try { legacy.Reset(); } catch (NullReferenceException) { result += 7; }
            IEnumerator<int> concrete = new List<int>().GetEnumerator();
            result += StateFault((IEnumerator)concrete); if (concrete.MoveNext()) return -100;
            result += StateFault((IEnumerator)concrete); concrete.Dispose();
            return result;
        }
        public static int NullAndFinally(int input, int mode)
        {
            IEnumerable<int> missing = null; int result = 0;
            try { missing.GetEnumerator(); } catch (NullReferenceException) { result += 7; }
            var list = new List<int>(); list.Add(input); list.Add(mode);
            var cursor = ((IEnumerable<int>)list).GetEnumerator();
            try { cursor.MoveNext(); if (mode < 0) throw new InvalidOperationException(); result = unchecked(result + cursor.Current); }
            catch (InvalidOperationException) { result = unchecked(result + 11); }
            finally { cursor.Dispose(); }
            cursor.MoveNext(); return unchecked(result + cursor.Current);
        }
    }
}
