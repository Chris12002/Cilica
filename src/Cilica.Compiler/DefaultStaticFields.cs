using System.Reflection;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

internal static class DefaultStaticFields
{
    internal static bool IsUninitialized(TypeInput owner, FieldInput field) =>
        !owner.IsReferenceAssembly && !owner.HasTypeInitializer && !owner.HasModuleInitializer
        && !field.HasExplicitStorage && (field.Attributes & FieldAttributes.Static) != 0
        && ObjectModelValidator.FieldAttributesSupported(field.Attributes & ~FieldAttributes.Static);

    // With no initializer, an initonly static field cannot acquire a nonzero
    // value through admitted IL. Addresses and writes remain unsupported.
    internal static bool IsDefault(TypeInput owner, FieldInput field) =>
        !owner.IsReferenceAssembly && !owner.HasTypeInitializer && !owner.HasModuleInitializer
        && !field.HasExplicitStorage
        && (field.Attributes & (FieldAttributes.Static | FieldAttributes.InitOnly)) == (FieldAttributes.Static | FieldAttributes.InitOnly)
        && ObjectModelValidator.FieldAttributesSupported(field.Attributes & ~FieldAttributes.Static);
}
