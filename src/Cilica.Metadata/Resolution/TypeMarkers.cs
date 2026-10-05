using System;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

internal static class TypeMarkers
{
    // Conservative rejection marker, including embedded declarations on older
    // targets. Recognizing this reserved name never grants runtime privileges.
    internal static bool IsByRefLike(ResolvedType type, Action inspect)
    {
        var reader = type.Assembly.Image.Reader;
        var names = new NameFormatter(reader);
        foreach (var handle in reader.GetTypeDefinition(type.Handle).GetCustomAttributes())
        {
            inspect();
            string name = names.AttributeTypeName(reader.GetCustomAttribute(handle).Constructor);
            if (name == "System.Runtime.CompilerServices.IsByRefLikeAttribute"
                || name.EndsWith("]System.Runtime.CompilerServices.IsByRefLikeAttribute", StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
