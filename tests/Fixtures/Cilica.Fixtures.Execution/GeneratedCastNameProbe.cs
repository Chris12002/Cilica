using System;
namespace Cilica.Fixtures.Execution
{
    public class CastNameOuter
    {
        public class Nested { }
        public struct NestedValue { public int Number; }
        public class Generic<T> { }
    }
    public class CastNameBox<T> { }
    public static class GeneratedCastNameProbe
    {
        private static int MessageHash(string message,int seed)
        { for(int i=0;i<message.Length;i++) seed=unchecked(seed*31+message[i]); return seed; }
        public static int NestedSource(int a,int b)
        {
            object value=new CastNameOuter.Nested();
            try { return ((ValueVectorBox)value).Number; }
            catch(InvalidCastException e) { return MessageHash(e.Message,b); }
        }
        public static int NestedTarget(int a,int b)
        {
            object value=new ValueVectorBox(a);
            try { return ((CastNameOuter.Nested)value)==null?0:1; }
            catch(InvalidCastException e) { return MessageHash(e.Message,b); }
        }
        public static int NestedArgument(int a,int b)
        {
            object value=new CastNameBox<CastNameOuter.Nested>();
            try { return ((CastNameBox<CastNameOuter.Generic<int>>)value)==null?0:1; }
            catch(InvalidCastException e) { return MessageHash(e.Message,b); }
        }
        public static int NestedGeneric(int a,int b)
        {
            object value=new CastNameOuter.Generic<CastNameOuter.Nested>();
            try { return ((CastNameOuter.Generic<int>)value)==null?0:1; }
            catch(InvalidCastException e) { return MessageHash(e.Message,b); }
        }
        public static int NestedArray(int a,int b)
        {
            object value=new CastNameOuter.Nested[1];
            try { return ((CastNameOuter.Generic<int>[])value).Length; }
            catch(InvalidCastException e) { return MessageHash(e.Message,b); }
        }
        public static int NestedBox(int a,int b)
        {
            object value=new CastNameOuter.NestedValue {Number=a};
            try { return ((ValueVectorCell)value).Number; }
            catch(InvalidCastException e) { return MessageHash(e.Message,b); }
        }
        public static int Text(int value, int mode)
        {
            object instance = new { Number = value };
            try { return ((ValueVectorBox)instance).Number; }
            catch (InvalidCastException error) {
                string message = error.Message; int hash = mode;
                for (int i = 0; i < message.Length; ++i) hash = unchecked(hash * 31 + message[i]);
                return hash;
            }
        }
    }
}
