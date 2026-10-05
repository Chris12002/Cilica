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
        private sealed record DelegateFacts(ObjectDelegateType Type, MethodDefinitionHandle Constructor, MethodDefinitionHandle Invoke);
        private readonly Dictionary<ObjectTypeId, DelegateFacts> _delegateFacts = [];
        private ObjectTypeId? _multicastDelegate;
        private readonly HashSet<ObjectTypeId> _readingDelegateSignatures = [];

        private ObjectDelegateType? DelegateType(ConstructedType type, Work? work, int offset)
        {
            var id = ClosedId(type, work, offset);
            if (_delegateFacts.TryGetValue(id, out var cached)) return cached.Type;
            if (_readingDelegateSignatures.Contains(id)) Fail(work, "Recursive delegate parameter signatures require later closure lowering.", offset);
            var input = Snapshot(type.Definition, work, offset);
            if (input.BaseType is not TypeSignature.Named parent) return null;
            if (Snapshot(parent.Definition, work, offset).Source.Name != "System.MulticastDelegate") return null;
            _multicastDelegate ??= ObjectModelBuilder.Id(Require(resolver.ResolveType(library, "System", "MulticastDelegate"), work, offset));
            if (ObjectModelBuilder.Id(parent.Definition) != _multicastDelegate)
                Fail(work, "Delegates must derive directly from the selected runtime MulticastDelegate.", offset);
            if (type.Arguments.Count != 0) input = SnapshotClosed(type, work, offset);
            if (input.IsReferenceAssembly || input.OwnerGenericArity != type.Arguments.Count || !generics && type.Arguments.Count != 0
                || !input.ExternallyVisible || input.HasTypeInitializer || input.HasModuleInitializer
                || !input.Fields.IsEmpty || input.InterfaceCount != 0 || input.PackingSize != 0 || input.ClassSize != 0
                || !ObjectModelValidator.ClassAttributes(input.Attributes) || (input.Attributes & TypeAttributes.Sealed) == 0)
                Fail(work, "Delegate CIL requires public sealed exactly closed fieldless delegate declarations without initialization, interfaces or native layout.", offset);
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
                    && (!integers64 || p is not (TypeSignature.Primitive { Code: PrimitiveTypeCode.String or PrimitiveTypeCode.Object }
                        or TypeSignature.Named { Kind: SignatureTypeKind.Class } or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } or TypeSignature.Vector)))
                || invoke.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.Void })
                Fail(work, "Delegate requires .ctor(Object, native int) and exact Int32/reference-parameter Invoke returning Int32 or void; wide, value and byref parameters require later lowering.", offset);
            _readingDelegateSignatures.Add(id);
            var parameterTypes = invoke!.Signature.Parameters.Select(p => Storage(p, work, offset)).ToImmutableArray();
            _readingDelegateSignatures.Remove(id);
            var callable = new ObjectDelegateType(input.Source, invoke!.Signature.Parameters.Count,
                invoke.Signature.ReturnType is TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }) { Parameters = parameterTypes };
            if (callable.ParameterCount >= IrLimits.MaximumParameters || _delegateFacts.Values.Sum(d => d.Type.ParameterCount) + callable.ParameterCount > IrLimits.MaximumParameters)
                Fail(work, "Aggregate delegate signature limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            _delegateFacts.Add(id, new(callable, constructorHandle, invokeHandle));
            return callable;
        }
    }
}
