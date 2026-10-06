using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

internal static class Int32EnumStorage
{
    internal static bool Matches(TypeSignature signature, ResolvedType? enumBase, Func<ResolvedType, TypeInput> read)
    {
        if (enumBase is null || signature is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named)
            return false;
        var input = read(named.Definition);
        if (input.BaseType is not TypeSignature.Named parent || parent.Definition != enumBase) return false;
        var fields = input.Fields.Where(f => (f.Attributes & FieldAttributes.Static) == 0).ToArray();
        // Visibility is an access property, not a scalar representation property.
        // Admit top-level internal enums in stored object/value layouts. Direct
        // operands still check their caller, and generic/boxed paths keep their
        // existing stricter nominal access rules.
        bool internalTopLevel = input.LexicalOwners.Length == 1
            && input.LexicalOwners[0].Visibility == TypeAttributes.NotPublic;
        if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || (!input.ExternallyVisible && !internalTopLevel) || input.IsByRefLike
            || input.HasTypeInitializer || input.HasModuleInitializer || input.InterfaceCount != 0
            || input.PackingSize != 0 || input.ClassSize != 0 || !ValueLayoutPlanner.AttributesSupported(input.Attributes)
            || (input.Attributes & TypeAttributes.LayoutMask) != TypeAttributes.AutoLayout
            || fields.Length != 1 || fields[0].Name != "value__" || fields[0].HasExplicitStorage
            || fields[0].Attributes != (FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName)
            || fields[0].Signature is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
            || input.Fields.Any(f => (f.Attributes & FieldAttributes.Static) != 0
                && (f.Attributes != (FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault)
                    || f.Signature is not TypeSignature.Named literal || literal.Definition != named.Definition)))
            throw new ArgumentException("Enum scalar storage requires a public or top-level internal nongeneric Int32 enum with exact value__ storage, literal constants, and no interfaces, layout or initialization. Type: " + input.Source.Name + ".");
        return true;
    }
}
