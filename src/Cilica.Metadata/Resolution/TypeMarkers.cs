using System;
using System.Reflection.Metadata;
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
    // This marker only restricts a receiver: translated bodies must still pass
    // readonly-location validation. Embedded reserved-name definitions are valid.
    internal static bool ReadOnlyReceiver(ResolvedAssembly assembly, MethodDefinitionHandle handle, Action inspect)
    {
        var reader = assembly.Image.Reader; var method = reader.GetMethodDefinition(handle);
        var names = new NameFormatter(reader);
        return Has(reader.GetTypeDefinition(method.GetDeclaringType()).GetCustomAttributes()) || Has(method.GetCustomAttributes());
        bool Has(CustomAttributeHandleCollection attributes)
        {
            foreach (var attribute in attributes)
            {
                inspect();
                string name = names.AttributeTypeName(reader.GetCustomAttribute(attribute).Constructor);
                if (name == "System.Runtime.CompilerServices.IsReadOnlyAttribute"
                    || name.EndsWith("]System.Runtime.CompilerServices.IsReadOnlyAttribute", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
