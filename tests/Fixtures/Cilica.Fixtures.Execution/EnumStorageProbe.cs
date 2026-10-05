using System;
namespace Cilica.Fixtures.Execution
{
    public enum FlowStatus { None, Ready = 3, Faulted = -1 }
    public enum OtherStatus { None, Ready = 8 }
    public enum SmallStatus : byte { None, Ready }
    public enum WideStatus : long { None, Ready }
    public struct EnumPair { public FlowStatus First; public OtherStatus Second; }
    public sealed class EnumHolder<T> { public T Value; }
    public static class EnumSlot<T> { public static T Value; }
    public interface IEnumRead { FlowStatus Read(FlowStatus value); }
    public sealed class EnumRead : IEnumRead { public FlowStatus Read(FlowStatus value) => (FlowStatus)((int)value + 1); }
    public static class EnumStorageProbe
    {
        private static T Copy<T>(ref T value) { T result = value; value = default(T); return result; }
        private static void Change(ref FlowStatus value, int add) { value = (FlowStatus)unchecked((int)value + add); }
        public static int Storage(int input, int mode)
        {
            var pair = new EnumPair { First = (FlowStatus)input, Second = (OtherStatus)mode };
            var holder = new EnumHolder<EnumPair>(); holder.Value = pair;
            Change(ref holder.Value.First, mode);
            var copy = holder.Value;
            var previous = Copy(ref copy.First);
            return unchecked((int)previous + (int)copy.First + (int)holder.Value.First + (int)copy.Second);
        }
        public static int GenericAndDispatch(int input, int mode)
        {
            EnumSlot<FlowStatus>.Value = (FlowStatus)input;
            EnumSlot<OtherStatus>.Value = (OtherStatus)mode;
            var holder = new EnumHolder<FlowStatus>(); holder.Value = EnumSlot<FlowStatus>.Value;
            IEnumRead reader = new EnumRead();
            var result = reader.Read(holder.Value);
            return unchecked((int)result + (int)EnumSlot<OtherStatus>.Value);
        }
        public static int Small(int input, int mode) { var holder = new EnumHolder<SmallStatus>(); holder.Value = (SmallStatus)input; return (int)holder.Value; }
        public static int Wide(int input, int mode) { var holder = new EnumHolder<WideStatus>(); holder.Value = (WideStatus)input; return (int)holder.Value; }
        public static int Box(int input, int mode) { object boxed = (FlowStatus)input; return (int)(FlowStatus)boxed; }
        public static int Array(int input, int mode) { var values = new FlowStatus[1]; values[0] = (FlowStatus)input; return (int)values[0]; }
    }
}
