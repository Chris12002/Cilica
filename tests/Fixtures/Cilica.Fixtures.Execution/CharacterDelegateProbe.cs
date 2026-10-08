using System;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public sealed class CharacterDelegateReceiver
    {
        public int Seen;
        public char Expected;
        public char[] Values;
        public Func<char,bool> Back;
        public bool Check(char value)
        {
            var pressure = new CharacterDelegateReceiver { Expected = value };
            Seen = unchecked(Seen * 31 + pressure.Expected);
            return pressure.Expected == Expected;
        }
        public void Set(char value) { Expected = new CharacterDelegateReceiver { Expected = value }.Expected; }
        public bool Mutate(char value)
        {
            Seen++;
            Values[1] = Expected;
            var pressure = new CharacterDelegateReceiver { Expected = value };
            if (value == '\uffff') throw new InvalidOperationException("unit");
            return pressure.Expected == Expected;
        }
    }
    public static class CharacterDelegateProbe
    {
        public static bool Test(char value) => value >= '\u8000' || value == 'A';
        public static int Read(char value) => value;
        public static bool Same(char left, char right) => left == right;
        public static int AllUnits(int first, int unused)
        {
            var test = new Func<char,bool>(Test);
            var read = new Func<char,int>(Read);
            for (int i=0;i<32;i++)
            {
                char value=unchecked((char)(first+i));
                if (test(value) != (value >= '\u8000' || value == 'A') || read(value) != (first+i)) return i+1;
            }
            return 0;
        }
        public static int Capture(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)a), Seen=b };
            receiver.Back=new Func<char,bool>(receiver.Check);
            var callback=receiver.Back;
            bool first=callback(unchecked((char)a));
            var setter=new Action<char>(receiver.Set); setter(unchecked((char)b));
            bool second=callback(unchecked((char)a));
            return unchecked(receiver.Seen * 7 + (first?1:0) + (second?2:0) + receiver.Expected);
        }
        public static int Two(int a,int b)
        {
            var test=new Func<char,char,bool>(Same);
            var predicate=new Predicate<char>(Test);
            return (test(unchecked((char)a),unchecked((char)b))?1:0) + (predicate(unchecked((char)a))?2:0);
        }
        public static int LiveAny(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)b), Values=new char[]{unchecked((char)a),'x','y'} };
            receiver.Back=new Func<char,bool>(receiver.Mutate);
            try { return receiver.Values.Any(receiver.Back)?receiver.Seen*10+1:receiver.Seen*10; }
            catch(InvalidOperationException) { return receiver.Seen*10+2; }
        }
        // Independently test live callback reads while retaining LiveAny as a negative.
        public static int LiveLoop(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)b), Values=new char[]{unchecked((char)a),'x','y'} };
            receiver.Back=new Func<char,bool>(receiver.Mutate);
            try
            {
                for(int i=0;i<receiver.Values.Length;i++)
                    if(receiver.Back(receiver.Values[i])) return receiver.Seen*10+1;
                return receiver.Seen*10;
            }
            catch(InvalidOperationException) { return receiver.Seen*10+2; }
        }
        public static Func<char,bool> Make(char expected)
        {
            var receiver=new CharacterDelegateReceiver { Expected=expected };
            receiver.Back=new Func<char,bool>(receiver.Check);
            return receiver.Back;
        }
        public static int SoleReceiver(int a,int b)
        {
            var callback=Make(unchecked((char)a));
            var pressure=Make(unchecked((char)b));
            bool first=callback(unchecked((char)a));
            bool second=callback(unchecked((char)b));
            return (first?1:0)+(second?2:0)+(pressure(unchecked((char)b))?4:0);
        }
        public static char Argument(ref int trace,int value)
        {
            trace=unchecked(trace*10+1);
            if(value==1) throw new InvalidOperationException("argument");
            return unchecked((char)value);
        }
        public static int Faults(int a,int b)
        {
            var receiver=new CharacterDelegateReceiver { Expected=unchecked((char)a) };
            Func<char,bool> callback=a==0?null:new Func<char,bool>(receiver.Check);
            int trace=b;
            try { bool answer=callback(Argument(ref trace,b));return unchecked(trace*10+(answer?1:0)); }
            catch(NullReferenceException) { return unchecked(trace*10+2); }
            catch(InvalidOperationException) { return unchecked(trace*10+3); }
        }
        public static int NullTarget(int a,int b)
        {
            CharacterDelegateReceiver receiver=null;
            try { var callback=new Func<char,bool>(receiver.Check);return callback(unchecked((char)a))?1:0; }
            catch(ArgumentException) { return unchecked(a+b); }
        }
        public static bool ByteIdentity(byte value)=>value != 0;
        public static bool UShortIdentity(ushort value)=>value != 0;
        public static char CharIdentity(char value)=>value;
        public static int UnsupportedByte(int a,int b)=>new Func<byte,bool>(ByteIdentity)(unchecked((byte)a))?1:0;
        public static int UnsupportedUShort(int a,int b)=>new Func<ushort,bool>(UShortIdentity)(unchecked((ushort)a))?1:0;
        public static int UnsupportedResult(int a,int b)=>new Func<char,char>(CharIdentity)(unchecked((char)a));
    }
}
