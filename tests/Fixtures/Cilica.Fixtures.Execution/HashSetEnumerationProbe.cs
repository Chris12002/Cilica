using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class HashSetEnumerationProbe
    {
        private static int Hash(string text) { int h = 17; for (int i = 0; i < text.Length; i++) h = unchecked(h * 31 + text[i]); return h; }
        private static HashSet<int> Make(int value)
        { var set = new HashSet<int>(); set.Add(value); set.Add(unchecked(value + 1)); set.Add(unchecked(value + 2)); return set; }
        public static int Concrete(int a, int b)
        {
            var set = Make(a); set.Remove(unchecked(a + 1)); set.Add(b);
            var e = set.GetEnumerator(); int result = e.Current; e.MoveNext();
            var copied = e; result = unchecked(result + e.Current * 7);
            while (e.MoveNext()) result = unchecked(result * 11 + e.Current);
            while (copied.MoveNext()) result = unchecked(result * 13 + copied.Current);
            e.Dispose(); copied.Dispose(); return unchecked(result + e.Current + copied.Current);
        }
        public static int Mutation(int a, int mode)
        {
            var set = Make(a); var e = set.GetEnumerator(); e.MoveNext(); int result = e.Current;
            if (mode == 1) set.Add(a);
            if (mode == 2) set.Remove(unchecked(a + 1));
            if (mode == 3) set.Clear();
            if (mode == 4) set.Add(unchecked(a + 3));
            if (mode == 5) { while (e.MoveNext()) { } set.Add(unchecked(a + 3)); }
            if (mode == 6) { e.Dispose(); set.Remove(a); }
            if (mode == 7) { set.Clear(); set.Add(a); }
            try { while (e.MoveNext()) result = unchecked(result * 7 + e.Current); }
            catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            e.Dispose(); return unchecked(result + e.Current);
        }
        public static int Interface(int a, int mode)
        {
            var set = Make(a); IEnumerator<int> e = ((IEnumerable<int>)set).GetEnumerator();
            int result = e.Current; e.MoveNext(); result = unchecked(result + e.Current);
            if (mode == 1) set.Add(a);
            if (mode == 2) set.Remove(unchecked(a + 1));
            if (mode == 3) set.Clear();
            if (mode == 4) set.Add(unchecked(a + 3));
            if (mode == 5) { while (e.MoveNext()) { } set.Add(unchecked(a + 3)); }
            if (mode == 6) ((IEnumerator)e).Reset();
            if (mode == 7) e.Dispose();
            try { while (e.MoveNext()) result = unchecked(result * 7 + e.Current); }
            catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            e.Dispose(); return unchecked(result + e.Current);
        }
        public static int CurrentAndReset(int a, int mode)
        {
            var set = new HashSet<string>(); set.Add("abc"); set.Add(null); set.Add("de");
            IEnumerator e = ((IEnumerable)set).GetEnumerator(); int result = 0;
            try { var value = e.Current; result = -101; } catch (InvalidOperationException error) { result = Hash(error.Message); }
            e.MoveNext(); result = unchecked(result + ((string)e.Current).Length);
            if (mode == 1) set.Clear();
            if (mode == 2) set.Add("f");
            if (mode == 3) set.Remove("de");
            try { e.Reset(); while (e.MoveNext()) { string value = (string)e.Current; result = unchecked(result * 7 + (value == null ? 11 : value.Length)); } }
            catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            try { string value = (string)e.Current; result = unchecked(result + (value == null ? 13 : value.Length)); }
            catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            ((IDisposable)e).Dispose(); return result;
        }
        public static int Empty(int a, int b)
        {
            var set = new HashSet<int>(); var e = ((IEnumerable<int>)set).GetEnumerator();
            var other = ((IEnumerable<int>)new HashSet<int>()).GetEnumerator(); var array = ((IEnumerable<int>)new int[0]).GetEnumerator();
            int result = e == other && e == array ? 7 : -101; set.Add(a);
            if (e.MoveNext() || other.MoveNext()) return -103;
            try { result += e.Current; } catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            ((IEnumerator)e).Reset(); e.Dispose(); other.Dispose(); array.Dispose(); return result;
        }
        public static int Values(int a, int b)
        {
            var trace = new KeyTrace(); var set = new HashSet<CollisionKey>();
            set.Add(new CollisionKey(a, trace)); set.Add(new CollisionKey(b, trace));
            var e = set.GetEnumerator(); e.MoveNext(); var first = e.Current; var copy = e;
            set.Clear(); var pressure = new KeyTrace(); pressure.Hashes = 73;
            int result = unchecked(first.Number + e.Current.Number + copy.Current.Trace.Hashes + pressure.Hashes);
            e.Dispose(); if (e.Current.Trace != trace) return -101;
            e.MoveNext(); copy.MoveNext(); return unchecked(result + (e.Current.Trace == null ? 7 : -103));
        }
        public static int Default(int a, int b)
        {
            HashSet<int>.Enumerator e = default; int result = e.Current;
            try { e.MoveNext(); } catch (NullReferenceException) { result += 7; }
            IEnumerator boxed = e;
            try { var value = boxed.Current; } catch (InvalidOperationException error) { result = unchecked(result + Hash(error.Message)); }
            try { boxed.Reset(); } catch (NullReferenceException) { result += 11; }
            e.Dispose(); return result;
        }
    }
}
