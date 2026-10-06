using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public sealed record RuntimeTypeToken(ObjectTypeId Target, ObjectTypeId Type, ObjectTypeId Implementation,
    ObjectTypeId Handle, ObjectFieldId HandleField);

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private (ObjectTypeId Type, ObjectTypeId Implementation)? _runtimeTypeFacts;
        private readonly Dictionary<MethodDefinitionHandle, MethodDeclarationInput> _runtimeTypeDeclarations = [];

        private (ObjectTypeId Type, ObjectTypeId Implementation) RuntimeTypes(Work work, int offset)
        {
            if (_runtimeTypeFacts is { } known) return known;
            var type = Require(resolver.ResolveType(library, "System", "Type"), work, offset);
            var implementation = Require(resolver.ResolveType(library, "Cilica.Runtime", "RuntimeTypeObject"), work, offset);
            var member = Require(resolver.ResolveType(library, "System.Reflection", "MemberInfo"), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, type.Handle, LibraryIntrinsicCapability.RuntimeTypeLayout), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, implementation.Handle, LibraryIntrinsicCapability.RuntimeTypeObjectLayout), work, offset);
            void Shape(ResolvedType candidate, ResolvedType parent, TypeAttributes flags)
            {
                var input = Snapshot(candidate, work, offset);
                if (input.OwnerGenericArity != 0 || input.IsReferenceAssembly || input.IsByRefLike || !input.Fields.IsEmpty
                    || input.InterfaceCount != 0 || input.PackingSize != 0 || input.ClassSize != 0
                    || input.HasTypeInitializer || input.HasModuleInitializer || input.BaseType is not TypeSignature.Named named
                    || named.Definition != parent || input.Attributes != flags)
                    Fail(work, "Runtime type identity requires the exact fieldless selected Type/MemberInfo/RuntimeTypeObject hierarchy.", offset);
            }
            Shape(member, Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset), TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit);
            Shape(type, member, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit);
            Shape(implementation, type, TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
            var result = (IncludeReference(type, work, offset), IncludeReference(implementation, work, offset));
            _runtimeTypeFacts = result;
            return result;
        }

        private RuntimeTypeToken RuntimeToken(Work work, MethodInstruction instruction)
        {
            int offset = instruction.Offset;
            var signature = Require(resolver.ResolveClosedTypeOperand(work.Resolved,
                MetadataTokens.EntityHandle(Token(work, instruction, 0x01, 0x02, 0x1b))), work, offset);
            CheckArgumentAccess(signature, work, offset);
            // Scalar metadata identity uses its validated nominal boxed plan, never
            // the shared evaluation-stack carrier. Other value tokens stay bounded out.
            ObjectTypeId target;
            if (ScalarBoxOperand(signature, work, offset) is { } scalar) target = scalar.Type;
            else if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.String }) target = IncludeString(work, offset);
            else if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Object })
                target = IncludeReference(Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset), work, offset);
            else
            {
                if (signature is TypeSignature.Named named && Inline(named.Definition, work, offset)
                    || signature is not (TypeSignature.Named or TypeSignature.Vector or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class }))
                    Fail(work, "ldtoken requires an admitted scalar, closed reference type or vector; other value, open, method and field handles require later lowering.", offset);
                target = ReferenceOperand(signature, work, offset);
            }
            var facts = RuntimeTypes(work, offset);
            var handle = Require(resolver.ResolveType(library, "System", "RuntimeTypeHandle"), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, handle.Handle, LibraryIntrinsicCapability.RuntimeTypeHandleLayout), work, offset);
            var shape = Snapshot(handle, work, offset);
            if (shape.OwnerGenericArity != 0 || shape.IsReferenceAssembly || shape.IsByRefLike || shape.HasTypeInitializer || shape.HasModuleInitializer
                || shape.Attributes != (TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit)
                || shape.InterfaceCount != 0 || shape.PackingSize != 0 || shape.ClassSize != 0
                || shape.Fields.Length != 1 || shape.Fields[0].Name != "Value" || shape.Fields[0].Attributes != FieldAttributes.Assembly
                || shape.Fields[0].HasExplicitStorage || shape.Fields[0].Signature is not TypeSignature.Named fieldType
                || ObjectModelBuilder.Id(fieldType.Definition) != facts.Type || !Inline(handle, work, offset))
                Fail(work, "RuntimeTypeHandle requires the selected single traced Type field layout.", offset);
            var handleId = Include(handle, work, offset);
            return new(target, facts.Type, facts.Implementation, handleId, new(handleId, shape.Fields[0].Token));
        }

        private ValueIntrinsicCall? RuntimeTypeCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var owner = target.DeclaringType.Definition;
            bool get = owner == resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object).Value;
            bool hash = owner == resolver.ResolveType(library, "Cilica.Runtime", "TypeIdentity").Value;
            if (!get && !hash) return null;
            if (!_runtimeTypeDeclarations.TryGetValue(target.Handle, out var input))
            {
                if (_metadata.Remaining == 0) Fail(work, "Runtime type metadata budget exhausted.", instruction.Offset, DiagnosticIds.TranslationLimit);
                input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, instruction.Offset);
                if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Runtime type metadata budget exhausted.", instruction.Offset, DiagnosticIds.TranslationLimit);
                _runtimeTypeDeclarations.Add(target.Handle, input);
            }
            if (get && input.DeclaredName != "GetType") return null;
            bool objectHash = hash && input.DeclaredName == "ObjectHash";
            if (objectHash)
            {
                if (work.Assembly != library || instruction.OpCode != "call" || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0
                    || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer || input.HasBody
                    || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                    || input.Implementation != MethodImplAttributes.InternalCall || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                    || input.Signature.Header != 0 || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] != new TypeSignature.Primitive(PrimitiveTypeCode.Object)
                    || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Int32))
                    Fail(work, "Invalid selected object identity hash declaration or caller.", instruction.Offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ObjectIdentityHash), work, instruction.Offset);
                return new(ValueIntrinsic.ObjectIdentityHash, ObjectBase(), 1);
            }
            var facts = RuntimeTypes(work, instruction.Offset);
            if (target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0 || input.IsReferenceAssembly
                || input.HasTypeInitializer || input.HasModuleInitializer || input.HasBody || input.Implementation != MethodImplAttributes.InternalCall
                || (get ? instruction.OpCode is not ("call" or "callvirt") || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig)
                    || input.Signature.Header != 0x20 || input.Signature.Parameters.Count != 0
                    || input.Signature.ReturnType is not TypeSignature.Named returned || ObjectModelBuilder.Id(returned.Definition) != facts.Type
                  : work.Assembly != library || instruction.OpCode != "call" || input.DeclaredName != "Hash"
                    || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                    || input.Signature.Header != 0 || input.Signature.Parameters.Count != 1
                    || input.Signature.Parameters[0] is not TypeSignature.Named parameter || ObjectModelBuilder.Id(parameter.Definition) != facts.Type
                    || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Int32)))
                Fail(work, "Invalid selected runtime type identity intrinsic declaration or caller.", instruction.Offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle,
                get ? LibraryIntrinsicCapability.ObjectGetType : LibraryIntrinsicCapability.RuntimeTypeHash), work, instruction.Offset);
            return new(get ? ValueIntrinsic.ObjectGetType : ValueIntrinsic.RuntimeTypeHash, facts.Implementation, 1) { RuntimeTypeBase = facts.Type };
        }
    }
}
