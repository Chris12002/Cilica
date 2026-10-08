using System;
using System.Globalization;
using Cilica.Library;
namespace Cilica.Fixtures.Execution
{
    public static class DecimalProbe
    {
        public static decimal Operand(int index)
        {
            if (index == 0) return new decimal(0,0,0,true,28);
            if (index == 1) return new decimal(1,0,0,false,28);
            if (index == 2) return new decimal(5,0,0,true,28);
            if (index == 3) return new decimal(-1,-1,-1,false,0);
            if (index == 4) return new decimal(-1,-1,-1,true,28);
            if (index == 5) return new decimal(25,0,0,false,1);
            if (index == 6) return new decimal(15,0,0,false,28);
            if (index == 7) return new decimal(0,-1,0,false,9);
            if (index == 8) return new decimal(1,0,1,false,1);
            if (index == 9) return new decimal(0,0,1,true,28);
            if (index == 10) return new decimal(100,0,0,false,2);
            if (index == 11) return new decimal(-2,-1,-1,false,1);
            if (index == 12) return new decimal(0,0,0,false,0);
            if (index == 13) return new decimal(3);
            if (index == 14) return new decimal(2);
            if (index == 15) return new decimal(-10);
            if (index == 16) return new decimal(int.MinValue);
            if (index == 17) return new decimal(int.MaxValue);
            if (index == 18) return new decimal(5,0,0,false,1);
            if (index == 19) return new decimal(1,0,0,true,1);
            if (index == 20) return new decimal(99,0,0,false,0);
            if (index == 21) return new decimal(9,0,0,false,28);
            if (index == 22) return new decimal(-1,-1,-1,false,28);
            return new decimal(10,0,0,false,28);
        }
        private static int Observe(string text,int position) => position < 0 ? text.Length : position < text.Length ? text[position] : -1;
        public static int Multiply(int index,int position)
        {
            try { return ObserveDecimal(Operand(index % 24) * Operand((index / 24) % 24),position); }
            catch(OverflowException) {return -100;}
        }
        public static int Divide(int index,int position)
        {
            try { return ObserveDecimal(Operand(index % 24) / Operand((index / 24) % 24),position); }
            catch(OverflowException) {return -100;} catch(DivideByZeroException) {return -101;}
        }
        public static int Route(int first,int second)
        {
            decimal value = (first - second) * (decimal)second / 2;
            string text = value.ToString(CultureInfo.InvariantCulture); int hash=17;
            for(int i=0;i<text.Length;i++) hash=unchecked(hash*31+text[i]);
            return hash;
        }
        public static int IntegerMultiply(int first,int second) => Observe(((decimal)first * second).ToString(CultureInfo.InvariantCulture),first & 31);
        public static int IntegerDivide(int first,int second)
        {
            try { return Observe(((decimal)first / second).ToString(CultureInfo.InvariantCulture),first & 31); }
            catch(DivideByZeroException e) { return e.Message.Length; }
        }
        public static int CopyAndRepeat(int first,int second)
        {
            var value=(decimal)first / 3; var copy=value;
            value=value*(decimal)second;
            string a=copy.ToString(CultureInfo.InvariantCulture), b=copy.ToString(CultureInfo.InvariantCulture);
            return (ReferenceEquals(a,b)?1:0)+a.Length*31+value.ToString(CultureInfo.InvariantCulture).Length;
        }
        public static DecimalParts LibraryOperand(int index)
        {
            if (index == 0) return new DecimalParts(0,0,0,true,28);
            if (index == 1) return new DecimalParts(1,0,0,false,28);
            if (index == 2) return new DecimalParts(5,0,0,true,28);
            if (index == 3) return new DecimalParts(4294967295U,4294967295U,4294967295U,false,0);
            if (index == 4) return new DecimalParts(4294967295U,4294967295U,4294967295U,true,28);
            if (index == 5) return new DecimalParts(25,0,0,false,1);
            if (index == 6) return new DecimalParts(15,0,0,false,28);
            if (index == 7) return new DecimalParts(0,4294967295U,0,false,9);
            if (index == 8) return new DecimalParts(1,0,1,false,1);
            if (index == 9) return new DecimalParts(0,0,1,true,28);
            if (index == 10) return new DecimalParts(100,0,0,false,2);
            if (index == 11) return new DecimalParts(4294967294U,4294967295U,4294967295U,false,1);
            if (index == 12) return new DecimalParts(0,0,0,false,0);
            if (index == 13) return DecimalArithmetic.FromInt32(3);
            if (index == 14) return DecimalArithmetic.FromInt32(2);
            if (index == 15) return DecimalArithmetic.FromInt32(-10);
            if (index == 16) return DecimalArithmetic.FromInt32(int.MinValue);
            if (index == 17) return DecimalArithmetic.FromInt32(int.MaxValue);
            if (index == 18) return new DecimalParts(5,0,0,false,1);
            if (index == 19) return new DecimalParts(1,0,0,true,1);
            if (index == 20) return new DecimalParts(99,0,0,false,0);
            if (index == 21) return new DecimalParts(9,0,0,false,28);
            if (index == 22) return new DecimalParts(4294967295U,4294967295U,4294967295U,false,28);
            return new DecimalParts(10,0,0,false,28);
        }
        public static int LibraryMultiply(int index,int position)
        {
            try { return ObserveParts(DecimalArithmetic.Multiply(LibraryOperand(index%24),LibraryOperand((index/24)%24)),position); }
            catch(OverflowException) {return -100;}
        }
        public static int LibraryDivide(int index,int position)
        {
            try { return ObserveParts(DecimalArithmetic.Divide(LibraryOperand(index%24),LibraryOperand((index/24)%24)),position); }
            catch(OverflowException) {return -100;} catch(DivideByZeroException) {return -101;}
        }
        private static int ObserveDecimal(decimal value,int position)
        { return position < -1 ? decimal.GetBits(value)[position+5] : Observe(value.ToString(CultureInfo.InvariantCulture),position); }
        private static int ObserveParts(DecimalParts value,int position)
        {
            if(position == -5)return unchecked((int)value.Low);
            if(position == -4)return unchecked((int)value.Mid);
            if(position == -3)return unchecked((int)value.High);
            if(position == -2)return value.Flags;
            return Observe(DecimalArithmetic.InvariantText(value),position);
        }
        public static int FaultText(int first,int second)
        {
            try {
                decimal value=first==0 ? ((decimal)(first+1)/second) : ((decimal)first*2147483647*2147483647*2147483647*2147483647);
                return value.ToString(CultureInfo.InvariantCulture).Length;
            }
            catch(Exception e) {return Observe(e.Message,second);}
        }
        public static int UnsupportedConstant(int first,int second) => decimal.One.ToString(CultureInfo.InvariantCulture).Length;
        public static int UnsupportedParts(int first,int second) => new decimal(first,second,0,false,0).ToString(CultureInfo.InvariantCulture).Length;
        public static int UnsupportedDefault(int first,int second) => ((decimal)first).ToString().Length;
        public static int UnsupportedNull(int first,int second) => ((decimal)first).ToString((IFormatProvider)null).Length;
        public static int UnsupportedAddition(int first,int second) => (((decimal)first)+((decimal)second)).ToString(CultureInfo.InvariantCulture).Length;
    }
}
