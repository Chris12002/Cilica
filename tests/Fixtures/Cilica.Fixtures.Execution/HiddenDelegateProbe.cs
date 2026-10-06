using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    internal delegate int AssemblyCallback(int first, int second);
    public static class HiddenDelegateProbe
    {
        private delegate int Operation(int first, int second);
        private delegate ValueVectorBox Maker(int value);
        private delegate void Update(State state, int value);
        public sealed class State
        {
            public int Number;
            public State(int number) { Number = number; }
            public int Add(int first, int second) => unchecked(Number + first + second);
        }
        private static int Sum(int first, int second) => unchecked(first + second);
        private static ValueVectorBox Make(int value) => new ValueVectorBox(value);
        private static void Edit(State state, int value) { state.Number ^= value; }
        public static int Static(int a, int b) { Operation operation = Sum; return operation(a, b); }
        public static int Internal(int a, int b) { AssemblyCallback operation = Sum; return operation(a, b); }
        public static int Instance(int a, int b) { Operation operation = new State(a).Add; return operation(a, b); }
        public static int Array(int a, int b)
        {
            var values = new Operation[2]; values[0] = Sum; values[1] = new State(a).Add;
            return unchecked(values[0](a, b) + values[1](b, a));
        }
        public static int List(int a, int b) { var values = new List<Operation>(); values.Add(Sum); return values[0](a, b); }
        public static int Types(int a, int b)
        {
            Operation operation = Sum; object value = a < 0 ? null : a == 0 ? (object)new State(b) : operation;
            int result = value is Operation ? 3 : 7;
            try { var cast = (Operation)value; return unchecked(result + (cast == null ? 11 : cast(a, b))); }
            catch (InvalidCastException) { return result + 13; }
        }
        public static int Reference(int a, int b) { Maker make = Make; return unchecked(make(a).Number + make(b).Number); }
        public static int Void(int a, int b) { var state = new State(a); Update update = Edit; update(state, b); return state.Number; }
        public static int Fault(int a, int b)
        {
            var fault = new InvalidOperationException("private callback");
            Operation operation = (x, y) => { if (x < 0) throw fault; return unchecked(x + y); };
            try { return operation(a, b); } catch (InvalidOperationException e) { return e == fault ? e.Message.Length : -100; }
        }
        private static class Child
        {
            internal static class Grandchild { internal static int Run(int a, int b) { Operation operation = Sum; return operation(a, b); } }
        }
        public static int Deep(int a, int b) => Child.Grandchild.Run(a, b);
        public struct Payload { public int Number; public ValueVectorBox Reference; }
        private delegate Payload Transformer(Payload value);
        private static Payload Transform(Payload value) => new Payload { Number = unchecked(value.Number + 7), Reference = new ValueVectorBox(unchecked(value.Reference.Number + 3)) };
        public static int Value(int a, int b)
        {
            var value = new Payload { Number = a, Reference = new ValueVectorBox(b) }; Transformer transform = Transform;
            var result = transform(value);
            return unchecked(result.Number + result.Reference.Number + value.Number + value.Reference.Number);
        }
        private delegate T GenericOperation<T>(T value);
        private static T Identity<T>(T value) => value;
        public static int UnsupportedGeneric(int a, int b) { GenericOperation<int> operation = Identity; return operation(a); }
    }
}
