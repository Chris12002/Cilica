using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private sealed record DelegateFacts(ObjectDelegateType Type, MethodDefinitionHandle Constructor, MethodDefinitionHandle Invoke, MethodSignature Signature);
        private readonly Dictionary<ObjectTypeId, DelegateFacts> _delegateFacts = [];
        private ObjectTypeId? _multicastDelegate;
        private readonly HashSet<ObjectTypeId> _readingDelegateSignatures = [];

        private ObjectDelegateType? DelegateType(ConstructedType type, Work? work, int offset)
        {
            var id = ClosedId(type, work, offset);
            if (_delegateFacts.TryGetValue(id, out var cached)) return cached.Type;
            if (_readingDelegateSignatures.Contains(id)) Fail(work, "Recursive delegate signatures require later closure lowering.", offset);
            if (TypeBase(type.Definition, work, offset).BaseType is not TypeSignature.Named parent) return null;
            if (TypeAccess(parent.Definition, work, offset).Source.Name != "System.MulticastDelegate") return null;
            _multicastDelegate ??= ObjectModelBuilder.Id(Require(resolver.ResolveType(library, "System", "MulticastDelegate"), work, offset));
            if (ObjectModelBuilder.Id(parent.Definition) != _multicastDelegate)
                Fail(work, "Delegates must derive directly from the selected runtime MulticastDelegate.", offset);
            var input = type.Arguments.Count != 0 ? SnapshotClosed(type, work, offset) : Snapshot(type.Definition, work, offset);
            if (input.IsReferenceAssembly || input.OwnerGenericArity != type.Arguments.Count || !generics && type.Arguments.Count != 0
                || !input.ExternallyVisible && (!integers64 || type.Arguments.Count != 0 || input.LexicalOwners.IsDefaultOrEmpty
                    || !ObjectLexicalAccess.Valid(id, input.LexicalOwners) || input.LexicalOwners.Any(o => o.GenericArity != 0))
                || input.HasTypeInitializer || input.HasModuleInitializer
                || !input.Fields.IsEmpty || input.InterfaceCount != 0 || input.PackingSize != 0 || input.ClassSize != 0
                || !ObjectModelValidator.ClassAttributes(input.Attributes) || (input.Attributes & TypeAttributes.Sealed) == 0)
                Fail(work, "Delegate CIL requires public exactly closed or nongeneric lexically described sealed fieldless delegate declarations without initialization, interfaces or native layout.", offset);
            MethodDeclarationInput? constructor = null, invoke = null;
            MethodDefinitionHandle constructorHandle = default, invokeHandle = default;
            foreach (var declaration in Require(resolver.ReadDelegateDeclarations(type, _metadata), work, offset).Methods)
            {
                string name = declaration.DeclaredName;
                var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(declaration.Source.Token);
                if (declaration.HasBody || declaration.Signature.Header != 0x20 || declaration.Signature.GenericArity != 0
                    || declaration.Implementation != MethodImplAttributes.Runtime)
                    Fail(work, "Delegate constructor and Invoke must be bodyless managed runtime declarations with ordinary instance signatures.", offset);
                if (name == ".ctor")
                {
                    if (constructor is not null) Fail(work, "Delegate has duplicate constructors.", offset);
                    constructor = declaration; constructorHandle = handle;
                }
                else
                {
                    if (invoke is not null) Fail(work, "Delegate has duplicate Invoke declarations.", offset);
                    invoke = declaration; invokeHandle = handle;
                }
            }
            var ctorFlags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var invokeFlags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;
            if (constructor is null || constructor.Attributes != ctorFlags
                || constructor.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || constructor.Signature.Parameters.Count != 2
                || constructor.Signature.Parameters[0] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Object }
                || constructor.Signature.Parameters[1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.IntPtr }
                || invoke is null || invoke.Attributes != invokeFlags
                || invoke.Signature.Parameters.Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }
                    && (!integers64 || p is not (TypeSignature.Primitive { Code: PrimitiveTypeCode.Int64 or PrimitiveTypeCode.String or PrimitiveTypeCode.Object }
                        or TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType }
                        or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType } or TypeSignature.Vector)))
                || invoke.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Void }
                    && !(integers64 && invoke.Signature.ReturnType is TypeSignature.Primitive { Code: PrimitiveTypeCode.Boolean })
                    && (!integers64 || invoke.Signature.ReturnType is not (TypeSignature.Primitive { Code: PrimitiveTypeCode.Int64 or PrimitiveTypeCode.String or PrimitiveTypeCode.Object }
                        or TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType }
                        or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class or SignatureTypeKind.ValueType } or TypeSignature.Vector)))
                Fail(work, "Delegate requires .ctor(Object, native int) and exact Int32/Int64/reference/inline-value parameters with Invoke returning Int32, Int64, Boolean, reference, inline value or void; unsigned/narrow parameters/results and byref signatures require later lowering.", offset);
            _readingDelegateSignatures.Add(id);
            var parameterTypes = invoke!.Signature.Parameters.Select(p => Storage(p, work, offset)).ToImmutableArray();
            if (invoke.Signature.Parameters.Zip(parameterTypes).Any(pair =>
                pair.First is TypeSignature.Named { Kind: SignatureTypeKind.ValueType } or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType }
                && pair.Second.Kind != IrTypeKind.Value))
                Fail(work, "Delegate value parameters require named non-enum inline layouts; scalar aliases remain unsupported.", offset);
            var returnType = Storage(invoke.Signature.ReturnType, work, offset, allowVoid: true);
            if (invoke.Signature.ReturnType is TypeSignature.Named { Kind: SignatureTypeKind.ValueType } or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType }
                && returnType.Kind != IrTypeKind.Value)
                Fail(work, "Delegate value results require named non-enum inline layouts; scalar aliases remain unsupported.", offset);
            _readingDelegateSignatures.Remove(id);
            var callable = new ObjectDelegateType(input.Source, invoke.Signature.Parameters.Count, returnType) {
                Parameters = parameterTypes, ExternallyVisible = input.ExternallyVisible,
                LexicalOwners = integers64 ? input.LexicalOwners : [] };
            if (callable.ParameterCount >= IrLimits.MaximumParameters || _delegateFacts.Values.Sum(d => d.Type.ParameterCount) + callable.ParameterCount > IrLimits.MaximumParameters)
                Fail(work, "Aggregate delegate signature limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _delegateFacts.Add(id, new(callable, constructorHandle, invokeHandle, invoke.Signature));
            return callable;
        }
    }
}
