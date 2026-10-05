using System;

namespace Cilica.Fixtures.Execution
{
    public static class ComposedStringProbe
    {
        public static int CodeUnits(int index, int mode)
        {
            string text = mode == 0 ? null : "A\0\ud800X\udfff";
            try { return text[index]; }
            catch (NullReferenceException) { return 41; }
            catch (IndexOutOfRangeException) { return 53; }
        }
        public static int ArgumentFault(int input, int divisor)
        {
            string text = null;
            try { return text[12 / divisor]; }
            catch (DivideByZeroException) { return input ^ 61; }
            catch (NullReferenceException) { return input ^ 41; }
        }
        public static int Concat(int input, int mode)
        {
            string left = mode == 0 ? null : mode == 1 ? "" : "A\0\ud800";
            string right = input == 0 ? null : input == 1 ? "" : "X\udfff";
            string joined = string.Concat(left, right);
            int result = joined.Length;
            for (int index = 0; index < joined.Length; ++index) result = unchecked(result * 31 + joined[index]);
            if ((object)joined == (object)left) result ^= 17;
            if ((object)joined == (object)right) result ^= 37;
            if (joined != "") result ^= 53;
            return result;
        }
        public static int Rooted(int input, int other)
        {
            WideOuter value = default;
            value.Marker = input;
            string original = string.Concat("start", "\ud800\0end");
            object box = value;
            string next = string.Concat(original, "tail");
            var target = new AllocatingClassDispatch<long>();
            return unchecked(original[5] + next[6] + ((WideOuter)box).Marker + (int)target.Echo(other) + WideObjects.Values(input, other));
        }
        public static int Unsupported(int input, int other) => string.Concat("a", "b", input == 0 ? "" : "c").Length;
    }
}
