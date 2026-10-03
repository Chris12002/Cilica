using System;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

/// <summary>One bounded storage grammar for fields, executable methods and dispatch contracts.</summary>
internal static class ObjectSignatureTypes
{
    internal const string Unsupported = "Storage requires Int32, selected String, nominal class/interface references, or one-dimensional SZARRAY vectors of those elements; jagged arrays, multidimensional arrays, other values and modifiers require later lowering.";

    internal static IrType? Convert(TypeSignature signature, ObjectTypeId objectBase,
        Func<ResolvedType, ObjectTypeId> include, Func<ObjectTypeId, ObjectTypeId> array, bool allowVoid = false,
        Func<ObjectTypeId>? text = null)
    {
        if (signature is TypeSignature.Vector vector)
        {
            // Deliberately nonrecursive: nested vectors are outside this profile.
            var element = Scalar(vector.Element, objectBase, include, text);
            if (element is null) return null;
            return IrType.Reference(array(ObjectTypeId.Vector(objectBase, element == IrType.Int32
                ? ObjectArrayElement.Int32 : ObjectArrayElement.Reference(element.Class!))));
        }
        return signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Void } && allowVoid
            ? IrType.Void : Scalar(signature, objectBase, include, text);
    }

    private static IrType? Scalar(TypeSignature signature, ObjectTypeId objectBase,
        Func<ResolvedType, ObjectTypeId> include, Func<ObjectTypeId>? text) => signature switch
    {
        TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 } => IrType.Int32,
        TypeSignature.Primitive { Code: PrimitiveTypeCode.Object } => IrType.Reference(objectBase),
        TypeSignature.Primitive { Code: PrimitiveTypeCode.String } when text is not null => IrType.Reference(text()),
        TypeSignature.Named { Kind: SignatureTypeKind.Class } named => IrType.Reference(include(named.Definition)),
        _ => null,
    };
}
