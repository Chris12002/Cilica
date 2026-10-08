using System;
using System.Collections.Generic;
namespace Cilica.Fixtures.Execution
{
    public struct BoxedVariantSource : IVariantSource<VariantCat>
    {
        public VariantCat Item;
        public VariantCat Read() => Item;
    }
    public struct BoxedVariantSink : IVariantSink<VariantAnimal>
    {
        public int Total;
        public int Write(VariantAnimal value) { Total = unchecked(Total + value.Number); return Total; }
    }
    public struct BoxedVariantExplicit : IVariantSource<VariantCat>
    {
        public int Number;
        VariantCat IVariantSource<VariantCat>.Read() => new VariantCat { Number = Number };
    }
    public interface IBoxedVariantDerived : IVariantSource<VariantCat> { }
    public struct BoxedVariantInherited : IBoxedVariantDerived
    {
        public VariantCat Item;
        public VariantCat Read() => Item;
    }
    public struct BoxedVariantExact : IVariantSource<VariantAnimal>, IVariantSource<VariantCat>
    {
        public int Number;
        VariantAnimal IVariantSource<VariantAnimal>.Read() => new VariantAnimal { Number = Number };
        VariantCat IVariantSource<VariantCat>.Read() => new VariantCat { Number = 47 };
    }
    public struct BoxedVariantAllocating : IVariantSource<VariantCat>
    {
        public VariantAnimal Saved;
        public int Number;
        public VariantCat Read()
        {
            var result = new VariantCat { Number = unchecked(Number + Saved.Number) };
            ++Number;
            if (Saved.Number < 0) throw new ArgumentException("value");
            return result;
        }
    }
    public struct BoxedVariantAmbiguous : IVariantSource<VariantCat>, IVariantSource<VariantDog>
    {
        VariantCat IVariantSource<VariantCat>.Read() => new VariantCat();
        VariantDog IVariantSource<VariantDog>.Read() => new VariantDog();
    }
    public struct BoxedVariantGeneric : IVariantGeneric<VariantCat>
    {
        public VariantCat Read<T>(T value) => new VariantCat();
    }
    public static class BoxedVariantGapProbe
    {
        public static int Contravariant(int a, int b)
        {
            IVariantSink<VariantCat> sink = new BoxedVariantSink { Total = 1 };
            int first = sink.Write(new VariantCat { Number = a });
            int second = sink.Write(new VariantCat { Number = b });
            return unchecked(first * 31 + second);
        }
        public static int Explicit(int a, int b)
        {
            IVariantSource<VariantAnimal> source = new BoxedVariantExplicit { Number = a };
            return unchecked(source.Read().Number + b);
        }
        public static int Inherited(int a, int b)
        {
            IVariantSource<VariantAnimal> source = new BoxedVariantInherited { Item = new VariantCat { Number = a } };
            return unchecked(source.Read().Number + b);
        }
        public static int ExactWins(int a, int b)
        {
            IVariantSource<VariantAnimal> source = new BoxedVariantExact { Number = a };
            return unchecked(source.Read().Number + b);
        }
        public static int AllocatingFaults(int a, int b)
        {
            IVariantSource<VariantAnimal> source = new BoxedVariantAllocating { Number = b, Saved = new VariantAnimal { Number = a } };
            try { var first = source.Read(); var second = source.Read(); return unchecked(first.Number * 31 + second.Number); }
            catch (ArgumentException) { try { source.Read(); } catch (ArgumentException) { return 29; } return 0; }
        }
        public static int Null(int a, int b)
        {
            IVariantSource<VariantAnimal> source = a < 0 ? null : (IVariantSource<VariantAnimal>)new BoxedVariantSource { Item = new VariantCat { Number = b } };
            try { return source.Read().Number; } catch (NullReferenceException) { return 37; }
        }
        public static int PreparedListCopy(int a, int b)
        {
            var source = new List<VariantCat>(); source.Add(new VariantCat { Number = a }); source.Add(new VariantCat { Number = b });
            int first = 0;
            using (IEnumerator<VariantCat> cursor = source.GetEnumerator()) { if (cursor.MoveNext()) first = cursor.Current.Number; }
            var copy = new List<VariantAnimal>(source);
            return unchecked(first + copy.Count + copy[0].Number + copy[1].Number);
        }
        public static int PreparedReadOnlyCopy(int a, int b)
        {
            var source = new List<VariantCat>(); source.Add(new VariantCat { Number = a }); source.Add(new VariantCat { Number = b });
            IReadOnlyList<VariantCat> read = source.AsReadOnly(); int first = 0;
            using (IEnumerator<VariantCat> cursor = read.GetEnumerator()) { if (cursor.MoveNext()) first = cursor.Current.Number; }
            var copy = new List<VariantAnimal>(read);
            return unchecked(first + copy.Count + copy[0].Number + copy[1].Number);
        }
        public static int UnsupportedAmbiguous(int a, int b)
        {
            IVariantSource<VariantAnimal> source = new BoxedVariantAmbiguous(); return source.Read().Number;
        }
        public static int UnsupportedGeneric(int a, int b)
        {
            IVariantGeneric<VariantAnimal> source = new BoxedVariantGeneric(); return source.Read<int>(a).Number;
        }
        public static int Exact(int a, int b)
        {
            IVariantSource<VariantCat> source = new BoxedVariantSource { Item = new VariantCat { Number = a } };
            return unchecked(source.Read().Number + b);
        }
        public static int Covariant(int a, int b)
        {
            IVariantSource<VariantAnimal> source = new BoxedVariantSource { Item = new VariantCat { Number = a } };
            return unchecked(source.Read().Number + b);
        }
        public static int ListCopy(int a, int b)
        {
            var source = new List<VariantCat>(); source.Add(new VariantCat { Number = a }); source.Add(new VariantCat { Number = b });
            var copy = new List<VariantAnimal>(source);
            return unchecked(copy.Count + copy[0].Number + copy[1].Number);
        }
        public static int ReadOnlyCopy(int a, int b)
        {
            var source = new List<VariantCat>(); source.Add(new VariantCat { Number = a }); source.Add(new VariantCat { Number = b });
            IReadOnlyList<VariantCat> read = source.AsReadOnly();
            var copy = new List<VariantAnimal>(read);
            return unchecked(copy.Count + copy[0].Number + copy[1].Number);
        }
    }
}
