using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public sealed class StackCursorQuotaState
    {
        public Stack<int> Stack;public IEnumerator<int> Cursor;
        public StackCursorQuotaState(Stack<int> stack,IEnumerator<int> cursor){Stack=stack;Cursor=cursor;}
    }
    public static class StackCursorQuotaProbe
    {
        public static StackCursorQuotaState Make()
        {var stack=new Stack<int>();for(int i=0;i<8;i++)stack.Push(i);return new StackCursorQuotaState(stack,((IEnumerable<int>)stack).GetEnumerator());}
        public static void Push(StackCursorQuotaState state)=>state.Stack.Push(8);
        public static int Count(StackCursorQuotaState state)=>state.Stack.Count;
        public static int Read(StackCursorQuotaState state)=>state.Cursor.MoveNext()?state.Cursor.Current:-1;
        public static int Rejected(StackCursorQuotaState state)
        {try{state.Cursor.MoveNext();return 0;}catch(InvalidOperationException){return 1;}}
        public static int Run(int a,int b)
        {var state=Make();int value=Read(state);Push(state);return value+Count(state)+Rejected(state);}
    }
}
