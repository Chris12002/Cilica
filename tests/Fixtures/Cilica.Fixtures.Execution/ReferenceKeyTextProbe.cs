using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class GenericTextKey<T> { }
    public sealed class ReferenceTextTrace { public int Value; public Dictionary<ReferenceTextKey,int> Owner; }
    public class ReferenceTextKeyBase
    {
        public int Id, Mode; public ReferenceTextTrace Trace;
        public override int GetHashCode() { Trace.Value = unchecked(Trace.Value * 5 + 1); return 1; }
        public override bool Equals(object value)
        { Trace.Value = unchecked(Trace.Value * 5 + 2); return value is ReferenceTextKeyBase key && Id == key.Id; }
        public override string ToString()
        {
            Trace.Value = unchecked(Trace.Value * 5 + 3);
            var pressure = new int[3]; pressure[0] = Mode;
            if (pressure[0] == 1) return null;
            if (pressure[0] == 2) throw new InvalidOperationException("text callback");
            if (pressure[0] == 3 && Trace.Owner != null)
                Trace.Owner.TryAdd(new ReferenceTextKey { Id = ~Id, Trace = Trace }, 71);
            return "key\0\ud800";
        }
    }
    public sealed class ReferenceTextKey : ReferenceTextKeyBase { }
    public class FormattableTextKeyBase : IFormattable
    {
        public string ToString(string format, IFormatProvider provider) => "formatted";
        public override string ToString() => "ordinary";
    }
    public sealed class FormattableTextKey : FormattableTextKeyBase { }
    public static class ReferenceKeyTextProbe
    {
        private sealed class HiddenKey { }
        private static int Observe(string text)
        { if (text == null) return 0; int h = 17; for (int i = 0; i < text.Length; i++) h = unchecked(h * 31 + text[i]); return h; }
        public static int DefaultName(int a, int b)
        {
            var key = new PlainReferenceKey(); var d = new Dictionary<PlainReferenceKey,int>(); d.Add(key, b); int result = d[key];
            try { d.Add(key, a); } catch (ArgumentException error) { result = unchecked(result + Observe(error.Message) + Observe(error.ParamName)); }
            try { return unchecked(result + d[new PlainReferenceKey()]); } catch (KeyNotFoundException error) { return unchecked(result + Observe(error.Message) + d.Count); }
        }
        public static int GenericName(int a, int b)
        {
            var key = new GenericTextKey<Dictionary<int,string>>(); var d = new Dictionary<GenericTextKey<Dictionary<int,string>>,int>(); d.Add(key, a);
            try { d.Add(key, b); return 0; } catch (ArgumentException error) { return Observe(error.Message); }
        }
        public static int HiddenName(int a, int b)
        {
            var key = new HiddenKey(); var d = new Dictionary<HiddenKey,int>(); d.Add(key, a);
            try { return d[new HiddenKey()]; } catch (KeyNotFoundException error) { return Observe(error.Message); }
        }
        public static int Duplicate(int a, int b)
        {
            var trace = new ReferenceTextTrace(); var key = new ReferenceTextKey { Id = a, Trace = trace, Mode = b & 3 };
            var d = new Dictionary<ReferenceTextKey,int>(); d.Add(key, 17); trace.Owner = d; int result;
            try { d.Add(key, 19); result = 0; } catch (ArgumentException error) { result = Observe(error.Message); }
            catch (InvalidOperationException error) { result = Observe(error.Message); }
            return unchecked(result + trace.Value * 7 + d.Count * 11);
        }
        public static int Missing(int a, int b)
        {
            var trace = new ReferenceTextTrace(); var key = new ReferenceTextKey { Id = a, Trace = trace, Mode = b & 3 };
            var d = new Dictionary<ReferenceTextKey,int>(); trace.Owner = d; int result;
            if (a < 0) d.Add(new ReferenceTextKey { Id = ~a, Trace = trace }, b);
            try { result = d[key]; } catch (KeyNotFoundException error) { result = Observe(error.Message); }
            catch (InvalidOperationException error) { result = Observe(error.Message); }
            return unchecked(result + trace.Value * 7 + d.Count * 11);
        }
        public static int NullOrder(int a, int b)
        {
            var d = new Dictionary<ReferenceTextKey,int>(); int result;
            try { d.Add(null, a); result = 0; } catch (ArgumentNullException error) { result = Observe(error.Message); }
            try { return result + d[null]; } catch (ArgumentNullException error) { return unchecked(result + Observe(error.Message)); }
        }
        public static int UnsupportedPolymorphic(int a, int b)
        { var d = new Dictionary<ReferenceTextKeyBase,int>(); d.Add(new ReferenceTextKey(), a); return d.Count; }
        public static int UnsupportedFormattable(int a, int b)
        { var d = new Dictionary<FormattableTextKey,int>(); d.Add(new FormattableTextKey(), a); return d.Count; }
    }
}
