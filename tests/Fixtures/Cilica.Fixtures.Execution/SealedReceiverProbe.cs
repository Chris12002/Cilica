using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class SealedText
    {
        private int value;
        public SealedText(int value) { this.value = value; }
        public override string ToString() => value < 0 ? "negative" : "positive-value";
    }
    public sealed class SealedTextHolder
    {
        public SealedText Text;
        public SealedTextHolder(SealedText text) { Text = text; }
        public int Read() => Text.ToString().Length;
    }
    public static class SealedReceiverProbe
    {
        private static SealedText Create(int value) => new SealedText(value);
        private static int Parameter(SealedText text) => text.ToString().Length;
        private static int Unproven(object text) => text.ToString().Length;
        public static int Calls(int input, int mode)
        {
            SealedText local = mode < 0 ? null : new SealedText(input);
            int result = input;
            try { result = unchecked(result + local.ToString().Length + Parameter(local)); }
            catch (NullReferenceException) { result = unchecked(result + 17); }
            return unchecked(result + new SealedText(mode).ToString().Length + Create(input).ToString().Length);
        }
        public static int Fields(int input, int mode)
        {
            var holder = new SealedTextHolder(mode < 0 ? null : new SealedText(input));
            try { return unchecked(input + holder.Read()); }
            catch (NullReferenceException) { return unchecked(input + 31); }
        }
        public static int UnprovenReceiver(int input, int mode) => unchecked(Unproven(new SealedText(input)) + mode);
    }
}
