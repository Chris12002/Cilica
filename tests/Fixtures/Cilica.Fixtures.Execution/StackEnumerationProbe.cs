using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public struct StackCursorValue { public SequenceBox Box;public int Number;public StackCursorValue(int value){Box=new SequenceBox(value);Number=value;} }
    public static class StackEnumerationProbe
    {
        private static int Message(string text) { int hash=17;for(int i=0;i<text.Length;i++)hash=unchecked(hash*31+text[i]);return hash; }
        public static int Concrete(int a,int b)
        {
            var stack=new Stack<int>();stack.Push(a);stack.Push(b);var e=stack.GetEnumerator();
            int result=e.Current;while(e.MoveNext())result=unchecked(result*31+e.Current);return unchecked(result*31+e.Current);
        }
        public static int Copies(int a,int b)
        {
            var stack=new Stack<int>();stack.Push(a);stack.Push(b);var first=stack.GetEnumerator();first.MoveNext();var second=first;
            first.MoveNext();int x=second.Current;second.Dispose();int y=second.Current;bool end=second.MoveNext();
            return unchecked(first.Current*31+x*7+y+(end?100:0)+second.Current);
        }
        public static int Interfaces(int a,int b)
        {
            var stack=new Stack<int>();stack.Push(a);stack.Push(b);IEnumerator e=((IEnumerable)stack).GetEnumerator();
            int result=(int)e.Current;e.MoveNext();object first=e.Current,second=e.Current;result=unchecked(result*31+(int)first+(ReferenceEquals(first,second)?100:0));
            e.Reset();result=unchecked(result*31+(int)e.Current);while(e.MoveNext())result=unchecked(result*31+(int)e.Current);
            ((IDisposable)e).Dispose();return unchecked(result*31+(int)e.Current);
        }
        public static int Generic(int a,int b)
        {
            var stack=new Stack<long>();stack.Push((long)a*10000000000L);stack.Push(b);long result=0;
            using(IEnumerator<long> e=((IEnumerable<long>)stack).GetEnumerator())while(e.MoveNext())result=unchecked(result*31+e.Current);
            return unchecked((int)(result^(result>>32)));
        }
        public static int Values(int a,int b)
        {
            var stack=new Stack<StackCursorValue>();stack.Push(new StackCursorValue(a));stack.Push(new StackCursorValue(b));
            var e=stack.GetEnumerator();e.MoveNext();var saved=e.Current;saved.Number=123;
            var allocated=new SequenceBox(a);int result=e.Current.Number+e.Current.Box.Number;
            e.MoveNext();return unchecked(result*31+e.Current.Box.Number+allocated.Number+saved.Box.Number);
        }
        public static int OfType(int a,int b)
        {
            var stack=new Stack<object>();stack.Push(new SequenceBox(a));stack.Push(null);stack.Push("text");stack.Push(new SequenceBox(b));
            int count=stack.OfType<SequenceBox>().Count();int result=0;
            foreach(var item in stack.OfType<SequenceBox>())result=unchecked(result*31+item.Number);
            return unchecked(result*31+count);
        }
        public static int Mutation(int a,int b)
        {
            var stack=new Stack<int>();stack.Push(a);IEnumerator<int> e=((IEnumerable<int>)stack).GetEnumerator();e.MoveNext();
            if((b&1)==0)stack.Push(b);else stack.Pop();int result=e.Current;
            try{e.MoveNext();return -100;}catch(InvalidOperationException ex){result=unchecked(result*31+Message(ex.Message));}
            try{((IEnumerator)e).Reset();return -101;}catch(InvalidOperationException ex){result=unchecked(result*31+Message(ex.Message));}
            e.Dispose();return unchecked(result*31+e.Current);
        }
        public static int EmptyConcrete(int a,int b)
        {
            var stack=new Stack<int>();var e=stack.GetEnumerator();int result=e.Current;
            try{stack.Pop();}catch(InvalidOperationException){result+=1;}
            if(e.MoveNext())return -100;result+=e.Current;
            stack.Push(a);try{e.MoveNext();return -101;}catch(InvalidOperationException){return result+2;}
        }
        public static int EmptyInterfaces(int a,int b)
        {
            var stack=new Stack<int>();IEnumerator<int> first=((IEnumerable<int>)stack).GetEnumerator();IEnumerator second=((IEnumerable)stack).GetEnumerator();
            IEnumerator<int> array=((IEnumerable<int>)Array.Empty<int>()).GetEnumerator();IEnumerator<int> list=((IEnumerable<int>)new List<int>()).GetEnumerator();
            int result=(ReferenceEquals(first,second)?1:0)+(ReferenceEquals(first,array)?2:0)+(ReferenceEquals(first,list)?4:0);
            second.Reset();try{int ignored=first.Current;return ignored-100;}catch(InvalidOperationException ex){result=unchecked(result*31+Message(ex.Message));}
            stack.Push(a);if(first.MoveNext())return -101;
            try{int ignored=first.Current;return ignored-102;}catch(InvalidOperationException ex){result=unchecked(result*31+Message(ex.Message));}
            first.Dispose();second.Reset();return result;
        }
        public static int DefaultCursor(int a,int b)
        {
            Stack<int>.Enumerator e=default;int result=e.Current;e.Dispose();
            try{e.MoveNext();return -100;}catch(NullReferenceException){result++;}
            IEnumerator box=e;try{box.Reset();return -101;}catch(NullReferenceException){return result+2;}
        }
    }
}
