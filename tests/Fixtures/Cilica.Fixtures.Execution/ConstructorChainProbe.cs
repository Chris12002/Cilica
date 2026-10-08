#nullable disable
using System;
namespace Cilica.Fixtures.Execution
{
    public sealed class ChainNode { public int Value; public ChainNode(int value){Value=value;} }
    public struct ChainCell { public ChainNode Reference; public long Wide; }
    public static class ChainTrace
    {
        public static int Value;
        public static int Mark(int mark){Value=unchecked(Value*10+mark);return mark;}
        public static ChainNode Node(int value,int mark){Mark(mark);if(value==-1)throw new ArgumentException("argument");return new ChainNode(value);}
    }
    public class ChainParent
    {
        public readonly ChainNode ParentNode;
        public ChainParent(ChainNode node,int mode){ChainTrace.Mark(4);if(mode==-2)throw new InvalidOperationException("base");ParentNode=node;}
    }
    public sealed class ChainChild:ChainParent
    {
        public readonly ChainNode Other;
        public readonly int Initial=ChainTrace.Mark(3);
        public readonly int Result;
        public ChainChild(int a,int b):this(ChainTrace.Node(a,1),ChainTrace.Node(b,2),b){ChainTrace.Mark(6);Result=unchecked(Result+a);}
        private ChainChild(ChainNode first,ChainNode second,int mode):base(first,mode)
        {ChainTrace.Mark(5);Other=second;Result=unchecked(first.Value*31+second.Value);if(mode==-3)throw new InvalidOperationException("inner");}
    }
    public sealed class ChainBox<T>
    {
        public T[] Values;
        public ChainBox(T value):this(new[]{value},1){}
        public ChainBox(T[] values,int length){Values=new T[length];Array.Copy(values,Values,length);}
    }
    public sealed class ChainBranch
    {
        public readonly int Number;
        public ChainBranch(int value):this(value,0,0){Number=unchecked(Number+3);}
        private ChainBranch(int value,int unused,int other){Number=value;}
        public ChainBranch(int value,int mode):this(value){if(mode<0)Number=unchecked(Number+17);else Number=unchecked(Number+31);}
    }
    public static class ConstructorChainProbe
    {
        public static int Trace(int a,int b)
        {
            ChainTrace.Value=0;int result=0;
            try{var c=new ChainChild(a,b);result=unchecked(c.Result+c.Initial*7+c.ParentNode.Value+c.Other.Value);}
            catch(ArgumentException e){result=e.Message.Length*31;}
            catch(InvalidOperationException e){result=e.Message.Length*103;}
            return unchecked(result+ChainTrace.Value*17);
        }
        public static int Recovery(int a,int b)
        {
            int first=Trace(a,b);var second=new ChainChild(11,13);
            return unchecked(first*31+second.Other.Value+ChainTrace.Value);
        }
        public static int Values(int a,int b)
        {
            var value=new ChainCell{Reference=new ChainNode(a),Wide=long.MinValue+b};var box=new ChainBox<ChainCell>(value);
            value.Wide=0;var extra=new ChainNode(b);return unchecked(box.Values[0].Reference.Value*31+(int)(box.Values[0].Wide>>32)+extra.Value);
        }
        public static int References(int a,int b)
        {
            var value=new ChainNode(a);var box=new ChainBox<ChainNode>(value);value.Value=b;
            return unchecked(box.Values[0].Value*31+(box.Values[0]==value?7:0));
        }
        public static int Branches(int a,int b){return new ChainBranch(a,b).Number;}
        public static int ClosedOwners(int a,int b)
        {
            var first=new ChainBox<int>(a);var second=new ChainBox<uint>(unchecked((uint)b));
            return unchecked(first.Values[0]*31+(int)second.Values[0]);
        }
        public static ChainBox<ChainNode> Create(){return new ChainBox<ChainNode>(new ChainNode(17));}
        public static int Retained(int a,int b){var box=Create();return box.Values[0].Value;}
    }
}
