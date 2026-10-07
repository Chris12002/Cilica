using System;
namespace Cilica.Fixtures.Execution
{
    public enum DefinedSparse { Negative = -7, Zero = 0, Alias = 0, High = 65536, Minimum = int.MinValue, Maximum = int.MaxValue }
    public enum DefinedOther { Zero = 0, One = 1 }
    public enum DefinedWide : long { Zero = 0 }
    public enum DefinedEmpty { }
    [Flags] public enum DefinedFlags { None = 0, One = 1, Two = 2, Eight = 8 }
    public static class EnumDefinedProbe
    {
        public static int Values(int a, int b) => Enum.IsDefined(typeof(DefinedSparse), (DefinedSparse)a) ? b : unchecked(b + 1);
        public static int Integers(int a, int b) => Enum.IsDefined(typeof(DefinedSparse), a) ? b : unchecked(b + 3);
        public static int Empty(int a, int b) => Enum.IsDefined(typeof(DefinedEmpty), (DefinedEmpty)a) ? b : unchecked(b + 5);
        public static int Flags(int a, int b) => Enum.IsDefined(typeof(DefinedFlags), (DefinedFlags)a) ? b : unchecked(b + 7);
        public static int Names(int a, int b)
        {
            string name = a < -3 ? "Negative" : a == -3 ? "Zero" : a == -2 ? "Alias" : a == -1 ? "High" : a == 0 ? "Minimum" : a == 1 ? "Maximum" : a == 2 ? "zero" : a == 3 ? "Zero, High" : a == 4 ? "0" : "";
            return Enum.IsDefined(typeof(DefinedSparse), name) ? b : unchecked(b + 11);
        }
        public static int Handles(int a, int b)
        {
            var handle = typeof(DefinedSparse).TypeHandle;
            var type = Type.GetTypeFromHandle(handle);
            return Enum.IsDefined(type, (DefinedSparse)a) ? b : unchecked(b + 13);
        }
        private static int Check(Type type, object value)
        {
            try { return Enum.IsDefined(type, value) ? 1 : 2; }
            catch (Exception e)
            {
                int hash = e is ArgumentNullException ? 17 : e is ArgumentException ? 19 : e is InvalidOperationException ? 23 : 29;
                string message = e.Message;
                for (int i = 0; i < message.Length; i++) hash = unchecked(hash * 31 + message[i]);
                var argument = e as ArgumentException;
                if (argument != null && argument.ParamName != null)
                    for (int i = 0; i < argument.ParamName.Length; i++) hash = unchecked(hash * 31 + argument.ParamName[i]);
                return hash;
            }
        }
        public static int NullOrder(int a, int b) => Check(a < 0 ? null : a == 0 ? typeof(int) : typeof(DefinedSparse), b < 0 ? null : (object)(DefinedSparse)b);
        public static int WrongEnum(int a, int b) => Check(a < 0 ? typeof(DefinedSparse) : typeof(DefinedOther), (DefinedOther)b);
        public static int WrongNumbers(int a, int b)
        {
            object value = a < -2 ? (object)(sbyte)b : a == -2 ? (object)(byte)b : a == -1 ? (object)(short)b : a == 0 ? (object)(ushort)b : a == 1 ? (object)(uint)b : a == 2 ? (object)(long)b : (object)(ulong)(long)b;
            return Check(typeof(DefinedSparse), value);
        }
        public static int UnsupportedObjects(int a, int b)
        {
            object value = a < 0 ? (object)(a < b) : a == 0 ? (object)(char)b : a == 1 ? (object)new ValueVectorBox(b) : new object();
            return Check(typeof(DefinedSparse), value);
        }
        public static int NonEnum(int a, int b) => Check(a < 0 ? typeof(int) : a == 0 ? typeof(object) : typeof(string), b < 0 ? null : (object)b);
        public static int UnsupportedWideEnum(int a, int b) => Enum.IsDefined(typeof(DefinedWide), (DefinedWide)a) ? a : b;
    }
}
