using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class DictionaryViewInterfaceProbe
    {
        public static int Order(int a, int b)
        {
            var d = new Dictionary<int, int>(); for (int i = 0; i < 10; i++) d.TryAdd(i, unchecked(a + i)); d.Remove(3); d.TryAdd(40, b);
            IEnumerable<int> keys = d.Keys; IEnumerable<int> values = d.Values; int result = 0;
            foreach (int key in keys) result = unchecked(result * 31 + key);
            foreach (int value in values) result = unchecked(result * 17 + value);
            var k = ((IEnumerable)d.Keys).GetEnumerator(); var v = ((IEnumerable)d.Values).GetEnumerator();
            while (k.MoveNext() && v.MoveNext()) result = unchecked(result + (int)k.Current * 7 + (int)v.Current * 11);
            return result;
        }
        private static int State(IEnumerator cursor)
        { try { var ignored = cursor.Current; return -100; } catch (InvalidOperationException e) { return e.Message == "Enumeration has either not started or has already finished." ? 7 : -101; } }
        public static int Current(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, a); d.TryAdd(2, b);
            var k = ((IEnumerable<int>)d.Keys).GetEnumerator(); var v = ((IEnumerable<int>)d.Values).GetEnumerator();
            int result = k.Current + v.Current + State((IEnumerator)k) + State((IEnumerator)v);
            k.MoveNext(); v.MoveNext(); object first = ((IEnumerator)v).Current; object second = ((IEnumerator)v).Current;
            result = unchecked(result + (int)first + (int)second + (ReferenceEquals(first, second) ? -100 : 11));
            k.Dispose(); v.Dispose(); result = unchecked(result + k.Current + v.Current);
            while (k.MoveNext()) { } while (v.MoveNext()) { }
            result = unchecked(result + k.Current + v.Current + State((IEnumerator)k) + State((IEnumerator)v));
            ((IEnumerator)k).Reset(); ((IEnumerator)v).Reset(); k.MoveNext(); v.MoveNext();
            return unchecked(result + k.Current + v.Current);
        }
        public static int Mutations(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, a); d.TryAdd(2, b); d.TryAdd(3, 17);
            var k = ((IEnumerable<int>)d.Keys).GetEnumerator(); var v = ((IEnumerable<int>)d.Values).GetEnumerator();
            k.MoveNext(); v.MoveNext(); int result = k.Current + v.Current;
            int mode = b & 7;
            if (mode == 0) d.Remove(2); else if (mode == 1) d.Clear(); else if (mode == 2) d[2] = a;
            else if (mode == 3) d.TryAdd(2, a); else if (mode == 4) d.TryAdd(4, a);
            else if (mode == 5) { d.Remove(2); d.TryAdd(4, a); } else if (mode == 6) d.Remove(40); else { while (k.MoveNext()) { } while (v.MoveNext()) { } d.Clear(); }
            try { while (k.MoveNext()) result = unchecked(result + k.Current); } catch (InvalidOperationException) { result = unchecked(result + 11); }
            try { while (v.MoveNext()) result = unchecked(result + v.Current); } catch (InvalidOperationException) { result = unchecked(result + 13); }
            try { ((IEnumerator)k).Reset(); if (k.MoveNext()) result = unchecked(result + k.Current); } catch (InvalidOperationException) { result = unchecked(result + 17); }
            try { ((IEnumerator)v).Reset(); if (v.MoveNext()) result = unchecked(result + v.Current); } catch (InvalidOperationException) { result = unchecked(result + 19); }
            return result;
        }
        public static int EmptyIdentity(int a, int b)
        {
            var d = new Dictionary<int, int>(); var keys = ((IEnumerable<int>)d.Keys).GetEnumerator(); var values = ((IEnumerable<int>)d.Values).GetEnumerator();
            var legacy = ((IEnumerable)d.Keys).GetEnumerator(); var array = ((IEnumerable<int>)new int[0]).GetEnumerator();
            int result = ReferenceEquals(keys, values) && ReferenceEquals(keys, legacy) && ReferenceEquals(keys, array) ? 7 : -100;
            ((IEnumerator)keys).Reset();
            try { result += keys.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration has not started. Call MoveNext." ? 11 : -101; }
            d.TryAdd(a, b); if (keys.MoveNext() || values.MoveNext()) return -102;
            try { result += (int)legacy.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration already finished." ? 13 : -103; }
            keys.Dispose(); var later = ((IEnumerable<int>)d.Keys).GetEnumerator(); later.MoveNext();
            return unchecked(result + later.Current);
        }
        public static int Defaults(int a, int b)
        {
            IEnumerator<int> keys = default(Dictionary<int, int>.KeyCollection.Enumerator);
            IEnumerator<int> values = default(Dictionary<int, int>.ValueCollection.Enumerator);
            int result = keys.Current + values.Current + State((IEnumerator)keys) + State((IEnumerator)values);
            try { keys.MoveNext(); } catch (NullReferenceException) { result += 11; }
            try { values.MoveNext(); } catch (NullReferenceException) { result += 13; }
            try { ((IEnumerator)keys).Reset(); } catch (NullReferenceException) { result += 17; }
            try { ((IEnumerator)values).Reset(); } catch (NullReferenceException) { result += 19; }
            keys.Dispose(); values.Dispose(); return unchecked(result + a + b);
        }
        public static int Copies(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, a); d.TryAdd(2, b);
            var k = ((IEnumerable<int>)d.Keys).GetEnumerator(); var v = ((IEnumerable<int>)d.Values).GetEnumerator();
            k.MoveNext(); v.MoveNext(); var kc = (Dictionary<int, int>.KeyCollection.Enumerator)k; var vc = (Dictionary<int, int>.ValueCollection.Enumerator)v;
            k.MoveNext(); v.MoveNext(); int result = unchecked(k.Current + v.Current * 3 + kc.Current * 5 + vc.Current * 7);
            var other = ((IEnumerable<int>)d.Keys).GetEnumerator();
            return unchecked(result + (ReferenceEquals(k, other) ? -100 : 11));
        }
        public static int References(int a, int b)
        {
            var d = new Dictionary<string, ValueVectorBox>(); var node = new ValueVectorBox(a); string key = string.Concat("key", "copy"); d.TryAdd(key, node);
            var k = ((IEnumerable<string>)d.Keys).GetEnumerator(); var v = ((IEnumerable<ValueVectorBox>)d.Values).GetEnumerator();
            k.MoveNext(); v.MoveNext(); object original = ((IEnumerator)v).Current; d.Clear(); node.Number = b;
            string pressure = string.Concat("cursor", "root");
            return unchecked(k.Current.Length + v.Current.Number + pressure.Length + (ReferenceEquals(original, node) ? 7 : -100));
        }
        public static int Values(int a, int b)
        {
            var d = new Dictionary<int, ValueVectorCell>(); ValueVectorCell value = default; value.Number = a; value.Reference = new ValueVectorBox(b); d.TryAdd(1, value);
            var cursor = ((IEnumerable<ValueVectorCell>)d.Values).GetEnumerator(); cursor.MoveNext();
            var first = (ValueVectorCell)((IEnumerator)cursor).Current; var second = (ValueVectorCell)((IEnumerator)cursor).Current;
            first.Number = b; first.Reference.Number = a; d.Clear(); string pressure = string.Concat("value", "cursor");
            return unchecked(first.Number + second.Number * 3 + cursor.Current.Reference.Number * 5 + pressure.Length);
        }
        public static int Nullable(int a, int b)
        {
            var d = new Dictionary<int, int?>(); d.TryAdd(1, a); d.TryAdd(2, null); d.TryAdd(3, b);
            var cursor = ((IEnumerable<int?>)d.Values).GetEnumerator(); var legacy = (IEnumerator)cursor; int result = 0;
            while (cursor.MoveNext()) { object value = legacy.Current; result = unchecked(result + (value == null ? 7 : (int)value)); }
            return result;
        }
        public static int PairOrder(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, a); d.TryAdd(2, b); d.TryAdd(3, 17); d.Remove(2); d.TryAdd(4, b);
            int result = 0; foreach (var pair in (IEnumerable<KeyValuePair<int, int>>)d) result = unchecked(result * 31 + pair.Key + pair.Value);
            var cursor = ((IEnumerable)d).GetEnumerator(); while (cursor.MoveNext()) { var pair = (KeyValuePair<int, int>)cursor.Current; result = unchecked(result * 17 + pair.Key + pair.Value); }
            return result;
        }
        public static int PairStates(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, a); d.TryAdd(2, b);
            var cursor = ((IEnumerable<KeyValuePair<int, int>>)d).GetEnumerator(); var legacy = (IEnumerator)cursor;
            int result = cursor.Current.Value + State(legacy); cursor.MoveNext(); object first = legacy.Current; object second = legacy.Current;
            result = unchecked(result + (ReferenceEquals(first, second) ? -100 : 7));
            var copy = (Dictionary<int, int>.Enumerator)cursor; cursor.MoveNext();
            result = unchecked(result + copy.Current.Value + cursor.Current.Value * 3);
            cursor.Dispose(); while (cursor.MoveNext()) { } result = unchecked(result + cursor.Current.Value + State(legacy));
            legacy.Reset(); cursor.MoveNext(); result = unchecked(result + cursor.Current.Value);
            IEnumerator<KeyValuePair<int, int>> missing = default(Dictionary<int, int>.Enumerator);
            result = unchecked(result + State((IEnumerator)missing));
            try { missing.MoveNext(); } catch (NullReferenceException) { result = unchecked(result + 11); }
            return result;
        }
        public static int PairReferences(int a, int b)
        {
            var d = new Dictionary<string, ValueVectorCell>(); ValueVectorCell value = default; value.Number = a; value.Reference = new ValueVectorBox(b);
            string key = string.Concat("pair", "key"); d.TryAdd(key, value);
            var cursor = ((IEnumerable<KeyValuePair<string, ValueVectorCell>>)d).GetEnumerator(); cursor.MoveNext();
            var first = (KeyValuePair<string, ValueVectorCell>)((IEnumerator)cursor).Current; var copied = first.Value; copied.Number = b; copied.Reference.Number = a;
            d.Clear(); string pressure = string.Concat("retained", "pair");
            return unchecked(cursor.Current.Value.Number + first.Value.Number * 3 + cursor.Current.Value.Reference.Number * 5 + copied.Number * 7 + pressure.Length + cursor.Current.Key.Length);
        }
        public static int PairEmpty(int a, int b)
        {
            var d = new Dictionary<int, int>(); var cursor = ((IEnumerable<KeyValuePair<int, int>>)d).GetEnumerator(); var legacy = ((IEnumerable)d).GetEnumerator();
            var array = ((IEnumerable<KeyValuePair<int, int>>)new KeyValuePair<int, int>[0]).GetEnumerator();
            var other = ((IEnumerable<KeyValuePair<int, int>>)new Dictionary<int, int>()).GetEnumerator();
            int result = ReferenceEquals(cursor, legacy) && ReferenceEquals(cursor, other) && !ReferenceEquals(cursor, array) ? 7 : -100;
            ((IEnumerator)cursor).Reset(); d.TryAdd(a, b); if (cursor.MoveNext()) return -101;
            try { var ignored = cursor.Current; } catch (InvalidOperationException e) { result += e.Message == "Enumeration has not started. Call MoveNext." ? 11 : -102; }
            return result;
        }
        public static int UnsupportedDictionaryEntry(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(a, b); var cursor = ((IDictionary)d).GetEnumerator(); cursor.MoveNext(); return (int)cursor.Key;
        }
        public static int UnsupportedEntryView(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(a, b); var cursor = (IDictionaryEnumerator)((IEnumerable<KeyValuePair<int, int>>)d).GetEnumerator(); cursor.MoveNext(); return (int)cursor.Entry.Value;
        }
        public static int ClearCurrent(int a, int b)
        {
            var d = new Dictionary<int, int>(); d.TryAdd(1, a); d.TryAdd(2, b);
            var k = ((IEnumerable<int>)d.Keys).GetEnumerator(); var v = ((IEnumerable<int>)d.Values).GetEnumerator();
            k.MoveNext(); v.MoveNext(); d.Clear(); return unchecked(k.Current + v.Current + State((IEnumerator)k) + State((IEnumerator)v));
        }
    }
}
