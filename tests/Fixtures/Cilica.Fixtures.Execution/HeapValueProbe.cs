using System;

namespace Cilica.Fixtures.Execution
{
    public struct HeapLeaf { public long Number; public HeapPayload Payload; }
    public struct HeapPair { public HeapLeaf Left; public HeapLeaf Right; }
    public struct HeapEmpty { }
    public sealed class HeapPayload
    {
        public int Number;
        public HeapPayload(int number) { Number = number; }
    }
    public class HeapHolder<T>
    {
        public T Value;
        public readonly T Initial;
        public HeapEmpty Empty;
        public HeapHolder(T value) { Value = value; Initial = value; }
    }
    public sealed class HeapChild : HeapHolder<HeapPair>
    {
        public Guid Id;
        public HeapChild(HeapPair value, Guid id) : base(value) { Id = id; }
    }
    public struct HeapLink { public HeapCycle Next; public long Number; }
    public sealed class HeapCycle { public HeapLink Link; }
    public interface IHeapAccessor { HeapPair Read(); void Write(HeapPair value); }
    public sealed class HeapAccessor : IHeapAccessor
    {
        private HeapPair value;
        public HeapPair Read() => value;
        public void Write(HeapPair replacement) { value = replacement; }
    }
    public static class HeapValueProbe
    {
        private static HeapPair Make(int number)
        {
            HeapPair pair = default;
            pair.Left.Number = (long)number * 2147483647L;
            pair.Left.Payload = new HeapPayload(number);
            pair.Right = pair.Left;
            return pair;
        }
        private static void Pressure()
        {
            for (int i = 0; i < 8; ++i) { var temporary = new HeapPayload(i); temporary.Number++; }
        }
        private static void Bump(ref HeapLeaf leaf, int number)
        {
            Pressure();
            leaf.Number += number;
            leaf.Payload = new HeapPayload(number);
        }
        private static void Replace(ref HeapPair pair, int number) { pair = Make(number); Pressure(); }
        public static int Copies(int input, int mode)
        {
            var holder = new HeapChild(Make(input), new Guid(input, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1));
            var copy = holder.Value;
            Bump(ref holder.Value.Left, mode);
            Pressure();
            return unchecked((int)(holder.Value.Left.Number + copy.Left.Number + holder.Initial.Left.Number)
                + holder.Value.Left.Payload.Number + holder.Value.Right.Payload.Number + holder.Initial.Right.Payload.Number
                + holder.Id.GetHashCode() + (holder.Value.Right.Payload == copy.Left.Payload ? 17 : 31));
        }
        public static int Aliases(int input, int mode)
        {
            var first = new HeapHolder<HeapPair>(Make(input));
            var second = new HeapHolder<HeapPair>(Make(mode));
            first.Value = second.Value;
            Replace(ref second.Value, input);
            Bump(ref first.Value.Right, mode);
            Pressure();
            return unchecked((int)(first.Value.Left.Number + second.Value.Right.Number)
                + first.Value.Right.Payload.Number + first.Initial.Left.Payload.Number
                + (first.Value.Left.Payload == second.Value.Left.Payload ? 37 : 41));
        }
        private static int ObserveEmpty(HeapEmpty value) => 1;
        private static void BorrowEmpty(ref HeapEmpty value) { Pressure(); value = default; }
        public static int EmptyAndNull(int input, int mode)
        {
            var holder = input < 0 ? null : new HeapHolder<HeapEmpty>(default);
            try
            {
                if ((mode & 3) == 0) return ObserveEmpty(holder.Value);
                if ((mode & 3) == 1) { BorrowEmpty(ref holder.Empty); return 2; }
                if ((mode & 3) == 2) { holder.Empty = default; return 3; }
                return ObserveEmpty(holder.Initial);
            }
            catch (NullReferenceException) { return -11; }
        }
        private static void Assign(ref HeapPair destination, int input, int mode) { destination = Make(input / mode); }
        public static int FaultOrder(int input, int mode)
        {
            HeapHolder<HeapPair> holder = null;
            try { Assign(ref holder.Value, input, mode); return 1; }
            catch (DivideByZeroException) { return 2; }
            catch (OverflowException) { return 3; }
            catch (NullReferenceException) { return 4; }
        }
        public static int CyclesAndDispatch(int input, int mode)
        {
            var cycle = new HeapCycle();
            cycle.Link.Next = cycle;
            cycle.Link.Number = (long)input * 2147483647L;
            IHeapAccessor accessor = new HeapAccessor();
            accessor.Write(Make(mode));
            Pressure();
            var result = accessor.Read();
            return unchecked((int)cycle.Link.Next.Link.Number + result.Left.Payload.Number
                + (int)result.Right.Number + (cycle == cycle.Link.Next ? 17 : 31));
        }
        public static int CompoundStore(int input, int mode)
        {
            HeapHolder<HeapPair> holder = null;
            try { holder.Value = Make(input / mode); return 1; }
            catch (DivideByZeroException) { return 2; }
            catch (OverflowException) { return 3; }
            catch (NullReferenceException) { return 4; }
        }
    }
}
