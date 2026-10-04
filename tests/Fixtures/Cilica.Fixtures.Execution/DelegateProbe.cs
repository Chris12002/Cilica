namespace Cilica.Fixtures.Execution
{
    public delegate int IntegerDelegate(int number);
    public delegate void IntegerAction(int number);
    public class DelegateReceiver
    {
        public int Number;
        public IntegerDelegate Back;
        public DelegateReceiver(int number) { Number = number; }
        public int Add(int number) => unchecked(Number + number);
        public void Set(int number) { Number = number; }
    }
    public static class DelegateProbe
    {
        public static int AddOne(int number) => unchecked(number + 1);
        public static int Fault(int number) => 100 / number;
        public static IntegerDelegate Make(int number)
        {
            var receiver = new DelegateReceiver(number);
            var add = new IntegerDelegate(AddOne);
            var captured = new IntegerDelegate(receiver.Add);
            var setter = new IntegerAction(receiver.Set);
            setter(captured(add(number)));
            receiver.Back = captured;
            return captured;
        }
        public static int Execute(int number, int mode)
        {
            var callable = Make(number);
            var pair = GenericPair<int, IntegerDelegate>.Create(number, callable);
            object box = pair;
            callable = null;
            pair = default;
            ValueObjectProbe.Pressure(12);
            var returned = (GenericPair<int, IntegerDelegate>)box;
            if (mode == 1) returned.Second = null;
            if (mode == 2) returned.Second = new IntegerDelegate(Fault);
            return returned.Second(returned.First);
        }
        public static int NullCapture(int number)
        {
            DelegateReceiver receiver = null;
            var callable = new IntegerDelegate(receiver.Add);
            return callable(number);
        }
        public static int NullOnly(int number)
        {
            IntegerDelegate callable = null;
            return callable(number);
        }
        public static int UnsupportedMulticast(int number)
        {
            var first = new IntegerDelegate(AddOne);
            var second = new IntegerDelegate(Fault);
            return (first + second)(number);
        }
    }
}
