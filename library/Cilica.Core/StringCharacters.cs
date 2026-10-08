// Adapted from pinned dotnet/runtime String.cs; see ../provenance.json.
namespace System
{
    public sealed partial class String
    {
        public string Replace(char oldChar, char newChar)
        {
            if (oldChar == newChar) return this;
            int first = IndexOf(oldChar);
            if (first < 0) return this;
            int length = Length;
            var units = new int[length];
            for (int i = 0; i < first; ++i) units[i] = this[i];
            for (int i = first; i < length; ++i)
            {
                char value = this[i];
                units[i] = value == oldChar ? newChar : value;
            }
            return Cilica.Runtime.StringConstruction.FromCodeUnits(units);
        }

        // Preserve the pinned String.cs copy/cache contract using ordinary
        // managed UTF-16 reads and stores; no native pointer or copy intrinsic.
        public char[] ToCharArray()
        {
            int length = Length;
            if (length == 0) return Array.Empty<char>();
            var result = new char[length];
            for (int i = 0; i < length; ++i) result[i] = this[i];
            return result;
        }

        // The compiler's exact granted constructor bridge targets this owned body.
        public static string CopyCharacters(char[] value)
        {
            if (value == null) return Empty;
            int length = value.Length;
            if (length == 0) return Empty;
            var units = new int[length];
            for (int i = 0; i < length; i++) units[i] = value[i];
            return Cilica.Runtime.StringConstruction.FromCodeUnits(units);
        }
    }
}
