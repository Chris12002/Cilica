using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public enum ClosedKeyOperation { Equal, IsNull, ContainsReferences, IsValueType }
public sealed record ClosedKeyIntrinsic(ClosedKeyOperation Operation, IrType Storage);

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        // Intrinsic selection never substitutes an application body by name.
        // Both the helper identity and the selected value target are authenticated.
        private ClosedKeyIntrinsic? KeyOperation(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var helper = resolver.ResolveType(library, "Cilica.Runtime", "KeyOperations`1").Value;
            if (helper is null || target.DeclaringType.Definition != helper) return null;
            int offset = instruction.Offset;
            if (work.Assembly != library || instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0)
                Fail(work, "Closed key operations are private to the selected core implementation.", offset);
            if (_metadata.Remaining == 0) Fail(work, "Key operation metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Key operation metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            var variable = new TypeSignature.Variable(false, 0);
            if (input.DeclaredName == "IndexOf")
            {
                if (input.IsReferenceAssembly || input.OwnerGenericArity != 1
                    || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                    || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                    || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Int32)
                    || input.Signature.Parameters.Count != 3 || input.Signature.Parameters[0] != new TypeSignature.Vector(variable)
                    || input.Signature.Parameters[1] != variable || input.Signature.Parameters[2] != new TypeSignature.Primitive(PrimitiveTypeCode.Int32))
                    Fail(work, "Invalid selected closed array search declaration.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ClosedArraySearch), work, offset);
                var element = target.DeclaringType.Arguments[0];
                CheckArgumentAccess(element, work, offset);
                if (IsNullable(element, work, offset))
                    Fail(work, "Nullable array search requires nullable equality semantics before execution.", offset);
                var elementStorage = Storage(element, work, offset);
                var selected = Require(resolver.ResolveType(library, "Cilica.Runtime",
                    elementStorage.Kind == IrTypeKind.Value ? "ValueArraySearch`1" : "CopiedArraySearch`1"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(selected, new([element])), "IndexOf", 3, _metadata), work, offset);
                return null;
            }
            if (input.DeclaredName is "ContainsReferences" or "IsValueType")
            {
                if (input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                    || input.OwnerGenericArity != 1 || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                    || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                    || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Boolean) || input.Signature.Parameters.Count != 0)
                    Fail(work, "Invalid selected closed reference-shape declaration.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, input.DeclaredName == "ContainsReferences" ? LibraryIntrinsicCapability.ClosedTypeContainsReferences : LibraryIntrinsicCapability.ClosedTypeIsValueType), work, offset);
                var element = target.DeclaringType.Arguments[0]; CheckArgumentAccess(element, work, offset);
                return new(input.DeclaredName == "ContainsReferences" ? ClosedKeyOperation.ContainsReferences : ClosedKeyOperation.IsValueType, Storage(element, work, offset));
            }
            bool equal = input.DeclaredName == "Equal", hash = input.DeclaredName == "Hash", isNull = input.DeclaredName == "IsNull";
            if (!(equal || hash || isNull) || input.IsReferenceAssembly || input.OwnerGenericArity != 1
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.ReturnType != new TypeSignature.Primitive(hash ? PrimitiveTypeCode.Int32 : PrimitiveTypeCode.Boolean)
                || input.Signature.Parameters.Count != (equal ? 2 : 1)
                || input.Signature.Parameters[0] != (isNull ? (TypeSignature)variable : new TypeSignature.ByReference(variable))
                || equal && input.Signature.Parameters[1] != variable)
                Fail(work, "Invalid selected closed key operation declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, equal ? LibraryIntrinsicCapability.ClosedKeyEqual
                : hash ? LibraryIntrinsicCapability.ClosedKeyHash : LibraryIntrinsicCapability.ClosedKeyIsNull), work, offset);
            var key = target.DeclaringType.Arguments[0];
            CheckArgumentAccess(key, work, offset);
            if (IsNullable(key, work, offset))
                Fail(work, "Nullable key operations require nullable null/equality/hash semantics before execution; ordinary value-key rules are not valid.", offset);
            var storage = Storage(key, work, offset);
            if (isNull) return new(ClosedKeyOperation.IsNull, storage);
            if (key is TypeSignature.Primitive { Code: PrimitiveTypeCode.String })
            {
                var strings = Require(resolver.ResolveType(library, "Cilica.Runtime", "StringKeyOperations"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(strings, SignatureList<TypeSignature>.Empty), equal ? "Equal" : "Hash", equal ? 2 : 1, _metadata), work, offset);
                return null;
            }
            if (key is TypeSignature.Named typeKey && typeKey.Definition == resolver.ResolveType(library, "System", "Type").Value)
            {
                _ = RuntimeTypes(work, offset);
                var helperType = Require(resolver.ResolveType(library, "Cilica.Runtime", "TypeKeyOperations"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(helperType, SignatureList<TypeSignature>.Empty), equal ? "Equal" : "Hash", equal ? 2 : 1, _metadata), work, offset);
                return null;
            }
            if (equal && storage.Kind is IrTypeKind.Int32 or IrTypeKind.Int64)
                return new(ClosedKeyOperation.Equal, storage);
            if (hash && EnumScalar(key, work, offset))
            {
                // The enum's exact nominal shape and access were checked above.
                // Enum.GetHashCode uses the underlying Int32 hash; reuse the selected
                // managed body and its byref scalar ABI rather than inventing a box.
                var integer = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Int32), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(integer, SignatureList<TypeSignature>.Empty),
                    "GetHashCode", 0, _metadata), work, offset);
                return null;
            }
            ConstructedType? receiver = key switch {
                TypeSignature.Named { Kind: SignatureTypeKind.ValueType } named => new(named.Definition, SignatureList<TypeSignature>.Empty),
                TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType } generic => new(generic.Definition.Definition, generic.Arguments),
                TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 } primitive
                    => new(Require(resolver.ResolveRuntimeType(library, primitive.Code), work, offset), SignatureList<TypeSignature>.Empty),
                _ => null };
            if (receiver is null) Fail(work, "Default key equality/hashing requires an admitted integer or exact inline IEquatable key; reference fallbacks require later lowering.", offset);
            if (key is TypeSignature.Primitive)
                target = Require(resolver.ResolveClosedDeclaration(receiver!, "GetHashCode", 0, _metadata), work, offset);
            else
            {
                var owner = Require(resolver.ResolveType(library, "System", equal ? "IEquatable`1" : "Object"), work, offset);
                var declaration = Require(resolver.ResolveClosedDeclaration(new(owner, equal ? new([key]) : SignatureList<TypeSignature>.Empty),
                    equal ? "Equals" : "GetHashCode", equal ? 1 : 0, _metadata), work, offset);
                target = Require(resolver.ResolveConstrainedValueTarget(declaration, receiver!, library, _metadata), work, offset).Implementation;
            }
            return null;
        }
    }
}
