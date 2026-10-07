namespace Cilica.Fixtures.Execution
{
    public interface IBoxCall { int Read(); }
    public struct BoxedCallValue : IBoxCall { public int Number; public int Read() => Number; }
    public sealed class BoxedCallClass : IBoxCall
    {
        public int Number;
        public BoxedCallClass(int number) { Number = number; }
        public int Read() => Number;
    }
    public interface IBoxParent { }
    public interface IBoxChild : IBoxParent { }
    public interface IBoxUnrelated { }
    public interface IBoxOut<out T> { }
    public struct InterfaceBoxValue : IBoxChild, IBoxOut<string>
    {
        public int Number;
        public string Text;
    }
    public struct OtherInterfaceBoxValue : IBoxChild { public int Number; }
    public sealed class InterfaceBoxHolder { public IBoxParent Value; }
    public struct BoxPair { public int Number; public long Wide; public string Text; }
    public interface IBoxState
    {
        void Advance(int amount);
        BoxPair Exchange(BoxPair value, int marker);
        int Inspect();
        void Fail(int amount);
        object Snapshot();
    }
    public struct BoxState : IBoxState
    {
        public BoxPair Pair;
        public int Count;
        public void Advance(int amount)
        {
            Pair.Number = unchecked(Pair.Number + amount); Count++;
            Pair.Wide = unchecked(Pair.Wide + amount);
            Pair.Text = string.Concat(Pair.Text, "!");
        }
        public BoxPair Exchange(BoxPair value, int marker)
        {
            BoxPair previous = Pair; Pair = value; Count = marker;
            Pair.Text = string.Concat(Pair.Text, "swap");
            return previous;
        }
        public int Inspect() => unchecked(Pair.Number + (int)Pair.Wide + Pair.Text.Length + Count);
        public void Fail(int amount) { Advance(amount); throw new System.InvalidOperationException("after mutation"); }
        public object Snapshot() { string pressure = string.Concat(Pair.Text, "copy"); Count += pressure.Length; return this; }
    }
    public interface IBoxCursor<T> { T Next(int delta); int Count(); }
    public struct BoxCursor<T> : IBoxCursor<T>
    {
        public T Item; public int Position;
        T IBoxCursor<T>.Next(int delta) { Position = unchecked(Position + delta); return Item; }
        int IBoxCursor<T>.Count() => Position;
    }
    public interface IBoxEmpty { int Read(int value); void Touch(int mode); }
    public struct EmptyBoxCall : IBoxEmpty
    {
        public int Read(int value) => unchecked(value + 117);
        public void Touch(int mode) { if (mode < 0) throw new System.InvalidOperationException(); }
    }
    public static class BoxedInterfaces
    {
        public static int Mutable(int input, int mode)
        {
            var raw = new BoxState(); raw.Pair.Number = input; raw.Pair.Wide = ((long)input << 32) | (uint)mode; raw.Pair.Text = "initial";
            raw.Advance(1);
            IBoxState first = raw; IBoxState alias = first; IBoxState independent = raw;
            first.Advance(mode); alias.Advance(2);
            var copied = (BoxState)first; copied.Advance(3);
            return unchecked(first.Inspect() + independent.Inspect() + copied.Inspect() + raw.Inspect());
        }
        public static int PayloadReturn(int input, int mode)
        {
            var raw = new BoxState(); raw.Pair.Number = input; raw.Pair.Wide = (long)input << 33; raw.Pair.Text = "old";
            IBoxState box = raw;
            var replacement = new BoxPair(); replacement.Number = mode; replacement.Wide = (long)mode << 31; replacement.Text = "new";
            var previous = box.Exchange(replacement, input);
            replacement.Number = 7;
            return unchecked(box.Inspect() + previous.Number + (int)previous.Wide + previous.Text.Length);
        }
        public static int MutationFault(int input, int mode)
        {
            var raw = new BoxState(); raw.Pair.Number = input; raw.Pair.Text = "fault";
            IBoxState box = raw; int result = 0;
            try { box.Fail(mode); } catch (System.InvalidOperationException) { result = box.Inspect(); }
            IBoxState missing = null;
            try { missing.Advance(input); } catch (System.NullReferenceException) { result = unchecked(result + 101); }
            try { result = missing.Inspect(); } catch (System.NullReferenceException) { result = unchecked(result + 211); }
            return result;
        }
        private static IBoxState CreateState(int input) { var value = new BoxState(); value.Pair.Number = input; value.Pair.Text = "only root"; return value; }
        public static int Allocation(int input, int mode)
        {
            IBoxState box = CreateState(input); box.Advance(mode);
            var copy = (BoxState)box.Snapshot();
            return unchecked(CreateState(mode).Inspect() + copy.Inspect() + box.Inspect());
        }
        public static int GenericValue(int input, int mode)
        {
            var raw = new BoxCursor<BoxPair>(); raw.Position = input; raw.Item.Number = mode; raw.Item.Wide = (long)input << 32; raw.Item.Text = "generic";
            IBoxCursor<BoxPair> box = raw;
            var first = box.Next(mode); var second = box.Next(input);
            return unchecked(first.Number + second.Text.Length + (int)second.Wide + box.Count() + raw.Position);
        }
        public static int GenericReference(int input, int mode)
        {
            var raw = new BoxCursor<BoxedCallClass>(); raw.Position = mode; raw.Item = new BoxedCallClass(input);
            IBoxCursor<BoxedCallClass> box = raw;
            var item = box.Next(input); item.Number = unchecked(item.Number + mode);
            return unchecked(box.Next(3).Number + box.Count() + raw.Position);
        }
        public static int Empty(int input, int mode)
        {
            IBoxEmpty box = new EmptyBoxCall(); int result = box.Read(input);
            try { box.Touch(mode); } catch (System.InvalidOperationException) { result = unchecked(result + 33); }
            return unchecked(result + box.Read(mode));
        }
        public static int ReadConfined(object value) => ((BoxedCallValue)value).Number;
        private static IBoxCall Escape(object value) => (IBoxCall)value;
        public static int ConfinedWithDispatch(int input, int mode)
        {
            IBoxCall other = new BoxedCallClass(input);
            var value = new BoxedCallValue(); value.Number = mode;
            return unchecked(ReadConfined(value) + other.Read());
        }
        public static int EscapedDispatch(int input, int mode)
        {
            var value = new BoxedCallValue(); value.Number = mode;
            return Escape(value).Read();
        }
        private static IBoxParent Wrap(InterfaceBoxValue value) => value;
        public static int UnsupportedDispatch(int input, int mode)
        {
            var value = new RuntimeEquatableValue(); value.Number = input;
            System.IEquatable<RuntimeEquatableValue> box = value;
            return box.Equals(value) ? mode : input;
        }
        public static int MixedDispatch(int input, int mode)
        {
            IBoxCall value = new BoxedCallClass(input);
            if (mode < 0) { var boxed = new BoxedCallValue(); boxed.Number = mode; value = boxed; }
            return value.Read();
        }
        public static int Relations(int input, int mode)
        {
            var value = new InterfaceBoxValue(); value.Number = input; value.Text = "box";
            object box = value;
            object second = value;
            IBoxChild child = (IBoxChild)box;
            IBoxParent parent = child;
            string pressure = string.Concat("allocation", "pressure");
            return ((object)parent == box ? 1 : 0) + (box == second ? 2 : 0)
                + (box is IBoxUnrelated ? 4 : 0) + (box is IBoxParent ? 8 : 0)
                + (box is IBoxOut<object> ? 16 : 0) + ((InterfaceBoxValue)parent).Number + pressure.Length;
        }
        public static int Storage(int input, int mode)
        {
            var value = new InterfaceBoxValue(); value.Number = input; value.Text = string.Concat("root", "value");
            var holder = new InterfaceBoxHolder(); holder.Value = value;
            var array = new IBoxParent[2]; array[0] = holder.Value; array[1] = value;
            var returned = Wrap(value);
            value.Number = mode; value.Text = "changed";
            var copy = (InterfaceBoxValue)array[0];
            copy.Number++;
            string pressure = string.Concat("later", "allocation");
            return copy.Number + ((InterfaceBoxValue)holder.Value).Number + copy.Text.Length + pressure.Length
                + ((InterfaceBoxValue)array[1]).Number + ((InterfaceBoxValue)returned).Text.Length;
        }
        public static int Faults(int input, int mode)
        {
            var value = new InterfaceBoxValue(); value.Number = input;
            object box = value; int result = 0;
            try { var other = (IBoxUnrelated)box; result += other == null ? 1 : 2; }
            catch (System.InvalidCastException) { result += 4; }
            try { var other = (OtherInterfaceBoxValue)box; result += other.Number; }
            catch (System.InvalidCastException) { result += 8; }
            object missing = null;
            try { var other = (InterfaceBoxValue)missing; result += other.Number; }
            catch (System.NullReferenceException) { result += 16; }
            return result + ((IBoxChild)missing == null ? 32 : 0);
        }
    }
}
