namespace Cilica.Fixtures.Execution
{
    public static class GenericMemory
    {
        private static T Empty<T>() => default;
        private static T Read<T>(ref T value) => value;
        private static void Copy<T>(ref T destination, ref T source) => destination = source;
        private static void Clear<T>(ref T destination) => destination = default;
        public static int References(int input, int mode)
        {
            var original = new WideCell(input);
            WideCell value = Empty<WideCell>();
            Copy(ref value, ref original);
            _ = new WideCell(mode);
            var copy = Read(ref value);
            Clear(ref value);
            string text = "kept";
            Clear(ref text);
            int[] array = new int[1];
            Clear(ref array);
            return (value == null ? 3 : 7) + (text == null ? 11 : 13) + (array == null ? 17 : 19) + unchecked((int)copy.Number);
        }
        public static int Integers(int input, int mode)
        {
            long value = ((long)input << 32) | (uint)mode;
            long copy = Empty<long>();
            Copy(ref copy, ref value);
            long observed = Read(ref copy);
            Clear(ref value);
            int zero = input;
            Clear(ref zero);
            uint bits = unchecked((uint)mode);
            uint unsignedCopy = Empty<uint>();
            Copy(ref unsignedCopy, ref bits);
            return unchecked((int)((observed >> 32) ^ observed ^ value) + zero + (int)Read(ref unsignedCopy));
        }
        public static int Values(int input, int mode)
        {
            var original = new WidePayload { Number = ((long)input << 32) | (uint)mode, Reference = new WideCell(mode) };
            var value = Empty<WidePayload>();
            Copy(ref value, ref original);
            Copy(ref value, ref value);
            var snapshot = Read(ref value);
            Clear(ref value);
            _ = new WideCell(input);
            return unchecked((int)snapshot.Number + (int)(snapshot.Number >> 32)) + (value.Reference == null && value.Number == 0 ? 23 : 29)
                + (snapshot.Reference == original.Reference ? 31 : 37);
        }
        public static int Narrow(int input, int mode) { byte value = (byte)input; Clear(ref value); return value + mode; }
    }
}
