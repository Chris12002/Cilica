using System;
namespace Cilica.Fixtures.Execution
{
    public static class SwitchProbe
    {
        public static int Dense(int value, int mode)
        {
            switch (mode) {
                case 0: return unchecked(value + 31);
                case 1: return unchecked(value - 19);
                case 2: case 4: return unchecked(value * 7);
                case 3: return value ^ 17;
                case 5: return ~value;
                default: return value;
            }
        }
        public static int Protected(int value, int mode)
        {
            var box = new ValueVectorBox(value);
            try {
                try {
                    switch (mode) {
                        case 0: box.Number = unchecked(box.Number + 1); break;
                        case 1: throw new InvalidOperationException("switch");
                        case 2: box = new ValueVectorBox(value ^ 7); break;
                        case 3: return unchecked(box.Number * 3);
                        case 4: box.Number = Dense(value, 5); break;
                        default: box.Number = unchecked(box.Number - 9); break;
                    }
                }
                finally { box.Number = unchecked(box.Number + 13); }
            }
            catch (InvalidOperationException) { box.Number = unchecked(box.Number + 23); }
            return box.Number;
        }
        public static int Loop(int value, int mode)
        {
            int result = value;
            for (int i = 0; i < 7; ++i) {
                switch (unchecked(mode + i)) {
                    case 0: result = unchecked(result + 1); continue;
                    case 1: result ^= 19; break;
                    case 2: case 4: result = unchecked(result * 3); break;
                    case 3: result = unchecked(result - 17); break;
                    case 5: result = ~result; break;
                    default: result = unchecked(result + i); break;
                }
            }
            return result;
        }
    }
}
