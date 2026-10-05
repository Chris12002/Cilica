using System;

namespace Cilica.Fixtures.Execution
{
    public interface IReferenceBoxMarker { int Read(); }
    public sealed class ReferenceBoxCell : IReferenceBoxMarker
    {
        public int Number;
        public ReferenceBoxCell(int value) { Number = value; }
        public int Read() => Number;
    }
    public static class ReferenceBoxing
    {
        private static object Pack<T>(T value) => value;
        private static T Unpack<T>(object value) => (T)value;
        private static bool IsNull<T>(T value) => value == null;
        public static int Identity(int input, int mode)
        {
            WideCell original = mode == 0 ? null : new WideCell(input);
            object packed = Pack(original);
            _ = new WideCell(mode);
            var restored = Unpack<WideCell>(packed);
            return (restored == original ? 3 : 7) + (IsNull(restored) ? 11 : unchecked((int)restored.Number));
        }
        public static int Contracts(int input, int mode)
        {
            IReferenceBoxMarker original = mode == 0 ? null : new ReferenceBoxCell(input);
            var packed = Pack(original);
            _ = new WideCell(mode);
            var restored = Unpack<IReferenceBoxMarker>(packed);
            string text = mode == 0 ? null : "reference";
            return (restored == original ? 13 : 17) + (Pack(text) == (object)text ? 19 : 23) + (Unpack<string>(Pack(text)) == text ? 29 : 31);
        }
        public static int Arrays(int input, int mode)
        {
            WideCell[] original = mode == 0 ? null : new WideCell[1];
            if (original != null) original[0] = new WideCell(input);
            var packed = Pack(original);
            _ = new WideCell(mode);
            var restored = Unpack<WideCell[]>(packed);
            var widened = Unpack<object[]>(packed);
            return (restored == original ? 37 : 41) + (widened == original ? 43 : 47) + (restored == null ? 53 : unchecked((int)restored[0].Number));
        }
        public static int Faults(int input, int mode)
        {
            object value = mode == 0 ? null : mode == 1 ? new WideCell(input) : (object)"wrong";
            try { var result = Unpack<WideCell>(value); return result == null ? 59 : unchecked((int)result.Number); }
            catch (InvalidCastException) { return 61; }
        }
        public static int ContractFaults(int input, int mode)
        {
            object value = mode == 0 ? null : mode == 1 ? new ReferenceBoxCell(input) : (object)new WideCell(input);
            try { var result = Unpack<IReferenceBoxMarker>(value); return result == null ? 67 : 71; }
            catch (InvalidCastException fault)
            {
                return fault.Message == "Unable to cast object of type 'Cilica.Fixtures.Execution.WideCell' to type 'Cilica.Fixtures.Execution.IReferenceBoxMarker'." ? 73 : 79;
            }
        }
    }
}
