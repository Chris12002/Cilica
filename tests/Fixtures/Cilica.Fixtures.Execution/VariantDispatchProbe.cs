using System;
using System.Collections.Generic;
using System.Linq;
namespace Cilica.Fixtures.Execution
{
    public class VariantAnimal { public int Number; }
    public sealed class VariantCat : VariantAnimal { }
    public sealed class VariantDog : VariantAnimal { }
    public interface IVariantSource<out T> { T Read(); }
    public interface IVariantSink<in T> { int Write(T value); }
    public class VariantSource : IVariantSource<VariantCat>
    {
        public VariantCat Item;
        public virtual VariantCat Read() => Item;
    }
    public sealed class VariantOverride : VariantSource
    {
        public override VariantCat Read() => new VariantCat { Number = unchecked(Item.Number + 7) };
    }
    public sealed class VariantExplicit : IVariantSource<VariantCat>
    {
        public VariantCat Item;
        VariantCat IVariantSource<VariantCat>.Read() => Item;
    }
    public sealed class VariantSink : IVariantSink<VariantAnimal>
    {
        public int Write(VariantAnimal value) => value.Number;
    }
    public sealed class VariantExact : IVariantSource<VariantCat>, IVariantSource<VariantAnimal>
    {
        public int Number;
        VariantCat IVariantSource<VariantCat>.Read() => new VariantCat { Number = 1 };
        VariantAnimal IVariantSource<VariantAnimal>.Read() => new VariantAnimal { Number = Number };
    }
    public sealed class VariantAmbiguous : IVariantSource<VariantCat>, IVariantSource<VariantDog>
    {
        VariantCat IVariantSource<VariantCat>.Read() => new VariantCat();
        VariantDog IVariantSource<VariantDog>.Read() => new VariantDog();
    }
    public interface IVariantGeneric<out T> { T Read<U>(U value); }
    public sealed class VariantGeneric : IVariantGeneric<VariantCat>
    {
        public VariantCat Read<U>(U value) => new VariantCat();
    }
    public static class VariantDispatchProbe
    {
        public static int Covariant(int a, int b) { IVariantSource<VariantAnimal> source = new VariantSource { Item = new VariantCat { Number = a } }; return unchecked(source.Read().Number + b); }
        public static int Contravariant(int a, int b) { IVariantSink<VariantCat> sink = new VariantSink(); return unchecked(sink.Write(new VariantCat { Number = a }) + b); }
        public static int Override(int a, int b) { IVariantSource<VariantAnimal> source = new VariantOverride { Item = new VariantCat { Number = a } }; return unchecked(source.Read().Number + b); }
        public static int Explicit(int a, int b) { IVariantSource<VariantAnimal> source = new VariantExplicit { Item = new VariantCat { Number = a } }; return unchecked(source.Read().Number + b); }
        public static int Exact(int a, int b) { IVariantSource<VariantAnimal> source = new VariantExact { Number = a }; return unchecked(source.Read().Number + b); }
        public static int Null(int a, int b)
        {
            IVariantSource<VariantAnimal> source = a < 0 ? null : new VariantSource { Item = new VariantCat { Number = a } };
            try { return unchecked(source.Read().Number + b); } catch (NullReferenceException) { return 37; }
        }
        public static int Iterator(int a, int b)
        {
            VariantAnimal[] animals = { new VariantCat { Number = a }, new VariantDog { Number = b }, new VariantCat { Number = b } };
            IEnumerable<VariantAnimal> selected = animals.OfType<VariantCat>();
            var list = new List<VariantAnimal>(selected);
            return unchecked(list.Count + list[0].Number + list[1].Number);
        }
        public static int UnsupportedAmbiguous(int a, int b) { IVariantSource<VariantAnimal> source = new VariantAmbiguous(); return source.Read().Number; }
        public static int UnsupportedGeneric(int a, int b) { IVariantGeneric<VariantAnimal> source = new VariantGeneric(); return source.Read<int>(a).Number; }
    }
}
