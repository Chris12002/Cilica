using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class ArrayMutationProbe
    {
        public static int Integers(int input, int mode)
        {
            var array = new int[mode < 0 ? 0 : 2];
            IList<int> list = array; int result = array.Length;
            try { list.Insert(input, mode); }
            catch (NotSupportedException e) { result += e.Message == "Collection was of a fixed size." ? 11 : -100; }
            try { list.RemoveAt(input); }
            catch (NotSupportedException e) { result += e.Message == "Collection was of a fixed size." ? 13 : -100; }
            try { list.Clear(); }
            catch (NotSupportedException e) { result += e.Message == "Collection is read-only." ? 17 : -100; }
            return result + array.Length;
        }
        public static int References(int input, int mode)
        {
            var array = new[] { "abc", "d" }; IList<object> list = array; int result = 0;
            try { list.Insert(input, new object()); }
            catch (NotSupportedException e) { result += e.Message == "Collection was of a fixed size." ? 11 : -100; }
            try { list.RemoveAt(mode); }
            catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.Clear(); }
            catch (NotSupportedException e) { result += e.Message == "Collection is read-only." ? 17 : -100; }
            return result + array[0].Length + array[1].Length;
        }
        public static int Values(int input, int mode)
        {
            var array = new ValueVectorBucket<ValueVectorCell>[2];
            ValueVectorBucket<ValueVectorCell> value = default;
            value.Value.Number = input; value.Value.Wide = ((long)input << 32) + mode;
            value.Value.Reference = new ValueVectorBox(mode); value.Marker = mode; array[1] = value;
            IList<ValueVectorBucket<ValueVectorCell>> list = array; int result = 0;
            try { list.Insert(input, value); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.RemoveAt(mode); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.Clear(); } catch (NotSupportedException e) { result += e.Message.Length; }
            return unchecked(result + array[1].Value.Number + array[1].Value.Reference.Number + (int)(array[1].Value.Wide >> 32) + array[1].Marker);
        }
        public static int EmptyValues(int input, int mode)
        {
            var array = new ValueVectorEmpty[mode < 0 ? 0 : 2]; IList<ValueVectorEmpty> list = array; int result = 0;
            try { list.Insert(input, default); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.RemoveAt(mode); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.Clear(); } catch (NotSupportedException e) { result += e.Message.Length; }
            ((IList)array).Clear(); return result + array.Length;
        }
        public static int LegacyInsert(int input, int mode)
        {
            IList list = mode < 0 ? (IList)new int[0] : new ValueVectorCell[2]; int result = 0;
            try { list.Insert(input, "wrong element type"); }
            catch (NotSupportedException e) { result += e.Message == "Collection was of a fixed size." ? 11 : -100; }
            try { list.RemoveAt(mode); }
            catch (NotSupportedException e) { result += e.Message == "Collection was of a fixed size." ? 13 : -100; }
            return result + list.Count;
        }
        public static int ClearIntegers(int input, int mode)
        {
            var array = new[] { input, mode }; IList list = array; object identity = list.SyncRoot;
            list.Clear(); if (list.SyncRoot != identity || list != (object)array) return -100;
            int result = array[0] + array[1]; array[1] = input;
            list.Clear(); return result + array[1] + list.Count;
        }
        public static int ClearReferences(int input, int mode)
        {
            var array = new[] { new ValueVectorBox(input), new ValueVectorBox(mode) };
            var saved = array[0]; IList list = array; list.Clear();
            if (array[0] != null || array[1] != null) return -100;
            array[1] = new ValueVectorBox(mode); list.Clear();
            return unchecked(saved.Number + (array[1] == null ? list.Count : -100));
        }
        public static int ClearValues(int input, int mode)
        {
            var array = new ValueVectorBucket<ValueVectorCell>[2];
            ValueVectorBucket<ValueVectorCell> value = default;
            value.Value.Number = input; value.Value.Wide = ((long)input << 32) + mode;
            value.Value.Reference = new ValueVectorBox(mode); value.Marker = mode; array[1] = value;
            ((IList)array).Clear(); var cleared = array[1];
            if (cleared.Value.Reference != null || cleared.Value.Wide != 0) return -100;
            return unchecked(cleared.Value.Number + cleared.Marker + array.Length + value.Value.Reference.Number);
        }
        public static int ClearEmpty(int input, int mode)
        {
            IList list = mode < 0 ? (IList)new ValueVectorCell[0] : new object[0];
            object identity = list.SyncRoot; list.Clear(); list.Clear();
            return list.Count + (list.SyncRoot == identity ? 1 : -100);
        }
        public static int Nulls(int input, int mode)
        {
            IList<int> generic = null; IList legacy = null; int result = 0;
            try { generic.Insert(input, input / mode); }
            catch (DivideByZeroException) { result += 1; }
            catch (OverflowException) { result += 2; }
            catch (NullReferenceException) { result += 3; }
            try { legacy.Insert(input, new object()); } catch (NullReferenceException) { result += 5; }
            try { generic.RemoveAt(input); } catch (NullReferenceException) { result += 7; }
            try { legacy.RemoveAt(input); } catch (NullReferenceException) { result += 11; }
            try { generic.Clear(); } catch (NullReferenceException) { result += 13; }
            try { legacy.Clear(); } catch (NullReferenceException) { result += 17; }
            return result;
        }
        public static int MixedValues(int input, int mode)
        {
            var concrete = new List<ValueVectorCell>();
            ValueVectorCell value = default; value.Number = input; value.Reference = new ValueVectorBox(mode);
            concrete.Add(value);
            IList<ValueVectorCell> list = mode < 0 ? (IList<ValueVectorCell>)new ValueVectorCell[2] : concrete;
            int result = 0;
            try { list.Insert(0, value); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.RemoveAt(0); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { list.Clear(); } catch (NotSupportedException e) { result += e.Message.Length; }
            return result + list.Count;
        }
        public static ValueVectorBox[] CreateTracked() => new[] { new ValueVectorBox(7), new ValueVectorBox(9) };
        public static void ClearTracked(ValueVectorBox[] array) => ((IList)array).Clear();
        public static ValueVectorCell[] CreateTrackedValues()
        {
            var array = new ValueVectorCell[2]; ValueVectorCell value = default;
            value.Reference = new ValueVectorBox(7); array[0] = value;
            value.Reference = new ValueVectorBox(9); array[1] = value; return array;
        }
        public static void ClearTrackedValues(ValueVectorCell[] array) => ((IList)array).Clear();
        public static int Tracked(int input, int mode)
        {
            var array = CreateTracked(); ClearTracked(array); return array[0] == null && array[1] == null ? array.Length : -100;
        }
        public static int TrackedValues(int input, int mode)
        {
            var array = CreateTrackedValues(); ClearTrackedValues(array);
            return array[0].Reference == null && array[1].Reference == null ? array.Length : -100;
        }
    }
}
