namespace Cilica.Fixtures.Execution
{
    public static class ValueCilProbe
    {
        public static int Execute(int number, int iterations)
        {
            var selected = number > 0 ? ValueReturnProbe.Make(number) : ValueReturnProbe.Make(7);
            var saved = selected;
            for (int i = 0; i < iterations; i++)
                selected.Number = unchecked(selected.Number + i);
            int looped = selected.Number;
            Clear(ref selected);
            Increment(ref selected.Number);
            return unchecked(saved.Number + looped + selected.Number);
        }
        public static void Clear(ref ValueLeaf value) { value = default; }
        public static void Increment(ref int value) { value = unchecked(value + 1); }
    }
}
