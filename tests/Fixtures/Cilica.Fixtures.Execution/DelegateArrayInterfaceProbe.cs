using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class DelegateArrayInterfaceProbe
    {
        private delegate int Handler(int value);
        private sealed class State
        {
            public int Number;
            public int Add(int value) => unchecked(Number + value);
        }
        public static int Count(int a, int b)
        {
            var state = new State { Number = a }; Handler[] handlers = { state.Add, null };
            ICollection values = handlers; return unchecked(values.Count + handlers[0](b));
        }
        public static int Read(int a, int b)
        {
            var state = new State { Number = a }; IList<Handler> values = new Handler[] { state.Add };
            var handler = values[0]; state.Number = b; return handler(a);
        }
        public static int Copy(int a, int b)
        {
            var state = new State { Number = a }; ICollection<Func<int,int>> values = new Func<int,int>[] { state.Add };
            var copied = new Func<int,int>[2]; values.CopyTo(copied, 1); state.Number = b;
            return unchecked(copied[1](a) + (copied[0] == null ? 3 : 0));
        }
        public static int Clear(int a, int b)
        {
            var state = new State { Number = a }; Handler[] handlers = { state.Add };
            var saved = handlers[0]; IList values = handlers; values.Clear(); state.Number = b;
            return unchecked(saved(a) + (handlers[0] == null ? 5 : 0));
        }
    }
}
