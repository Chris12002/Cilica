namespace Cilica.Fixtures.Execution
{
    public ref struct StackOnlyValue { public int Number; }
    public ref struct StackOnlyGeneric<T> { public T Value; }
    public static class ByRefLikeProbe
    {
        public static int Copy(int input, int mode)
        {
            var value = new StackOnlyValue { Number = input };
            var copy = value; copy.Number += mode;
            return value.Number ^ copy.Number;
        }
        public static int GenericCopy(int input, int mode)
        {
            var value = new StackOnlyGeneric<int> { Value = input };
            var copy = value; copy.Value += mode;
            return value.Value ^ copy.Value;
        }
    }
}
