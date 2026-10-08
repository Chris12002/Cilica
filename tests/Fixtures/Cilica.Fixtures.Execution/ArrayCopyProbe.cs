using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public class CopyBase { public int Number; public CopyBase(int number) { Number = number; } }
    public sealed class CopyLeft : CopyBase { public CopyLeft(int number) : base(number) { } }
    public sealed class CopyRight : CopyBase { public CopyRight(int number) : base(number) { } }
    public interface ICopyFirst { }
    public interface ICopySecond { }
    public sealed class CopyBoth : ICopyFirst, ICopySecond { }
    public sealed class CopyFirstOnly : ICopyFirst { }
    public static class ArrayCopyProbe
    {
        private static int Hash(string text) { int hash = 17; for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]); return hash; }
        public static int Integers(int a, int b)
        {
            int[] source = new int[b & 3]; for (int i = 0; i < source.Length; i++) source[i] = unchecked(a + i);
            int[] target = b < 0 ? null : new int[3]; int result = 0;
            try { ((ICollection<int>)source).CopyTo(target, a); result = 3; }
            catch (ArgumentException e) { result = Hash(e.Message) + Hash(e.ParamName); }
            if (target != null) for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + target[i]);
            return result;
        }
        public static int Lists(int a, int b)
        {
            var list = new List<ValueVectorBox>(); var value = new ValueVectorBox(a); list.Add(value); list.Add(null);
            var target = new ValueVectorBox[3]; ((ICollection<ValueVectorBox>)list).CopyTo(target, 1); list.Clear(); value.Number = b;
            return target[1].Number + (target[1] == value && target[2] == null ? 7 : -100);
        }
        public static int Values(int a, int b)
        {
            ValueVectorCell value = default; value.Number = a; value.Wide = ((long)a << 32) + b; value.Reference = new ValueVectorBox(b);
            var source = new[] { value }; var target = new ValueVectorCell[2]; ((ICollection<ValueVectorCell>)source).CopyTo(target, 1); source[0] = default;
            return unchecked(target[1].Number + (int)(target[1].Wide >> 32) + target[1].Reference.Number + (target[1].Reference == value.Reference ? 7 : -100));
        }
        public static int EmptyValues(int a, int b)
        {
            var source = new ValueVectorEmpty[b & 3]; var target = new ValueVectorEmpty[4];
            ((ICollection<ValueVectorEmpty>)source).CopyTo(target, 1); return source.Length + target.Length + a;
        }
        public static int CovariantSources(int a, int b)
        {
            CopyBase[] source = new CopyLeft[a & 1]; if ((a & 1) != 0) source[0] = new CopyLeft(a);
            CopyBase[] target = b < 0 ? null : new CopyRight[b & 1]; int result = 0;
            try { ((ICollection<CopyBase>)source).CopyTo(target, a); result = 3; }
            catch (ArgumentException e) { result = Hash(e.Message); }
            catch (ArrayTypeMismatchException e) { result = Hash(e.Message); }
            return result;
        }
        public static int Downcast(int a, int b)
        {
            var source = new object[2]; source[0] = "first"; source[1] = b < 0 ? new object() : "second";
            var target = new string[4]; int result = 0;
            try { ((ICollection<object>)source).CopyTo(target, (int)((uint)a % 3)); result = 3; }
            catch (InvalidCastException e) { result = Hash(e.Message); }
            catch (ArgumentException e) { result = Hash(e.Message); }
            for (int i = 0; i < target.Length; i++) result = unchecked(result * 7 + (target[i] == null ? 0 : target[i].Length));
            return result;
        }
        public static int Interfaces(int a, int b)
        {
            var source = new ICopyFirst[2]; source[0] = new CopyBoth(); source[1] = b < 0 ? (ICopyFirst)new CopyFirstOnly() : new CopyBoth();
            var target = new ICopySecond[2]; int result = 0;
            try { ((ICollection<object>)source).CopyTo(target, 0); result = 3; }
            catch (InvalidCastException e) { result = Hash(e.Message); }
            return unchecked(result + (target[0] == (object)source[0] ? 7 : 0) + (target[1] == (object)source[1] ? 11 : 0));
        }
        public static int Self(int a, int b)
        {
            var source = new[] { a, b }; ((ICollection<int>)source).CopyTo(source, 0); int result = unchecked(source[0] * 7 + source[1]);
            try { ((ICollection<int>)source).CopyTo(source, 1); } catch (ArgumentException e) { result = unchecked(result + Hash(e.Message)); }
            return result;
        }
        public static int EmptyList(int a, int b)
        {
            var list = new List<CopyBase>(); CopyBase[] target = b < 0 ? null : new CopyRight[b & 1];
            try { ((ICollection<CopyBase>)list).CopyTo(target, a & 1); return list.Count + 3; }
            catch (ArgumentException e) { return Hash(e.Message); }
        }
        public static int Unsigned(int a, int b)
        {
            uint[] source = { unchecked((uint)a), 0x80000000u, uint.MaxValue };
            uint[] destination = b < 0 ? null : new uint[] { 7, 11, 13, 17 };
            int result = 19;
            try { ((ICollection<uint>)source).CopyTo(destination, b); }
            catch (ArgumentException e) { result = Hash(e.Message); }
            if (destination != null) for (int i = 0; i < destination.Length; ++i)
                result = unchecked(result * 31 + (int)destination[i]);
            return result;
        }
    }
}
