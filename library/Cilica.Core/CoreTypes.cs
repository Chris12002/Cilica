// Cilica-owned declarations for the bounded native runtime. Build without a
// platform CoreLib. InternalCall is metadata only: every reached intrinsic still
// requires exact build approval and a supported compiler capability.
using System.Runtime.CompilerServices;

namespace System
{
    public class Object
    {
        public Object() { }
        [MethodImpl(MethodImplOptions.InternalCall)] protected extern object MemberwiseClone();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern Type GetType();
        public virtual bool Equals(object value) => this == value;
        public virtual int GetHashCode() => Cilica.Runtime.TypeIdentity.ObjectHash(this);
        [MethodImpl(MethodImplOptions.InternalCall)] public virtual extern string ToString();
    }
    public abstract class ValueType
    {
        protected ValueType() { }
        // Default structural value equality/hashing is still unsupported. Keep
        // overrides so it cannot silently inherit reference identity semantics.
        [MethodImpl(MethodImplOptions.InternalCall)] public override extern bool Equals(object value);
        [MethodImpl(MethodImplOptions.InternalCall)] public override extern int GetHashCode();
    }
    public abstract class Array
    {
        protected Array() { }
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern T[] Empty<T>();
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern void Clear(Array array, int index, int length);
        public static int IndexOf<T>(T[] array, T value)
        {
            if (array == null) throw new ArgumentNullException("array");
            return Cilica.Runtime.KeyOperations<T>.IndexOf(array, value, array.Length);
        }
        public static Collections.ObjectModel.ReadOnlyCollection<T> AsReadOnly<T>(T[] array)
        {
            if (array == null) throw new ArgumentNullException("array");
            return Collections.ObjectModel.ReadOnlyCollection<T>.FromArray(array, array.Length);
        }
        // Roslyn emits ldlen for vector.Length. Other Array APIs and interface
        // relationships require separate admitted runtime implementations.
        public extern int Length { [MethodImpl(MethodImplOptions.InternalCall)] get; }
    }
    public abstract partial class Enum : ValueType, IComparable
    {
        protected Enum() { }
        [MethodImpl(MethodImplOptions.InternalCall)] public extern int CompareTo(object target);
    }
    public abstract class Delegate { protected Delegate() { } }
    public abstract class MulticastDelegate : Delegate { protected MulticastDelegate() { } }
    public abstract class Attribute { protected Attribute() { } }
    // Primitive storage is owned by the compiler/runtime, not managed fields.
    public struct Void { }
    public struct Boolean
    {
        public static bool TryParse(string value, out bool result) => Cilica.Runtime.BooleanParsing.TryParse(value, out result);
    }
    public struct Char
    {
        // Adapted from pinned Char.ToString/CreateFromChar; see provenance.json.
        // Existing string construction preserves fresh identity and UTF-16 units.
        public override string ToString() => ToString(this);
        public static string ToString(char value) => new string(new[] { value });
    }
    public struct SByte { }
    public struct Byte { }
    public struct Int16 { }
    public struct UInt16 { }
    public struct Int32 : IConvertible, IComparable<int>, IEquatable<int>
    {
        public static int Parse(string s, Globalization.NumberStyles style, IFormatProvider provider)
        {
            if (style != Globalization.NumberStyles.Integer || provider != Globalization.CultureInfo.InvariantCulture)
                throw new NotSupportedException("Only invariant Integer parsing is supported.");
            return Cilica.Library.IntegerParsing.ParseInt32Integer(s);
        }
        public static bool TryParse(string text, Globalization.NumberStyles style, IFormatProvider provider, out int result)
        {
            if (style != Globalization.NumberStyles.Integer || provider != Globalization.CultureInfo.InvariantCulture)
                throw new NotSupportedException("Only invariant Integer parsing is supported.");
            return Cilica.Library.IntegerParsing.Int32Integer(text, out result);
        }
        public bool Equals(int value) => this == value;
        public int CompareTo(int value) => this < value ? -1 : this > value ? 1 : 0;
        public override int GetHashCode() => this;
        public override string ToString() => Cilica.Runtime.IntegerText.Int32(this);
        public string ToString(IFormatProvider provider)
        {
            if (provider != Globalization.CultureInfo.InvariantCulture) throw new NotSupportedException("Only the invariant provider is supported.");
            return Cilica.Runtime.IntegerText.Int32(this);
        }
    }
    public struct UInt32 : IConvertible, IComparable<uint>, IEquatable<uint>
    {
        public bool Equals(uint value) => this == value;
        public int CompareTo(uint value) => this < value ? -1 : this > value ? 1 : 0;
        public override int GetHashCode() => unchecked((int)this);
        public override string ToString() => Cilica.Runtime.IntegerText.UInt32(this);
        public string ToString(IFormatProvider provider)
        {
            if (provider != Globalization.CultureInfo.InvariantCulture) throw new NotSupportedException("Only the invariant provider is supported.");
            return Cilica.Runtime.IntegerText.UInt32(this);
        }
    }
    public struct Int64 : IConvertible, IComparable<long>, IEquatable<long>
    {
        public static long Parse(string s, IFormatProvider provider)
        {
            if (provider != Globalization.CultureInfo.InvariantCulture)
                throw new NotSupportedException("Only invariant Integer parsing is supported.");
            return Cilica.Library.IntegerParsing.ParseInt64Integer(s);
        }
        public static bool TryParse(string text, Globalization.NumberStyles style, IFormatProvider provider, out long result)
        {
            if (style != Globalization.NumberStyles.None || provider != Globalization.CultureInfo.InvariantCulture)
                throw new NotSupportedException("Only invariant None parsing is supported.");
            return Cilica.Library.IntegerParsing.Int64None(text, out result);
        }
        public bool Equals(long value) => this == value;
        public int CompareTo(long value) => this < value ? -1 : this > value ? 1 : 0;
        public override int GetHashCode() => unchecked((int)this ^ (int)(this >> 32));
        public override string ToString() => Cilica.Runtime.IntegerText.Int64(this);
        public string ToString(IFormatProvider provider)
        {
            if (provider != Globalization.CultureInfo.InvariantCulture) throw new NotSupportedException("Only the invariant provider is supported.");
            return Cilica.Runtime.IntegerText.Int64(this);
        }
    }
    public struct UInt64 : IConvertible, IComparable<ulong>, IEquatable<ulong>
    {
        public bool Equals(ulong value) => this == value;
        public int CompareTo(ulong value) => this < value ? -1 : this > value ? 1 : 0;
        public override int GetHashCode() => unchecked((int)this ^ (int)(this >> 32));
        public override string ToString() => Cilica.Runtime.IntegerText.UInt64(this);
        public string ToString(IFormatProvider provider)
        {
            if (provider != Globalization.CultureInfo.InvariantCulture) throw new NotSupportedException("Only the invariant provider is supported.");
            return Cilica.Runtime.IntegerText.UInt64(this);
        }
    }
    public struct Single { }
    public struct Double { }
    public struct IntPtr { }
    public struct UIntPtr { }
    public interface IFormatProvider { }
    public interface IConvertible { string ToString(IFormatProvider provider); }

