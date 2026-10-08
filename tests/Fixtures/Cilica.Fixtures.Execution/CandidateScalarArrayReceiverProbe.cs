using System;
namespace Cilica.Fixtures.Execution
{
    // Independent scalar receiver reductions, not retained candidate source.
    public static class CandidateScalarArrayReceiverProbe
    {
        public static int EqualsElement(int first, int second)
        {
            var values = new int[] { first, second };
            return values[0].Equals(values[1]) ? 1 : 0;
        }
        public static int EqualsAfterArgumentMutation(int first, int second)
        {
            var values = new int[] { first };
            return values[0].Equals(Replace(values, second)) ? 1 : 0;
        }
        public enum Number { Zero, One }
        public static int ReplaceArrayAndCopy(int first, int second)
        {
            var values = new int[] { first };
            ref int slot = ref values[0];
            int copy = slot;
            values = new int[] { second };
            Replace(values, second);
            slot = unchecked(slot + 7);
            return unchecked(slot + copy * 3 + values[0] * 5);
        }
        private static void Set<T>(ref T slot, T value) { slot = value; }
        private static int Read(in int slot, int ignored) { return slot; }
        public static int Reborrow(int first, int second)
        {
            var values = new int[] { first };
            ref int slot = ref values[0];
            Set(ref slot, second);
            return unchecked(Read(in slot, Replace(values, first)) + values[0]);
        }
        public static int UnsignedAlias(int first, int second)
        {
            var original = new uint[] { unchecked((uint)first) };
            var values = (int[])(object)original;
            bool equal = values[0].Equals(Replace(values, second));
            return unchecked((int)original[0] + (equal ? 17 : -17));
        }
        public static int EnumAlias(int first, int second)
        {
            var original = new Number[] { (Number)first };
            var values = (int[])(object)original;
            ref int slot = ref values[0];
            Set(ref slot, second);
            Replace(new int[1], first);
            return unchecked(slot + (int)original[0]);
        }
        public static int CarrierKinds(int first, int second)
        {
            return unchecked(UnsignedAlias(first, second) + new byte[first & 3].Length
                + new char[second & 3].Length + new bool[1].Length + new long[2].Length);
        }
        private static int Argument(int[] observed, int value)
        {
            observed[0] = 23;
            if (value < 0) throw new InvalidOperationException();
            return value;
        }
        public static int FaultOrder(int first, int second)
        {
            var observed = new int[1];
            int[] values = second == 0 ? null : new int[] { first };
            try { return (values[first].Equals(Argument(observed, second)) ? 31 : 37) + observed[0]; }
            catch (NullReferenceException) { return 101 + observed[0]; }
            catch (IndexOutOfRangeException) { return 103 + observed[0]; }
            catch (InvalidOperationException) { return 107 + observed[0]; }
        }
        private static int Replace(int[] values, int value)
        {
            var temporary = new int[17];
            temporary[0] = value;
            values[0] = temporary[0];
            return temporary[0];
        }
    }
}
