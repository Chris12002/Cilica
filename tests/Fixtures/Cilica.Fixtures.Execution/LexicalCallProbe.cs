using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class LexicalCallOwner
    {
        private readonly int number;
        private LexicalCallOwner(int number) { this.number = number; }
        private static int Add(int first, int second) => unchecked(first + second);
        private int Read(int other) => unchecked(number * 7 + other);
        private int IgnoreReceiver(int other) => other;
        private static T Identity<T>(T value) => value;
        private static void Update(ref int number, int other) { number ^= other; }
        private static int Throw(InvalidOperationException error) { throw error; }
        public static class Child
        {
            public static int Static(int a, int b) => Add(a, b);
            public static int Instance(int a, int b) => new LexicalCallOwner(a).Read(b);
            public static int Null(int a, int b)
            {
                LexicalCallOwner owner = a < 0 ? null : new LexicalCallOwner(a);
                try { return owner.IgnoreReceiver(b); } catch (NullReferenceException) { return unchecked(b + 31); }
            }
            public static int GenericMethod(int a, int b) => Add(Identity(a), b);
            public static int Borrow(int a, int b) { Update(ref a, b); return a; }
            public static int Fault(int a, int b)
            {
                var error = new InvalidOperationException(string.Concat("nested:", a < 0 ? "negative" : "positive"));
                try { return Throw(error); } catch (InvalidOperationException e) { return e == error ? unchecked(e.Message.Length + b) : -100; }
            }
            public static int Callback(int a, int b) { Func<int, int, int> callback = Add; return callback(a, b); }
            public static class Grandchild { public static int Deep(int a, int b) => Add(a, unchecked(b * 11)); }
        }
    }
    public static class GenericLexicalOwner<T>
    {
        private static int Read(int value) => value;
        public static class Child { public static int Run(int a, int b) => unchecked(Read(a) + b); }
    }
    public static class LexicalCallProbe
    {
        public static int Static(int a, int b) => LexicalCallOwner.Child.Static(a, b);
        public static int Instance(int a, int b) => LexicalCallOwner.Child.Instance(a, b);
        public static int Null(int a, int b) => LexicalCallOwner.Child.Null(a, b);
        public static int GenericMethod(int a, int b) => LexicalCallOwner.Child.GenericMethod(a, b);
        public static int Borrow(int a, int b) => LexicalCallOwner.Child.Borrow(a, b);
        public static int Fault(int a, int b) => LexicalCallOwner.Child.Fault(a, b);
        public static int Callback(int a, int b) => LexicalCallOwner.Child.Callback(a, b);
        public static int Deep(int a, int b) => LexicalCallOwner.Child.Grandchild.Deep(a, b);
        public static int EnumBorrow(int a, int b) => NestedEnumProbe.PrivateCall(a, b);
        public static int UnsupportedGenericOwner(int a, int b) => GenericLexicalOwner<int>.Child.Run(a, b);
    }
}