    public sealed partial class String
    {
        // Runtime-owned canonical empty string; requires its exact field grant.
        public static readonly string Empty;
        private String() { }
        [MethodImpl(MethodImplOptions.InternalCall)] public extern String(char[] value);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern int Compare(string left, string right, StringComparison comparisonType);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern bool StartsWith(string value, StringComparison comparisonType);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern string Format(string format, object arg0);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern string Format(string format, object arg0, object arg1);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern string Format(string format, object arg0, object arg1, object arg2);
        public extern int Length { [MethodImpl(MethodImplOptions.InternalCall)] get; }
        [IndexerName("Chars")] public extern char this[int index] { [MethodImpl(MethodImplOptions.InternalCall)] get; }
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern bool Equals(string left, string right);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern bool Equals(string left, string right, StringComparison comparisonType);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern bool operator ==(string left, string right);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern bool operator !=(string left, string right);
        [MethodImpl(MethodImplOptions.InternalCall)] public static extern string Concat(string left, string right);
        public override bool Equals(object value) => value is string other && Equals(this, other);
        [MethodImpl(MethodImplOptions.InternalCall)] public override extern int GetHashCode();
        public override string ToString() => this;
    }

    public class Exception
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern Exception();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern Exception(string message);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern Exception(string message, Exception innerException);
        public extern Exception InnerException { [MethodImpl(MethodImplOptions.InternalCall)] get; }
        public virtual extern string Message { [MethodImpl(MethodImplOptions.InternalCall)] get; }
    }
    public class SystemException : Exception
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern SystemException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern SystemException(string message);
    }
    public sealed class TypeInitializationException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern TypeInitializationException(string fullTypeName, Exception innerException);
        public extern string TypeName { [MethodImpl(MethodImplOptions.InternalCall)] get; }
    }
    public class ArithmeticException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArithmeticException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArithmeticException(string message);
    }
    public class DivideByZeroException : ArithmeticException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern DivideByZeroException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern DivideByZeroException(string message);
    }
    public class OverflowException : ArithmeticException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern OverflowException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern OverflowException(string message);
    }
    public class NullReferenceException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern NullReferenceException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern NullReferenceException(string message);
    }
    public class OutOfMemoryException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern OutOfMemoryException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern OutOfMemoryException(string message);
    }
    public class InvalidCastException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern InvalidCastException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern InvalidCastException(string message);
    }
    public sealed class IndexOutOfRangeException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern IndexOutOfRangeException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern IndexOutOfRangeException(string message);
    }
    public class ArrayTypeMismatchException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArrayTypeMismatchException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArrayTypeMismatchException(string message);
    }
    public class InvalidOperationException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern InvalidOperationException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern InvalidOperationException(string message);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern InvalidOperationException(string message, Exception innerException);
    }
    public class NotSupportedException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern NotSupportedException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern NotSupportedException(string message);
    }
    public class ObjectDisposedException : InvalidOperationException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ObjectDisposedException(string objectName);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ObjectDisposedException(string objectName, string message);
        public extern string ObjectName { [MethodImpl(MethodImplOptions.InternalCall)] get; }
        public override extern string Message { [MethodImpl(MethodImplOptions.InternalCall)] get; }
    }
    public class ArgumentException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentException(string message);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentException(string message, string paramName);
        public virtual extern string ParamName { [MethodImpl(MethodImplOptions.InternalCall)] get; }
    }
    public class ArgumentNullException : ArgumentException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentNullException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentNullException(string paramName);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentNullException(string paramName, string message);
    }
    public class ArgumentOutOfRangeException : ArgumentException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentOutOfRangeException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentOutOfRangeException(string paramName);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentOutOfRangeException(string paramName, string message);
        [MethodImpl(MethodImplOptions.InternalCall)] public extern ArgumentOutOfRangeException(string paramName, object actualValue, string message);
    }
}

namespace System.Globalization
{
    public class CultureInfo : System.IFormatProvider
    {
        private CultureInfo() { }
        public static extern CultureInfo InvariantCulture { [MethodImpl(MethodImplOptions.InternalCall)] get; }
    }
}

namespace System.Reflection
{
    public sealed class DefaultMemberAttribute : System.Attribute
    {
        public DefaultMemberAttribute(string memberName) { }
    }
}

namespace System.Runtime.CompilerServices
{
    public sealed class IsReadOnlyAttribute : System.Attribute { }
    public sealed class IndexerNameAttribute : System.Attribute
    {
        public IndexerNameAttribute(string name) { }
    }
    public enum MethodImplOptions { InternalCall = 4096 }
    public sealed class MethodImplAttribute : System.Attribute
    {
        public MethodImplAttribute(MethodImplOptions options) { }
    }
}

namespace System.Collections.Generic
{
    public class KeyNotFoundException : SystemException
    {
        [MethodImpl(MethodImplOptions.InternalCall)] public extern KeyNotFoundException();
        [MethodImpl(MethodImplOptions.InternalCall)] public extern KeyNotFoundException(string message);
    }
}
