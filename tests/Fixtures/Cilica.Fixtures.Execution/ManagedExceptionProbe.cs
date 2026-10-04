using System;

namespace Cilica.Fixtures.Execution
{
    public class ProbeException : Exception
    {
        public ProbeException() { }
        public ProbeException(string message) : base(message) { }
    }
    public static class ManagedExceptionProbe
    {
        public static int TypedCatch(int number)
        {
            try { return 100 / number; }
            catch (DivideByZeroException) { return 17; }
        }
        public static int Cleanup(int number)
        {
            int result = 0;
            try
            {
                try { result = 100 / number; }
                finally { result = unchecked(result + 5); }
            }
            catch (DivideByZeroException) { result = unchecked(result + 20); }
            return result;
        }
        public static int TypeFilter(int number)
        {
            try { if (number == 0) throw new ProbeException(); return 100 / number; }
            catch (Exception error) when (error is ProbeException || error is OverflowException) { return 31; }
        }
        public static int Rethrow(int number)
        {
            try
            {
                try { return 100 / number; }
                catch (DivideByZeroException) { throw; }
            }
            catch (Exception) { return 37; }
        }
        public static int NestedCleanup(int number)
        {
            int result = 0;
            try
            {
                try { result = 100 / number; }
                finally
                {
                    try { result = unchecked(result * 10 + 1); }
                    finally { result = unchecked(result * 10 + 2); }
                    result = unchecked(result * 10 + 3);
                }
            }
            catch (DivideByZeroException) { result = unchecked(result * 10 + 4); }
            return result;
        }
        public static int Replacement(int number)
        {
            var original = new ProbeException("original");
            var replacement = new ProbeException("replacement");
            try
            {
                try { Throw(original); }
                finally { ValueObjectProbe.Pressure(5); if (number == 0) Throw(replacement); }
            }
            catch (ProbeException error)
            {
                ValueObjectProbe.Pressure(5);
                return error == original ? error.Message.Length : error == replacement ? error.Message.Length + 100 : -1;
            }
            return -2;
        }
        public static void Throw(Exception error) { throw error; }
        public static int Composed(int number)
        {
            var error = new ProbeException("A\0\ud800Z");
            var pair = GenericPair<int, Exception>.Create(number, error);
            object box = pair; pair = default; error = null;
            var call = new IntegerDelegate(DelegateProbe.Fault);
            int result = 0;
            try
            {
                try { result = call(number); Throw(((GenericPair<int, Exception>)box).Second); }
                finally { ValueObjectProbe.Pressure(10); result = unchecked(result + 5); }
            }
            catch (Exception caught) when (caught is ProbeException || caught is DivideByZeroException)
            {
                var copy = (GenericPair<int, Exception>)box;
                if (caught == copy.Second) result = unchecked(result + caught.Message.Length + copy.First);
                else result = unchecked(result + caught.Message.Length);
            }
            return result;
        }
        public static int Message(int number)
        {
            Exception error;
            if (number == 0) error = new DivideByZeroException(null);
            else if (number == 1) error = new ProbeException();
            else if (number == 2) error = new ProbeException(null);
            else error = new ProbeException("A\0\ud800Z");
            try { Throw(error); }
            catch (Exception caught)
            {
                ValueObjectProbe.Pressure(5);
                return caught.Message == error.Message ? caught.Message.Length : -1;
            }
            return -2;
        }
        public static int OrderedCatches(int number)
        {
            try { Throw(number == 0 ? null : new ProbeException()); }
            catch (DivideByZeroException) { return 1; }
            catch (Exception error) when (error is OverflowException) { return 2; }
            catch (ProbeException) { return 3; }
            catch (Exception) { return 4; }
            return -1;
        }
        public static int RuntimeFaults(int number)
        {
            try
            {
                if (number == 0) return checked(int.MaxValue + number + 1);
                if (number == 1) { DelegateReceiver receiver = null; return receiver.Number; }
                if (number == 2) { var array = new int[1]; return array[number]; }
                if (number == 3) { object value = new ProbeException(); return ((DelegateReceiver)value).Number; }
                if (number == 4) return DelegateProbe.NullCapture(number);
                return 100 / number;
            }
            catch (OverflowException error) { return error.Message.Length + 100; }
            catch (NullReferenceException error) { return error.Message.Length + 200; }
            catch (IndexOutOfRangeException error) { return error.Message.Length + 300; }
            catch (InvalidCastException error) { return error.Message.Length + 400; }
            catch (ArgumentException error) { return error.Message.Length + 500; }
        }
        public static int GenericCastMessage(int number)
        {
            object box = GenericPair<int, Exception>.Create(number, null);
            try { return ((GenericPair<int, ProbeException>)box).First; }
            catch (InvalidCastException error)
            {
                return error.Message == "Unable to cast object of type 'Cilica.Fixtures.Execution.GenericPair`2[System.Int32,System.Exception]' to type 'Cilica.Fixtures.Execution.GenericPair`2[System.Int32,Cilica.Fixtures.Execution.ProbeException]'." ? 1 : -1;
            }
        }
        public static int AllocationFailure(int number)
        {
            try { Throw(new ProbeException()); }
            catch (OutOfMemoryException) { return 77; }
            catch (ProbeException) { return number; }
            return -1;
        }
        public static int Unhandled(int number)
        {
            try { return DelegateProbe.Fault(number); }
            finally { ValueObjectProbe.Pressure(4); }
        }
        public static int InvalidConstructor(int number) { throw new BehavioralException(); }
        public static int InvalidOverride(int number) { throw new MessageOverrideException(); }
        public static int GeneralFilter(int number)
        {
            try { throw new ProbeException(); }
            catch (Exception) when (number == 0) { return 41; }
        }
    }
    public class BehavioralException : Exception
    {
        public BehavioralException() : base("behavior") { }
    }
    public class MessageOverrideException : Exception
    {
        public override string Message => "override";
    }

}
