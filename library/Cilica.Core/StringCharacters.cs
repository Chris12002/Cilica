// Adapted from pinned dotnet/runtime String.cs; see ../provenance.json.
namespace System
{
    public sealed partial class String
    {
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
