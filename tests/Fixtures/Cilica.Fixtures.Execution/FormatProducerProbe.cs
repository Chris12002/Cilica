using System;
namespace Cilica.Fixtures.Execution
{
    public struct FormatProducerValue
    {
        public long Bits;
        public long Number => Bits;
        public long Advance() { Bits = unchecked(Bits + 1); return Bits; }
    }
    public sealed class FormatProducerSource
    {
        public long Bits;
        public int Calls, Stop;
        public FormatProducerValue Value
        {
            get
            {
                ++Calls;
                if (Calls == Stop) throw new InvalidOperationException("producer");
                return new FormatProducerValue { Bits = unchecked(Bits + Calls) };
            }
        }
    }
    public static class FormatProducerProbe
    {
        private static int Observe(string text)
        { int h = 17; for (int i = 0; i < text.Length; ++i) h = unchecked(h * 31 + text[i]); return h; }
        private static long Bits(int a, int b) => ((long)a << 32) | (uint)b;
        public static int Temporary(int a, int b)
        {
            var source = new FormatProducerSource { Bits = Bits(a, b) };
            return unchecked(Observe(string.Format("Item {0} has no value.", source.Value.Number)) + source.Calls);
        }
        public static int Several(int a, int b)
        {
            var source = new FormatProducerSource { Bits = Bits(a, b) };
            return unchecked(Observe(string.Format("{2}/{0,23}/{1,-23}/{0}", source.Value.Number,
                source.Value.Number, source.Value.Number)) + source.Calls);
        }
        private static int FromParameter(FormatProducerValue value)
        { return unchecked(Observe(string.Format("{0}/{1}", value.Advance(), value.Number)) + (int)value.Bits); }
        public static int AddressedParameter(int a, int b) => FromParameter(new FormatProducerValue { Bits = Bits(a, b) });
        public static int AddressedLocal(int a, int b)
        {
            var value = new FormatProducerValue { Bits = Bits(a, b) };
            return unchecked(Observe(string.Format("{0}/{1}", value.Advance(), value.Number)) + (int)value.Bits);
        }
        public static int FaultOrder(int a, int b)
        {
            var source = new FormatProducerSource { Bits = a, Stop = b & 3 };
            int result;
            try { result = Observe(string.Format("{9}", source.Value.Number, source.Value.Number)); }
            catch (InvalidOperationException e) { result = Observe(e.Message); }
            catch (FormatException e) { result = Observe(e.Message); }
            return unchecked(result + source.Calls * 97);
        }
        public static int NullReceiver(int a, int b)
        {
            FormatProducerSource source = null;
            try { return Observe(string.Format("{9}", source.Value.Number)); }
            catch (NullReferenceException) { return unchecked(a + b); }
        }
        public static int UnsupportedLiteralAlias(int a, int b)
        { var source = new FormatProducerSource { Bits = a }; string format = "{0}"; return string.Format(format, source.Value.Number).Length; }
        public static int UnsupportedBoxAlias(int a, int b)
        { var source = new FormatProducerSource { Bits = a }; object value = source.Value.Number; return string.Format("{0}", value).Length; }
        public static int UnsupportedDynamic(int a, int b)
        { var source = new FormatProducerSource { Bits = a }; return string.Format(a < 0 ? "{0}" : "<{0}>", source.Value.Number).Length; }
    }
}
