using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
internal static class ObjectEnumArgument
{
    internal static string Key(ObjectTypeId type) => new ClosedGenericEvidence(
        [new ClosedTypeShape.Named(SignatureTypeKind.ValueType, type.ContentHash, type.Token)], []).Key();
}
