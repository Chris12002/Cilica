using System;
namespace Cilica.Fixtures.Execution
{
    public interface IInitializedReader { int Read(int value); }
    public struct InitializedReaderValue : IInitializedReader
    {
        public static readonly int Seed = 13;
        public int Number;
        public int Read(int value) => unchecked(Seed + Number + value);
    }
    public sealed class InitializedReaderSlot<T> : IInitializedReader
    {
        private static readonly StaticBox Seed = new StaticBox { Value = 17 };
        public int Read(int value) { Seed.Value = value; return Seed.Value; }
        public int Stored() => Seed.Value;
    }
    public sealed class FailedInitializedReader : IInitializedReader
    {
        private static readonly int Seed = Fail();
        private static int Fail() { throw new InvalidOperationException("handler init"); }
        public int Read(int value) => unchecked(Seed + value);
    }
    public sealed class StrictInitializedReader : IInitializedReader
    {
        private static readonly int Seed;
        static StrictInitializedReader() { Seed = 19; }
        public int Read(int value) => unchecked(Seed + value);
    }
    public static class InitializedInterfaceProbe
    {
        private sealed class Handler : IInitializedReader
        {
            private static readonly InitializedPoint Id = new InitializedPoint(23, 29);
            public int Read(int value) { var id = Id; return unchecked(value + id.Number + (int)id.Wide); }
        }
        private sealed class ExplicitHandler : IInitializedReader
        {
            private static readonly int Seed = 31;
            int IInitializedReader.Read(int value) => unchecked(value + Seed);
        }
        public static int Private(int a, int b) { IInitializedReader reader = new Handler(); return unchecked(reader.Read(a) + reader.Read(b)); }
        public static int Explicit(int a, int b) { IInitializedReader reader = new ExplicitHandler(); return unchecked(reader.Read(a) + reader.Read(b)); }
        public static int Boxed(int a, int b) { var value = new InitializedReaderValue { Number = a }; IInitializedReader reader = value; value.Number = b; return reader.Read(b); }
        public static int Closed(int a, int b)
        {
            var first = new InitializedReaderSlot<int>(); var second = new InitializedReaderSlot<long>();
            IInitializedReader one = first; IInitializedReader two = second;
            one.Read(a); two.Read(b);
            return unchecked(first.Stored() + second.Stored());
        }
        public static int Failure(int a, int b)
        {
            IInitializedReader reader = new FailedInitializedReader(); Exception first = null;
            try { return reader.Read(a); } catch (TypeInitializationException error) { first = error.InnerException; }
            try { return reader.Read(b); }
            catch (TypeInitializationException error) { return unchecked((ReferenceEquals(first, error.InnerException) ? 100 : -100) + error.InnerException.Message.Length + a + b); }
        }
        public static int Null(int a, int b)
        {
            IInitializedReader reader = a < 0 ? null : new Handler();
            try { return reader.Read(b); } catch (NullReferenceException) { return 37; }
        }
        public static int UnsupportedStrict(int a, int b) { IInitializedReader reader = new StrictInitializedReader(); return reader.Read(a); }
    }
}
