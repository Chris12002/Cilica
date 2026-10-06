namespace Cilica.Fixtures.Execution
{
    public sealed class NestedEnumOwner
    {
        private enum Phase { None, First = 1, Second = 2 }
        private Phase phase;
        public NestedEnumOwner(int bits) { phase = (Phase)bits; }
        private static Phase Echo(Phase value) => value;
        private static void Edit(ref Phase value, int bits) { value ^= (Phase)bits; }
        public int Change(int bits) { Edit(ref phase, bits); return (int)Echo(phase); }
        public struct Cell
        {
            private Phase phase;
            public Cell(int bits) { phase = (Phase)bits; }
            public int Change(int bits) { phase ^= (Phase)bits; return (int)phase; }
            public int PrivateCall(int bits) { Edit(ref phase, bits); return (int)phase; }
            public int Read() => (int)phase;
        }
        public static int UnsupportedBox(int a, int b) { object value = (Phase)a; return (int)(Phase)value; }
        public static int UnsupportedGeneric(int a, int b) { var value = new EnumHolder<Phase>(); value.Value = (Phase)a; return (int)value.Value; }
    }
    public static class NestedEnumProbe
    {
        public static int PrivateCall(int a, int b) { var value = new NestedEnumOwner.Cell(a); return value.PrivateCall(b); }
        public static int Objects(int a, int b) { var value = new NestedEnumOwner(a); return value.Change(b); }
        public static int Values(int a, int b) { var value = new NestedEnumOwner.Cell(a); var copy = value; return unchecked(value.Change(b) + copy.Read()); }
        public static int Arrays(int a, int b) { var values = new NestedEnumOwner.Cell[1]; values[0] = new NestedEnumOwner.Cell(a); return values[0].Change(b); }
    }
}
