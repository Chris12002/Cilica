using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<ObjectFieldId, ObjectStaticField> _staticFields = [];
        private ObjectStaticField StaticField(Work work, ResolvedField field, TypeInput owner, FieldInput declaration, bool write, int offset)
        {
            if (!DefaultStaticFields.IsUninitialized(owner, declaration))
                Fail(work, "Static storage requires ordinary fields without type/module initialization or explicit storage; initialized statics require later lowering.", offset);
            if (write && (declaration.Attributes & FieldAttributes.InitOnly) != 0)
                Fail(work, "Readonly static fields cannot be assigned outside their type initializer.", offset);
            var id = new ObjectFieldId(ClosedId(field.DeclaringType, work, offset), MetadataTokens.GetToken(field.Handle));
            var access = declaration.Attributes & FieldAttributes.FieldAccessMask;
            if (access == FieldAttributes.PrivateScope || work.Binding.Owner != id.Owner && (access != FieldAttributes.Public || !owner.ExternallyVisible))
                Fail(work, "Static fields require the same owner or a public member on an externally visible type.", offset);
            if (_staticFields.TryGetValue(id, out var known)) return known;
            if (_staticFields.Count >= ObjectModelLimits.MaximumFields)
                Fail(work, "Static field storage limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (field.Signature is not (TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.String or PrimitiveTypeCode.Object }
                or TypeSignature.Named { Kind: SignatureTypeKind.Class } or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } or TypeSignature.Vector))
                Fail(work, "Static fields currently require Int32 or admitted reference storage; narrow, wide and inline values require later lowering.", offset);
            var storage = Storage(field.Signature, work, offset);
            var result = new ObjectStaticField(id, storage, declaration.Attributes, owner.ExternallyVisible);
            _staticFields.Add(id, result); return result;
        }
    }
}
