// Cilica-owned bounded API/cache adapter. Decimal arithmetic lives in the
// pinned-source Cilica.Library.IntegerFormatting adaptation.
using System.Runtime.CompilerServices;
namespace Cilica.Runtime
{
    internal static class IntegerText
    {
        internal static string Int32(int value) => value >= 0 && value < 300 ? Small((uint)value) : Library.IntegerFormatting.Int32Invariant(value);
        internal static string UInt32(uint value) => value < 300 ? Small(value) : Library.IntegerFormatting.UInt32Invariant(value);
        internal static string Int64(long value) => value >= 0 && value < 300 ? Small((uint)value) : Library.IntegerFormatting.Int64Invariant(value);
        internal static string UInt64(ulong value) => value < 300 ? Small((uint)value) : Library.IntegerFormatting.UInt64Invariant(value);
        private static string Small(uint value)
        {
            string cached = IntegerTextCache.Find(value);
            if (cached != null) return cached;
            return IntegerTextCache.Publish(value, Library.IntegerFormatting.UInt32Invariant(value));
        }
    }
    internal static class IntegerTextCache
    {
        [MethodImpl(MethodImplOptions.InternalCall)] internal static extern string Find(uint value);
        [MethodImpl(MethodImplOptions.InternalCall)] internal static extern string Publish(uint value, string text);
    }
}
