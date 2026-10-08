#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public static class StackProbe
    {
        private static int Hash(string text) { int h=17;for(int i=0;i<text.Length;i++)h=unchecked(h*31+text[i]);return h; }
        public static int Integers(int a,int b)
        {
            var stack=new Stack<int>();int h=stack.Count;
            for(int i=0;i<80;i++) { stack.Push(unchecked(a+i*b));h=unchecked(h*31+stack.Count); }
            while(stack.Count!=0)h=unchecked(h*17+stack.Pop());return h;
        }
        public static int Reuse(int a,int b)
        {
            var stack=new Stack<int>();int h=17;
            for(int round=0;round<5;round++) { for(int i=0;i<17+round;i++)stack.Push(unchecked(a+i+b*round));while(stack.Count>0)h=unchecked(h*31+stack.Pop()); }
            return h+stack.Count;
        }
        public static int Wide(int a,int b)
        {
            var stack=new Stack<long>();for(int i=0;i<19;i++)stack.Push(unchecked(((long)a<<32)|(uint)(b+i)));
            int h=17;while(stack.Count>0){long value=stack.Pop();h=unchecked(h*31+(int)value+(int)(value>>32));}return h;
        }
        public static int Values(int a,int b)
        {
            var stack=new Stack<ValueTuple<int,int>>();for(int i=0;i<19;i++)stack.Push(new ValueTuple<int,int>(a+i,b-i));int h=17;
            while(stack.Count>0){var value=stack.Pop();h=unchecked(h*31+value.Item1*17+value.Item2);}return h;
        }
        public static int References(int a,int b)
        {
            var stack=new Stack<ProbeNode>();var shared=new ProbeNode(a);stack.Push(shared);stack.Push(shared);stack.Push(null);
            for(int i=0;i<19;i++)stack.Push(new ProbeNode(b+i));int h=17;
            while(stack.Count>0){var value=stack.Pop();h=unchecked(h*31+(value==null?13:value.Initial)+(value==shared?7:0));}return h;
        }
        public static int InlineReferences(int a,int b)
        {
            var stack=new Stack<ValueTuple<ProbeNode,int>>();for(int i=0;i<19;i++)stack.Push(new ValueTuple<ProbeNode,int>(new ProbeNode(a+i),b-i));int h=17;
            while(stack.Count>0){var value=stack.Pop();h=unchecked(h*31+value.Item1.Initial*17+value.Item2);}return h;
        }
        public static int Empty(int a,int b)
        {
            var stack=new Stack<int>();int h=0;
            for(int i=0;i<3;i++){try{h=stack.Pop();}catch(InvalidOperationException e){h=unchecked(h*31+Hash(e.Message));}stack.Push(a);h=unchecked(h*17+stack.Pop());}
            return h+stack.Count;
        }
        public static int Interfaces(int a,int b)
        {
            var stack=new Stack<int>();stack.Push(a);stack.Push(b);IReadOnlyCollection<int> read=stack;ICollection legacy=stack;
            int h=read.Count+legacy.Count+(legacy.IsSynchronized?13:17)+(legacy.SyncRoot==stack?31:37);
            return unchecked(h*31+stack.Pop()*17+stack.Pop());
        }
        public static Stack<ProbeNode> MakeTracked() { var stack=new Stack<ProbeNode>();for(int i=0;i<6;i++)stack.Push(new ProbeNode(i));return stack; }
        public static ProbeNode PopTracked(Stack<ProbeNode> stack)=>stack.Pop();
        public static int CountTracked(Stack<ProbeNode> stack)=>stack.Count;
        public static void PushNullTracked(Stack<ProbeNode> stack)=>stack.Push(null);
        public static int Lifetime(int a,int b) { var stack=MakeTracked();PushNullTracked(stack);PopTracked(stack);return CountTracked(stack); }
        public static int UnsupportedTraversal(int a,int b) { IEnumerable<int> stack=new Stack<int>();return stack.GetEnumerator().MoveNext()?1:0; }
        public static int UnsupportedCopy(int a,int b) { ICollection stack=new Stack<int>();stack.CopyTo(new int[1],0);return a; }
        public static int UnsupportedCapacity(int a,int b)=>new Stack<int>(a).Count;
    }
}
