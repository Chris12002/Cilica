using System.Runtime.CompilerServices;
namespace System
{
    public abstract partial class Enum
    {
        public static bool IsDefined(Type enumType, object value)
        {
            if (enumType == null) throw new ArgumentNullException("enumType");
            if (value == null) throw new ArgumentNullException("value");
            int result = Cilica.Runtime.EnumMetadata.Query(enumType, value);
            if (result < 2) return result != 0;
            if (result == 2) throw new ArgumentException("Type provided must be an Enum.", "enumType");
            if (result == 5) throw new InvalidOperationException("Unknown enum type.");
            throw new ArgumentException(Cilica.Runtime.EnumMetadata.ErrorMessage(enumType, value));
        }
    }
}
namespace Cilica.Runtime
{
    internal static class EnumMetadata
    {
        [MethodImpl(MethodImplOptions.InternalCall)] internal static extern int Query(System.Type type, object value);
        [MethodImpl(MethodImplOptions.InternalCall)] internal static extern string ErrorMessage(System.Type type, object value);
    }
}
