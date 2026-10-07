// Adapted from pinned dotnet/runtime String.Manipulation.cs; see ../provenance.json.
namespace System
{
    public sealed partial class String
    {
        public string Substring(int startIndex)
        {
            if (startIndex == 0) return this;
            int length = unchecked(Length - startIndex);
            if (length == 0) return Empty;
            if ((uint)startIndex > (uint)Length) ThrowSubstringRange(startIndex, length);
            return CopySubstring(startIndex, length);
        }
        public string Substring(int startIndex, int length)
        {
            // Subtraction after checking start avoids overflow on every target.
            if ((uint)startIndex > (uint)Length || (uint)length > (uint)(Length - startIndex))
                ThrowSubstringRange(startIndex, length);
            if (length == 0) return Empty;
            if (length == Length) return this;
            return CopySubstring(startIndex, length);
        }
        private void ThrowSubstringRange(int startIndex, int length)
        {
            if (startIndex < 0) ThrowNegativeSubstring("startIndex", startIndex);
            if (startIndex > Length)
                throw new ArgumentOutOfRangeException("startIndex", "startIndex cannot be larger than length of string.");
            if (length < 0) ThrowNegativeSubstring("length", length);
            throw new ArgumentOutOfRangeException("length", "Index and length must refer to a location within the string.");
        }
        private static void ThrowNegativeSubstring(string parameter, int value)
        {
            throw new ArgumentOutOfRangeException(parameter, value,
                Concat(parameter, " ('", Cilica.Runtime.IntegerText.Int32(value), "') must be a non-negative value."));
        }
        private string CopySubstring(int startIndex, int length)
        {
            var units = new int[length];
            for (int i = 0; i < length; ++i) units[i] = this[startIndex + i];
            return Cilica.Runtime.StringConstruction.FromCodeUnits(units);
        }
    }
}
