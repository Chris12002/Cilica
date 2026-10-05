using System;

namespace Cilica.Fixtures.Execution
{
    public struct MutatingHashValue
    {
        public long Number;
        public object Reference;
        public override int GetHashCode()
        {
            Number = unchecked(Number + 1);
            _ = new WideCell(Number);
            if (Number == 1) throw new InvalidOperationException("hash");
            return unchecked((int)Number) + (Reference != null ? 3 : 7);
        }
    }
    public static class ConstrainedValueProbe
    {
        private static int Hash<T>(ref T value) => value.GetHashCode();
        private static int Change<T>(ref T value, int next) where T : struct, IDirectConstrainedValue<int> => value.Change(next);
        public static int InterfaceOracle(int input, int mode)
        {
            var value = new DirectConstrainedValue<int> { Item = input };
            int before = Change(ref value, mode);
            return unchecked(before + value.Item);
        }
        public static int Mutation(int input, int mode)
        {
            var value = new MutatingHashValue { Number = input, Reference = new WideCell(mode) };
            int first;
            try { first = value.GetHashCode(); }
            catch (InvalidOperationException) { first = 19; }
            int second;
            try { second = Hash(ref value); }
            catch (InvalidOperationException) { second = 23; }
            return unchecked(first + second + (int)value.Number + mode);
        }
        public static int Generic(int input, int mode)
        {
            var first = new DirectConstrainedValue<int> { Item = input };
            var second = new DirectConstrainedValue<long> { Item = mode };
            return Hash(ref first) + Hash(ref second) + first.Item + (int)second.Item;
        }
        public static int Fallback(int input, int mode)
        {
            var value = new ConstrainedWithoutOverride { Number = input };
            return Hash(ref value) ^ mode;
        }
    }
}
