using System;
namespace Cilica.Fixtures.Execution
{
    public enum CompareLayer { Low = -1, Neutral = 0, High = 1 }
    public enum CompareOther { Zero = 0 }
    public enum CompareWide : long { Zero = 0 }
    public sealed class CompareLayerCell { public CompareLayer Value; }
    public static class EnumCompareProbe
    {
        public static int Values(int a, int b) { var value=(CompareLayer)a; return value.CompareTo((CompareLayer)b); }
        public static int Null(int a, int b) { var value=(CompareLayer)a; return value.CompareTo(null); }
        public static int Field(int a, int b)
        {
            var owner = new CompareLayerCell { Value=(CompareLayer)a };
            return owner.Value.CompareTo((CompareLayer)b);
        }
        public static CompareLayer Change(ref CompareLayer value, int b)
        {
            var pressure = new CompareLayerCell { Value=(CompareLayer)b }; value=(CompareLayer)unchecked(b+1); return pressure.Value;
        }
        public static int Evaluation(int a, int b) { var value=(CompareLayer)a; return value.CompareTo(Change(ref value,b)); }
        public static int NullField(int a, int b)
        {
            CompareLayerCell owner = null;
            try { return owner.Value.CompareTo((CompareLayer)b); }
            catch (NullReferenceException) { return unchecked(a+b); }
        }
        public static int ArgumentFaults(int a, int b)
        {
            var value=(CompareLayer)a;
            try { return value.CompareTo((CompareLayer)(a/b)); }
            catch (DivideByZeroException) { return 109; }
            catch (OverflowException) { return 113; }
        }
        public static int UnsupportedConstraint(int a, int b) => CompareClosed((CompareLayer)a,(CompareLayer)b);
        private static int CompareClosed<T>(T left,T right) where T:struct,Enum => left.CompareTo(right);
        public static int UnsupportedDifferent(int a, int b) { var value=(CompareLayer)a; return value.CompareTo((CompareOther)b); }
        public static int UnsupportedInteger(int a, int b) { var value=(CompareLayer)a; return value.CompareTo(b); }
        public static int UnsupportedAlias(int a, int b)
        {
            var value=(CompareLayer)a; object right=(CompareLayer)b;
            if (right==null) return 999; return value.CompareTo(right);
        }
        public static int UnsupportedWide(int a, int b) { var value=(CompareWide)a; return value.CompareTo((CompareWide)b); }
        public static int UnsupportedBoxed(int a, int b) { Enum value=(CompareLayer)a; return value.CompareTo((CompareLayer)b); }
    }
}
