// Input data for metadata inspection tests. Each declaration exercises a
// metadata feature; the code is never run.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: CLSCompliant(false)]

namespace System.Runtime.CompilerServices
{
    // Required for records and init accessors when targeting .NET Standard.
    internal static class IsExternalInit
    {
    }
}

namespace Cilica.Fixtures.Basic
{
    public interface IShape
    {
        int Area { get; }

        string Describe();
    }

    public abstract class Shape : IShape
    {
        protected Shape(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract int Area { get; }

        public virtual string Describe() => Name;
    }

    public sealed class Square : Shape
    {
        public const int Sides = 4;
        public const string Label = "square\n\"quoted\"";
        public const long Large = -9000000000;
        public const double Half = 0.5;
        public const float Third = 1f / 3f;
        public const char Letter = 'q';
        public static readonly Square Unit = new Square(1);

        private readonly int _side;

        public Square(int side)
            : base("square")
        {
            _side = side;
        }

        public override int Area => _side * _side;

        public override string Describe() => "square";
    }

    public enum Colour : byte
    {
        Red = 1,
        Green = 2,
        Blue = 4,
    }

    [Flags]
    public enum Permissions
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public readonly int LengthSquared => X * X + Y * Y;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct Overlay
    {
        [FieldOffset(0)] public long Whole;
        [FieldOffset(0)] public int Low;
        [FieldOffset(4)] public int High;
    }

    public delegate TResult Transformer<in T, out TResult>(T input);

    public interface ICovariant<out T>
    {
        T Get();
    }

    public class Repository<TKey, TValue>
        where TKey : IComparable<TKey>
        where TValue : class, new()
    {
        private readonly Dictionary<TKey, TValue> _items = new Dictionary<TKey, TValue>();

        public event EventHandler<TKey> Added;

        public TValue this[TKey key]
        {
            get => _items[key];
            set => _items[key] = value;
        }

        public bool TryGet(TKey key, out TValue value) => _items.TryGetValue(key, out value);

        public TOut Convert<TOut>(TKey key, Func<TValue, TOut> convert)
            where TOut : struct => convert(_items[key]);

        public class Entry
        {
            public TKey Key;
        }
    }

    public static class Maths
    {
        public static int Add(int a, int b = 2) => checked(a + b);

        public static ref int First(int[] values) => ref values[0];

        public static void Swap<T>(ref T left, ref T right)
        {
            T temporary = left;
            left = right;
            right = temporary;
        }

        public static int Sum(params int[] values)
        {
            int total = 0;
            foreach (int value in values)
            {
                total += value;
            }
            return total;
        }

        public static int[,] Grid(int width, int height) => new int[width, height];

        public static void Read(in Point point, out int x)
        {
            x = point.X;
        }
    }

    public sealed record Card(string Id, int Cost);

    public class ExplicitImplementation : IDisposable
    {
        void IDisposable.Dispose()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class NoteAttribute : Attribute
    {
        public NoteAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public Colour Colour { get; set; }

        public Type Related { get; set; }

        public int[] Values { get; set; }
    }

    [Note("sample", Colour = Colour.Green, Related = typeof(Square), Values = new[] { 1, 2 })]
    [Note("second")]
    public class Annotated
    {
        [Note("method")]
        public void Run()
        {
        }
    }

    public static unsafe class Native
    {
        public static delegate* unmanaged[Cdecl]<int, int> Callback;
        public static delegate*<string, void> ManagedCallback;

        [DllImport("libc", EntryPoint = "getpid")]
        public static extern int GetProcessId();

        public static int* Offset(int* pointer, int count) => pointer + count;
    }

    public class Outer
    {
        protected internal int Value;

        private protected void Hidden()
        {
        }

        public class Inner
        {
            private class Deep
            {
            }
        }
    }

    public class Locking
    {
        public volatile int Counter;

        static Locking()
        {
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Synchronised()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Slow()
        {
        }
    }

    public interface IDefaultMethod
    {
        int Value() => 1;
    }
}
