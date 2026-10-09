using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public interface IFinalDelegateValue { int Read(int value); }
    public class FinalDelegateValue : IFinalDelegateValue
    {
        public int Number;
        public FinalDelegateValue(int number) { Number = number; }
        public int Read(int value) { var pressure = new HeapPayload(value); return unchecked(Number + pressure.Number); }
    }
    public class FinalDelegateChild : FinalDelegateValue
    {
        public FinalDelegateChild(int number) : base(number) { }
        public new int Read(int value) => unchecked(value - Number);
    }
    public class OpenDelegateValue
    {
        public virtual int Read(int value) => value;
    }
    public class SealedDelegateValue : OpenDelegateValue
    {
        public sealed override int Read(int value) => unchecked(value + 17);
    }
    public static class FinalDelegateProbe
    {
        public static int Set(int a, int b)
        {
            var set = new HashSet<int>(); set.Add(a); Func<int,bool> includes = set.Contains;
            var pressure = new HeapPayload(b); return includes(pressure.Number) ? 13 : 17;
        }
        public static int ValueSet(int a, int b)
        {
            var set = new HashSet<Guid>(); set.Add(new Guid(a,0,0,0,0,0,0,0,0,0,0));
            Func<Guid,bool> includes = set.Contains; var pressure = new HeapPayload(b);
            return includes(new Guid(pressure.Number,0,0,0,0,0,0,0,0,0,0)) ? 47 : 53;
        }
        public static int Composed(int a, int b)
        {
            var set = new HashSet<int>(); set.Add(a); set.Add(b);
            return new[] {a,b,unchecked(a+b)}.Where(set.Contains).Count();
        }
        public static int FinalMember(int a, int b)
        {
            var receiver = new FinalDelegateValue(a); Func<int,int> read = receiver.Read;
            receiver.Number = b; var pressure = new HeapPayload(a); return read(pressure.Number);
        }
        public static int Hidden(int a, int b)
        {
            FinalDelegateValue receiver = new FinalDelegateChild(a); Func<int,int> read = receiver.Read; return read(b);
        }
        public static int List(int a, int b) { var list = new List<int>(); list.Add(a); Predicate<int> contains = list.Contains; return contains(b) ? 41 : 43; }
        public static int Sealed(int a, int b)
        { var receiver = new SealedDelegateValue(); Func<int,int> read = receiver.Read; return read(unchecked(a+b)); }
        public static int Null(int a, int b)
        {
            FinalDelegateValue receiver = a < 0 ? null : new FinalDelegateValue(a);
            try { Func<int,int> read = receiver.Read; return read(a/b); }
            catch (NullReferenceException) { return 23; } catch (ArgumentException) { return 29; }
            catch (DivideByZeroException) { return 31; } catch (OverflowException) { return 37; }
        }
        public static int Overridable(int a,int b) { var receiver = new OpenDelegateValue(); Func<int,int> read = receiver.Read; return read(b); }
        public static int ThroughInterface(int a,int b) { IFinalDelegateValue receiver = new FinalDelegateValue(a); Func<int,int> read = receiver.Read; return read(b); }
    }
}
