using System;
using System.Text;
namespace Cilica.Fixtures.Execution
{
    public sealed class StackReceiverTrace { public int Value, Mode; }
    public sealed class StackReceiverText
    {
        public int Value, Mode; public StackReceiverTrace Trace;
        public StackReceiverText(int value, StackReceiverTrace trace) { Value = value; Trace = trace; }
        public override string ToString()
        {
            Trace.Value = unchecked(Trace.Value * 5 + 3);
            var pressure = new int[5]; pressure[0] = Value;
            if (Mode == 2) throw new ArgumentException("text");
            return pressure[0] < 0 ? "negative" : "nonnegative";
        }
    }
    public static class StackReceiverProbe
    {
        private static int Observe(string text) { int result = 17; for (int i = 0; i < text.Length; ++i) result = unchecked(result * 31 + text[i]); return result; }
        private static void Fill(StringBuilder text, string content)
        { for (int i = 0; i < 17; ++i) text.Append(content); }
        private static string FingerprintText(string content)
        { var text = new StringBuilder(); Fill(text, content); return text.ToString(); }
        public static int Fingerprint(int a, int b) => unchecked(Observe(FingerprintText(a < 0 ? "A\0\ud800" : "bc\udc00")) + b);
        private static void Touch(StackReceiverText text, StackReceiverTrace trace)
        {
            int mode = trace.Mode;
            trace.Value = unchecked(trace.Value * 5 + 1);
            var pressure = new int[5]; pressure[0] = mode;
            if (mode < 0) throw new InvalidOperationException("touch");
            if (text != null) { text.Value = unchecked(text.Value + pressure[0]); text.Mode = mode & 3; }
        }
        private static int TouchResult(StackReceiverText text, StackReceiverTrace trace)
        { Touch(text, trace); return trace.Mode; }
        private static StackReceiverText Create(int a, StackReceiverTrace trace) => a < 0 ? null : new StackReceiverText(a,trace);
        private static string Duplicated(int a, int b, StackReceiverTrace trace)
        { var text = new StackReceiverText(a,trace); Touch(text,trace); return text.ToString(); }
        private static string Returned(int a, int b, StackReceiverTrace trace)
        { var text = Create(a,trace); TouchResult(text,trace); return text.ToString(); }
        private static string Repeated(int a, int b, StackReceiverTrace trace)
        { var text = new StackReceiverText(a,trace); Touch(text,trace); TouchResult(text,trace); return text.ToString(); }
        public static int Duplicate(int a, int b)
        {
            var trace = new StackReceiverTrace { Mode = b }; int result;
            try { result = Observe(Duplicated(a,b,trace)); } catch (InvalidOperationException) { result = 11; } catch (ArgumentException) { result = 13; }
            return unchecked(result + trace.Value * 7);
        }
        public static int ReturnedAndNull(int a, int b)
        {
            var trace = new StackReceiverTrace { Mode = b }; int result;
            try { result = Observe(Returned(a,b,trace)); } catch (InvalidOperationException) { result = 11; } catch (ArgumentException) { result = 13; } catch (NullReferenceException) { result = 17; }
            return unchecked(result + trace.Value * 7);
        }
        public static int RepeatedCopies(int a, int b)
        {
            var trace = new StackReceiverTrace { Mode = b }; int result;
            try { result = Observe(Repeated(a,b,trace)); } catch (InvalidOperationException) { result = 11; } catch (ArgumentException) { result = 13; }
            return unchecked(result + trace.Value * 7);
        }
    }
}
