using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class FieldReturnHolder
    {
        public HeapPair Value;
        public readonly HeapPair Initial;
        public HeapEmpty Empty;
        public int Count;
        public int ReadThenReturn(int a) { int previous = Count; if (a < 0) Count = ~a; Value = FieldReturnProbe.Make(a); return previous + Value.Left.Payload.Number; }
        public int StoreThenReturn(int a) { Count = a; Value = FieldReturnProbe.Make(a); return Value.Right.Payload.Number; }
        public int AfterCatch(int a) { try { Count++; } catch (NullReferenceException) { } Value = FieldReturnProbe.Make(a); return a; }
        public FieldReturnHolder(int value) { Value = FieldReturnProbe.Make(value); Initial = FieldReturnProbe.Make(~value); }
    }
    public sealed class FieldReturnTrace { public int Sequence; public FieldReturnHolder Current; }
    public interface IFieldReturnFactory { HeapPair Make(int value); }
    public sealed class FieldReturnFactory : IFieldReturnFactory
    { public HeapPair Make(int value) => FieldReturnProbe.Make(value); }
    public static class FieldReturnProbe
    {
        internal static HeapPair Make(int value)
        {
            var result = new HeapPair(); result.Left.Number = (long)value * 2147483647L;
            result.Left.Payload = new HeapPayload(value); result.Right = result.Left;
            for (int i = 0; i < 8; ++i) { var pressure = new HeapPayload(i); pressure.Number++; }
            return result;
        }
        private static int Read(HeapPair value) => unchecked((int)value.Left.Number + (int)(value.Right.Number >> 32)
            + value.Left.Payload.Number + value.Right.Payload.Number + (ReferenceEquals(value.Left.Payload, value.Right.Payload) ? 17 : 0));
        private static FieldReturnHolder Receiver(FieldReturnTrace trace, int a)
        { trace.Sequence = 1; if (a < 0) throw new ArgumentException("receiver"); return a == 0 ? null : trace.Current; }
        private static HeapPair Value(FieldReturnTrace trace, int a, int b)
        { trace.Sequence = unchecked(trace.Sequence * 10 + 2); return Make(a / b); }
        public static int Order(int a, int b)
        {
            var trace = new FieldReturnTrace(); trace.Current = new FieldReturnHolder(a);
            try { Receiver(trace, a).Value = Value(trace, a, b); return Read(trace.Current.Value); }
            catch (ArgumentException) { return 100 + trace.Sequence; }
            catch (DivideByZeroException) { return 200 + trace.Sequence; }
            catch (OverflowException) { return 300 + trace.Sequence; }
            catch (NullReferenceException) { return 400 + trace.Sequence; }
        }
        private static HeapPair Swap(FieldReturnTrace trace, int a, int b)
        { trace.Current = new FieldReturnHolder(b); return Make(a); }
        public static int OriginalReceiver(int a, int b)
        {
            var trace = new FieldReturnTrace(); var original = new FieldReturnHolder(a); trace.Current = original;
            original.Value = Swap(trace, b, a);
            return unchecked(Read(original.Value) + Read(trace.Current.Value) + (ReferenceEquals(original, trace.Current) ? 100 : 200));
        }
        public static int FaultPreservesDestination(int a, int b)
        {
            var trace = new FieldReturnTrace(); var holder = new FieldReturnHolder(a);
            try { holder.Value = Value(trace, a, b); }
            catch (DivideByZeroException) { trace.Sequence += 10; }
            catch (OverflowException) { trace.Sequence += 20; }
            return unchecked(Read(holder.Value) + trace.Sequence);
        }
        public static int VirtualReturn(int a, int b)
        {
            IFieldReturnFactory factory = new FieldReturnFactory(); var holder = new FieldReturnHolder(a);
            holder.Value = factory.Make(b); return unchecked(Read(holder.Value) + Read(holder.Initial));
        }
        private static HeapEmpty EmptyValue(FieldReturnTrace trace, int b)
        { trace.Sequence = 7; var pressure = new HeapPayload(b); if (b == 0) throw new ArgumentException("empty"); return default; }
        public static int NullableEmptyReturn(int a, int b)
        {
            var trace = new FieldReturnTrace(); var holder = a < 0 ? null : new FieldReturnHolder(a);
            try { holder.Empty = EmptyValue(trace, b); return trace.Sequence; }
            catch (ArgumentException) { return 10 + trace.Sequence; }
            catch (NullReferenceException) { return 20 + trace.Sequence; }
        }
        public static int EmptyReturn(int a, int b)
        {
            var trace = new FieldReturnTrace(); var holder = new FieldReturnHolder(a);
            try { holder.Empty = EmptyValue(trace, b); return trace.Sequence; }
            catch (ArgumentException) { return 10 + trace.Sequence; }
        }
        public static int JoinedAllocations(int a, int b)
        {
            var holder = a < b ? new FieldReturnHolder(a) : new FieldReturnHolder(b);
            holder.Value = Make(a); return Read(holder.Value);
        }
        public static int LoopRetainsReceiver(int a, int b)
        {
            var holder = new FieldReturnHolder(a);
            for (int i = 0; i < (b & 3); i++) holder.Value = Make(unchecked(a + i));
            return Read(holder.Value);
        }
        public static int NullableJoin(int a, int b)
        { var holder = a < b ? null : new FieldReturnHolder(b); holder.Value = Make(a); return Read(holder.Value); }
        public static int NullableBackEdge(int a, int b)
        {
            var holder = new FieldReturnHolder(a);
            for (int i = 0; i < (b & 3); i++) { holder.Value = Make(a); holder = null; }
            return a;
        }
        private static void Clear(ref FieldReturnHolder holder) { holder = null; }
        public static int AddressedReceiver(int a, int b)
        { var holder = new FieldReturnHolder(a); Clear(ref holder); holder.Value = Make(b); return a; }
        public static int FinallyClearsReceiver(int a, int b)
        {
            var holder = new FieldReturnHolder(a);
            try { if (b == 0) throw new ArgumentException(); } catch (ArgumentException) { }
            finally { holder = null; }
            holder.Value = Make(b); return a;
        }
        public static int InstanceAfterRead(int a, int b) { var holder = new FieldReturnHolder(a); holder.Count = b; return holder.ReadThenReturn(a); }
        public static int InstanceAfterStore(int a, int b) { var holder = new FieldReturnHolder(a); return holder.StoreThenReturn(b); }
        public static int ExceptionDropsReceiver(int a, int b) { var holder = new FieldReturnHolder(a); return holder.AfterCatch(b); }
        private static HeapPair ConstructThenFail(int a, int b)
        { var value = Make(b); if (a < 0) throw new ArgumentException("callee"); return value; }
        public static int CalleeFaultPreservesField(int a, int b)
        {
            var holder = new FieldReturnHolder(a);
            try { holder.Value = ConstructThenFail(a, b); }
            catch (ArgumentException) { }
            return Read(holder.Value);
        }
        public static int ReadonlyConstruction(int a, int b)
        { var holder = new FieldReturnHolder(a); holder.Value = Make(b); return unchecked(Read(holder.Initial) + Read(holder.Value)); }
    }
}
