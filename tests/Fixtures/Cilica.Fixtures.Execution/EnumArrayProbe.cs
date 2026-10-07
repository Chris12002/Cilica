using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public enum ArrayEnum { Zero, One, Two }
    public enum OtherArrayEnum { Zero, One, Two }
    internal enum HiddenArrayEnum { Zero, One }
    public sealed class EnumArrayOwner { public ArrayEnum[] Values; public EnumArrayOwner(int value) { Values = new ArrayEnum[] { (ArrayEnum)value }; } }
    public enum SmallArrayEnum : byte { Zero, One }
    public static class EnumArrayProbe
    {
        private static readonly ArrayEnum[] Order = { ArrayEnum.One, ArrayEnum.Two, ArrayEnum.Zero, (ArrayEnum)(-1) };
        public static int Direct(int a, int b) { var data = new ArrayEnum[3]; data[0] = (ArrayEnum)a; data[1] = (ArrayEnum)b; return unchecked((int)data[0] + (int)data[1] + (int)data[2]); }
        public static int Alias(int a, int b) {
            object original = new ArrayEnum[] {(ArrayEnum)a, (ArrayEnum)b};
            var integers = (int[])original; var other = (OtherArrayEnum[])original;
            integers[0] = b; other[1] = (OtherArrayEnum)a;
            object reverse = new int[] {a, b}; ((ArrayEnum[])reverse)[0] = (ArrayEnum)b;
            return unchecked((int)((ArrayEnum[])original)[0] + (int)other[1] + ((int[])reverse)[0] + (original is OtherArrayEnum[] ? 7 : -100));
        }
        public static int Empty(int a, int b) { var one=Array.Empty<ArrayEnum>(); var two=Array.Empty<OtherArrayEnum>(); return (ReferenceEquals(one,Array.Empty<ArrayEnum>()) ? 1 : -100) + (ReferenceEquals(one,two) ? -100 : 3) + ((object)one is int[] ? 7 : -100); }
        public static int Constant(int a, int b) { var values=new ArrayEnum[] {ArrayEnum.One,ArrayEnum.Two,(ArrayEnum)127,(ArrayEnum)int.MinValue,(ArrayEnum)int.MaxValue}; return (int)values[(a & 3)] ^ (int)values[4]; }
        public static int Field(int a, int b) { var first=new EnumArrayOwner(a); var second=new EnumArrayOwner(b); return unchecked((int)first.Values[0] + (int)second.Values[0]); }
        public static int Faults(int a, int b) { ArrayEnum[] values = a < 0 ? null : new ArrayEnum[1]; try { values[b] = (ArrayEnum)(100 / ((a & 1) + 1)); return (int)values[0]; } catch(NullReferenceException) { return 13; } catch(IndexOutOfRangeException) { return 17; } }
        public static int Identity(int a, int b) { object one=new ArrayEnum[0]; object two=new OtherArrayEnum[0]; object integers=new int[0]; return (one.GetType() == two.GetType() ? -100 : 1) + (one.GetType() == integers.GetType() ? -100 : 3); }
        public static int Hidden(int a, int b) { var values=new HiddenArrayEnum[] {(HiddenArrayEnum)a,(HiddenArrayEnum)b}; return unchecked((int)values[0]+(int)values[1]); }
        public static int Static(int a, int b) => (int)Order[a & 3];
        public static int CastFault(int a, int b) { object values=new ArrayEnum[1]; try { return ((char[])values)[0]; } catch(InvalidCastException e) { return e.Message.Length; } }
        public static int Views(int a, int b) { object values=new ArrayEnum[1]; return (values is IList<int> ? 1 : -100) + (values is IReadOnlyList<int> ? 3 : -100) + (values is byte[] ? -100 : 7); }
        public static int UnsupportedWidth(int a, int b) => (int)(new SmallArrayEnum[1])[0];
        public static int UnsupportedInterface(int a, int b) { IList<ArrayEnum> values=new ArrayEnum[1]; return (int)values[0]; }
        public static int UnsupportedIntegerView(int a, int b) { IList<int> values=(IList<int>)(object)new ArrayEnum[1]; return values[0]; }
        public static int UnsupportedUnsigned(int a, int b) { var values=(uint[])(object)new ArrayEnum[1]; return (int)values[0]; }
    }
}
