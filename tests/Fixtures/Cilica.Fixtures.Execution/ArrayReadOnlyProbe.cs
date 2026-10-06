using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Cilica.Fixtures.Execution
{
    public static class ArrayReadOnlyProbe
    {
        public static int Live(int input, int mode)
        {
            var array = new[] { input, mode }; var view = Array.AsReadOnly(array);
            var other = Array.AsReadOnly(array);
            if (view == other || ((ICollection)view).SyncRoot != array) return -100;
            array[0] = unchecked(mode + 1);
            return unchecked(view[0] + view[1] + view.Count + (view.Contains(mode) ? 3 : 0) + view.IndexOf(mode));
        }
        public static int Empty(int input, int mode)
        {
            var first = Array.AsReadOnly(new int[0]); var second = Array.AsReadOnly(Array.Empty<int>());
            var separate = new ReadOnlyCollection<int>(new int[0]);
            var reference = Array.AsReadOnly(new string[0]);
            if (first != second || first == separate || (object)first == reference) return -100;
            if (((ICollection)first).SyncRoot != Array.Empty<int>()) return -101;
            return first.Count + reference.Count + (first.Contains(input) ? -100 : 7) + first.IndexOf(mode);
        }
        public static int Reference(int input, int mode)
        {
            var array = new[] { new ValueVectorBox(input), new ValueVectorBox(mode) };
            var view = Array.AsReadOnly(array); var old = view[0];
            array[0] = new ValueVectorBox(mode); array[1] = null;
            if (view[1] != null || old == view[0]) return -100;
            return unchecked(old.Number + view[0].Number);
        }
        public static int Value(int input, int mode)
        {
            var array = new ValueVectorCell[1]; ValueVectorCell initial = default;
            initial.Number = input; initial.Reference = new ValueVectorBox(mode); array[0] = initial;
            var view = Array.AsReadOnly(array); var copy = view[0];
            initial.Number = mode; initial.Reference = new ValueVectorBox(input); array[0] = initial;
            return unchecked(copy.Number + copy.Reference.Number + view[0].Number + view[0].Reference.Number);
        }
        public static int Faults(int input, int mode)
        {
            int result = 0;
            try { Array.AsReadOnly<int>(null); } catch (ArgumentNullException e) { result += e.ParamName == "array" ? 11 : -100; }
            IList<int> view = Array.AsReadOnly(mode < 0 ? new int[0] : new int[1]);
            try { view[input] = mode; } catch (NotSupportedException e) { result += e.Message == "Collection is read-only." ? 13 : -100; }
            try { view.Remove(input); } catch (NotSupportedException e) { result += e.Message.Length; }
            try { result += view[input]; } catch (ArgumentOutOfRangeException e) { result += e.ParamName == "index" ? 17 : -100; }
            return result;
        }
        public static int Covariant(int input, int mode)
        {
            string[] array = new[] { "a", "bc" }; object[] widened = array;
            var view = Array.AsReadOnly(widened); array[0] = "def";
            if (view[0] != (object)array[0] || ((ICollection)view).SyncRoot != array) return -100;
            IList<object> list = view;
            try { list[mode] = new object(); } catch (NotSupportedException e) { return e.Message.Length + view.Count; }
            return -101;
        }
    }
}
