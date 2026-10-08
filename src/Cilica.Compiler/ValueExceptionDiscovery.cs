using System;
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
        private ObjectTypeId? _exceptionBase, _stringType, _argumentBase;
        private readonly List<ExceptionResourceBinding> _exceptionResources = [];
        private readonly HashSet<ObjectTypeId> _exceptionTypes = [], _frameworkExceptions = [];
        private readonly List<RuntimeExceptionBinding> _runtimeFaults = [];
        private readonly Dictionary<IrMethodId, MethodDeclarationInput> _exceptionDeclarations = [];
        private readonly HashSet<IrMethodId> _constructors = [];
        private readonly Dictionary<IrMethodId, ExceptionMessageConstruction> _exceptionMessages = [];
        private int _literalUnits;
        private bool IsException(ObjectTypeId type) => _exceptionTypes.Contains(type);

        private void InitializeExceptions()
        {
            string[] names = ["Exception", "SystemException", "ArithmeticException", "DivideByZeroException", "OverflowException",
                "NullReferenceException", "OutOfMemoryException", "InvalidCastException", "IndexOutOfRangeException", "ArrayTypeMismatchException", "ArgumentException"];
            uint[] statuses = [0, 0, 0, 1, 2, 4, 5, 7, 10, 11, 12];
            if (integers64) { names = [.. names, "ArgumentNullException", "ArgumentOutOfRangeException", "InvalidOperationException", "NotSupportedException"]; statuses = [.. statuses, 0, 0, 0, 0]; }
            for (int i = 0; i < names.Length; ++i)
            {
                var type = Require(resolver.ResolveType(library, "System", names[i])); var id = ObjectModelBuilder.Id(type);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(type.Assembly, type.Handle, LibraryIntrinsicCapability.ExceptionLayout));
                var input = Snapshot(type, null, 0);
                if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || !input.ExternallyVisible)
                    Fail(null, "Intrinsic exceptions require concrete selected-runtime nongeneric public definitions.");
                // These exact selected identities use Cilica's payload, never the framework's private fields/initializers.
                _snapshots[id] = input with { Fields = [], Interfaces = [], InterfaceCount = 0,
                    Attributes = TypeAttributes.Public | TypeAttributes.BeforeFieldInit, PackingSize = 0, ClassSize = 0,
                    HasTypeInitializer = false, HasModuleInitializer = false };
                _frameworkExceptions.Add(id); _exceptionTypes.Add(id);
                _references.TryAdd(id, new(type, SignatureList<TypeSignature>.Empty));
                if (i == 0) _exceptionBase = id;
                if (integers64 && i == 10) _argumentBase = id;
                if (integers64 && i is 1 or 2) _exceptionResources.Add(new(id, i == 1 ? ExceptionResourceKind.System : ExceptionResourceKind.Arithmetic));
                if (i >= 11) _exceptionResources.Add(new(id, (ExceptionResourceKind)(i - 11)));
                if (statuses[i] != 0) _runtimeFaults.Add(new(statuses[i], id));
            }
        }
        private ObjectTypeId IncludeString(Work? work, int offset)
        {
            var type = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String), work, offset);
            _stringType = ObjectModelBuilder.Id(type);
            return IncludeReference(type, work, offset);
        }
        private ObjectTypeId ReferenceOperand(TypeSignature signature, Work work, int offset) => signature switch
        {
            TypeSignature.Primitive { Code: PrimitiveTypeCode.String } => IncludeString(work, offset),
            TypeSignature.Primitive { Code: PrimitiveTypeCode.Object } when classes => IncludeReference(
                Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset), work, offset),
            TypeSignature.Vector vector when integers64 => Vector(vector.Element, work, offset),
            TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.Unknown } type => IncludeReference(type.Definition, work, offset),
            TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } type => ClosedReferenceOperand(type, work, offset),
            _ => throw new Failure(new(DiagnosticIds.ExecutionUnsupported, DiagnosticSeverity.Error,
                "Exception/type-test operands require nominal reference classes." + Path(work), Location(work, offset))),
        };
        private ObjectTypeId ClosedReferenceOperand(TypeSignature.Generic type, Work work, int offset)
        {
            CheckArgumentAccess(type, work, offset);
            return IncludeReference(new ConstructedType(type.Definition.Definition, type.Arguments), work, offset);
        }
        private void IncludeExceptionHierarchy(ConstructedType type, Work? work, int offset)
        {
            var chain = new List<ResolvedType>(); var seen = new HashSet<ObjectTypeId>();
            var current = type.Definition;
            while (true)
            {
                var id = ObjectModelBuilder.Id(current);
                if (!seen.Add(id) || seen.Count > ObjectModelLimits.MaximumOwnerDepth) Fail(work, "Cyclic or excessive exception class ancestry.", offset);
                if (_exceptionTypes.Contains(id)) break;
                string name = TypeAccess(current, work, offset).Source.Name;
                if (integers64 && name is ("System.FormatException" or "System.TypeInitializationException" or "System.Collections.Generic.KeyNotFoundException" or "System.ObjectDisposedException")
                    && current == Require(resolver.ResolveType(library, name == "System.Collections.Generic.KeyNotFoundException" ? "System.Collections.Generic" : "System",
                        name[(name.LastIndexOf('.') + 1)..]), work, offset))
                {
                    var input = Snapshot(current, work, offset);
                    _ = Require(resolver.AuthorizeLibraryIntrinsic(current.Assembly, current.Handle, LibraryIntrinsicCapability.ExceptionLayout), work, offset);
                    if (input.IsReferenceAssembly || input.OwnerGenericArity != 0 || !input.ExternallyVisible
                        || input.BaseType is not TypeSignature.Named formatBase
                        || ObjectModelBuilder.Id(formatBase.Definition) != _exceptionResources.Single(r => r.Kind == (name == "System.ObjectDisposedException" ? ExceptionResourceKind.InvalidOperation : ExceptionResourceKind.System)).Type)
                        Fail(work, "Selected framework resource exception requires a public nongeneric implementation directly derived from its selected framework base.", offset);
                    _snapshots[id] = input with { Fields = [], Interfaces = [], InterfaceCount = 0,
                        Attributes = TypeAttributes.Public | TypeAttributes.BeforeFieldInit, PackingSize = 0, ClassSize = 0,
                        HasTypeInitializer = false, HasModuleInitializer = false };
                    _frameworkExceptions.Add(id); _exceptionTypes.Add(id);
                    _references.TryAdd(id, new(current, SignatureList<TypeSignature>.Empty));
                    if (input.Source.Name == "System.TypeInitializationException") _typeInitializationException = id;
                    else _exceptionResources.Add(new(id, input.Source.Name == "System.ObjectDisposedException" ? ExceptionResourceKind.ObjectDisposed : input.Source.Name == "System.Collections.Generic.KeyNotFoundException" ? ExceptionResourceKind.KeyNotFound : ExceptionResourceKind.Format));
                    break;
                }
                chain.Add(current);
                if (TypeBase(current, work, offset).BaseType is not TypeSignature.Named parent) return;
                current = parent.Definition;
            }
            if (chain.Count != 0 && _exceptionResources.Any(r => r.Type == ObjectModelBuilder.Id(current) && r.Kind == ExceptionResourceKind.ObjectDisposed))
                Fail(work, "Custom ObjectDisposedException descendants require explicit constructor and ObjectName semantics.", offset);
            foreach (var definition in chain)
            {
                var input = Snapshot(definition, work, offset);
                if (type.Arguments.Count != 0 || input.OwnerGenericArity != 0
                    // Register nominal shells before all caller bindings exist (for example catch clauses).
                    // Each explicit type use and constructor call performs its own access check.
                    || !(input.ExternallyVisible || integers64 && input.LexicalOwners.Length == 1) || input.Source.Name.Any(ch => !char.IsLetterOrDigit(ch) && ch is not ('.' or '_'))
                    || input.HasTypeInitializer || input.HasModuleInitializer || !input.Fields.IsEmpty || input.InterfaceCount != 0
                    || input.PackingSize != 0 || input.ClassSize != 0 || input.IsReferenceAssembly || !ObjectModelValidator.ClassAttributes(input.Attributes))
                    Fail(work, "Custom exceptions require public or same-assembly internal top-level nongeneric fieldless classes without initialization, interfaces or native layout.", offset);
                if (!_references.ContainsKey(ObjectModelBuilder.Id(definition)) && _references.Count + _values.Count >= ObjectModelLimits.MaximumTypes) Fail(work, "Exception type closure exceeds the type budget.", offset, DiagnosticIds.TranslationLimit);
                var declarations = Require(resolver.ReadExceptionTypeDeclarations(definition, _metadata), work, offset);
                if (declarations.HasVirtualMembers) Fail(work, "Custom exception virtual members and Message overrides require dispatch-aware lowering.", offset);
                var id = ObjectModelBuilder.Id(definition); _exceptionTypes.Add(id); _references.TryAdd(id, new(definition, SignatureList<TypeSignature>.Empty));
            }
        }
        private MethodDeclarationInput ExceptionDeclaration(ResolvedMethod target, Work work, int offset)
        {
            var definition = target.DeclaringType.Definition; var id = new IrMethodId(definition.Assembly.ContentHash, MetadataTokens.GetToken(target.Handle));
            if (!_exceptionDeclarations.TryGetValue(id, out var input))
            {
                if (_metadata.Remaining == 0 || _exceptionDeclarations.Count + _methods.Count + _abstractMethods.Count >= ProgramLimits.MaximumMethods)
                    Fail(work, "Exception intrinsic metadata/method budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                input = Require(resolver.ReadMethodDeclarationInput(definition.Assembly, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Intrinsic metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            }
            // Access is caller-specific even when declaration metadata is cached.
            bool sameAssemblyException = integers64 && IsException(ObjectModelBuilder.Id(definition))
                && input.LexicalOwners.Length == 1 && definition.Assembly.ContentHash == work.Binding.Owner.ContentHash;
            bool managedString = integers64 && definition == resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String).Value
                && input.HasBody && input.Implementation == MethodImplAttributes.IL
                && (input.Attributes & (MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final
                    | MethodAttributes.Abstract | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0;
            if (target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0 || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0
                || !(input.OwnerExternallyVisible || sameAssemblyException) || input.IsReferenceAssembly
                || !managedString && !((input.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public
                    || sameAssemblyException && input.DeclaredName == ".ctor" && (input.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Assembly)
                || (input.Implementation & MethodImplAttributes.CodeTypeMask) is not (MethodImplAttributes.IL or MethodImplAttributes.Runtime)
                || (input.Implementation & (MethodImplAttributes.ManagedMask | MethodImplAttributes.Synchronized)) != 0
                || ((input.Attributes & MethodAttributes.Static) == 0) != ((input.Signature.Header & 0x20) != 0))
                Fail(work, "Unsupported exception/string intrinsic declaration flags, visibility or generic signature.", offset);
            _exceptionDeclarations.TryAdd(id, input); return input;
        }
        private ValueIntrinsicCall? ExceptionCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            var owner = ClosedId(target.DeclaringType, work, instruction.Offset);
            IncludeExceptionHierarchy(target.DeclaringType, work, instruction.Offset);
            bool exception = IsException(owner);
            var selectedString = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.String), work, instruction.Offset);
            if (!exception && owner != ObjectModelBuilder.Id(selectedString)) return null;
            int offset = instruction.Offset; var input = ExceptionDeclaration(target, work, offset);
            var signature = input.Signature;
            const MethodAttributes memberFlags = MethodAttributes.MemberAccessMask | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static
                | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.RTSpecialName;
            if ((input.Attributes & ~memberFlags) != 0) Fail(work, "Unsupported intrinsic member attributes.", offset);
            bool Primitive(TypeSignature type, PrimitiveTypeCode code) => type is TypeSignature.Primitive p && p.Code == code;
            if (exception)
            {
                IncludeReference(target.DeclaringType, work, offset);
                if (TypeInitializationExceptionCall(work, instruction, target, owner, input) is { } special) return special;
                if (ObjectDisposedExceptionCall(work, instruction, target, owner, input) is { } disposed) return disposed;
                if (integers64 && instruction.OpCode == "newobj" && input.DeclaredName == ".ctor"
                    && _exceptionResources.Any(r => r.Type == owner && r.Kind == ExceptionResourceKind.InvalidOperation)
                    && signature.Parameters.Count == 2)
                {
                    if (input.HasBody || input.Implementation != MethodImplAttributes.InternalCall
                        || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                        || signature.Header != 0x20 || !Primitive(signature.ReturnType, PrimitiveTypeCode.Void)
                        || !Primitive(signature.Parameters[0], PrimitiveTypeCode.String)
                        || signature.Parameters[1] is not TypeSignature.Named { Kind: SignatureTypeKind.Class } inner
                        || ObjectModelBuilder.Id(inner.Definition) != _exceptionBase)
                        Fail(work, "InvalidOperationException wrapping requires the exact selected (String, Exception) constructor.", offset);
                    Authorize(LibraryIntrinsicCapability.InvalidOperationInnerConstructor);
                    _ = IncludeString(work, offset);
                    return new(ValueIntrinsic.NewInvalidOperationInner, owner, 2);
                }
                if (integers64 && instruction.OpCode == "newobj" && input.DeclaredName == ".ctor"
                    && _exceptionResources.Any(r => r.Type == owner && r.Kind == ExceptionResourceKind.ArgumentOutOfRange)
                    && signature.Parameters.Count == 3)
                {
                    if (input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                        || signature.Header != 0x20 || !Primitive(signature.ReturnType, PrimitiveTypeCode.Void)
                        || !Primitive(signature.Parameters[0], PrimitiveTypeCode.String)
                        || !Primitive(signature.Parameters[1], PrimitiveTypeCode.Object)
                        || !Primitive(signature.Parameters[2], PrimitiveTypeCode.String))
                        Fail(work, "Actual-value range faults require the exact selected (String, Object, String) constructor.", offset);
                    Authorize(LibraryIntrinsicCapability.ArgumentOutOfRangeActualConstructor);
                    _ = IncludeString(work, offset);
                    return new(ValueIntrinsic.NewArgumentOutOfRangeActual, owner, 3);
                }
                if (instruction.OpCode == "newobj" && input.DeclaredName == ".ctor")
                {
                    ValidateExceptionConstructor(target, work, offset, 0);
                    if (signature.Parameters.Count != 0) _ = IncludeString(work, offset);
                    if (MessageInnerSignature(signature)) return new(ValueIntrinsic.NewMessageInnerException, owner, 2);
                    bool named = _exceptionResources.Any(r => r.Type == owner && r.Kind is ExceptionResourceKind.ArgumentNull or ExceptionResourceKind.ArgumentOutOfRange);
                    int count = signature.Parameters.Count;
                    return new(ValueIntrinsic.NewException, owner, count,
                        named ? count == 2 ? 1 : -1 : count == 0 ? -1 : 0,
                        named ? count == 0 ? -1 : 0 : owner == _argumentBase && count == 2 ? 1 : -1)
                        { ExceptionMessage = _exceptionMessages.GetValueOrDefault(new(owner.ContentHash, MetadataTokens.GetToken(target.Handle))) };
                }
                if (instruction.OpCode == "callvirt" && (owner == _exceptionBase || owner == _argumentBase || _exceptionResources.Any(r => r.Type == owner && r.Kind is ExceptionResourceKind.ArgumentOutOfRange or ExceptionResourceKind.ObjectDisposed)) && input.DeclaredName == "get_Message"
                    && signature.Header == 0x20 && signature.Parameters.Count == 0 && Primitive(signature.ReturnType, PrimitiveTypeCode.String))
                { Authorize(LibraryIntrinsicCapability.ExceptionMessage); _ = IncludeString(work, offset); return new(ValueIntrinsic.ExceptionMessage, owner, 1); }
                if (integers64 && instruction.OpCode == "callvirt" && owner == _argumentBase && input.DeclaredName == "get_ParamName"
                    && signature.Header == 0x20 && signature.Parameters.Count == 0 && Primitive(signature.ReturnType, PrimitiveTypeCode.String))
                { Authorize(LibraryIntrinsicCapability.ExceptionParameterName); _ = IncludeString(work, offset); return new(ValueIntrinsic.ExceptionParameterName, owner, 1); }
                Fail(work, "Only verified exception constructors and selected Exception.Message are supported; other members or overrides need explicit lowering.", offset);
            }
            var text = IncludeString(work, offset);
            if (instruction.OpCode is ("call" or "callvirt") && input.DeclaredName == "get_Length" && signature.Header == 0x20
                && signature.Parameters.Count == 0 && Primitive(signature.ReturnType, PrimitiveTypeCode.Int32))
            { Authorize(LibraryIntrinsicCapability.StringLength); return new(ValueIntrinsic.StringLength, text, 1); }
            if (instruction.OpCode == "call" && input.DeclaredName is "op_Equality" or "Equals" && signature.Header == 0
                && signature.Parameters.Count == 2 && signature.Parameters.All(p => Primitive(p, PrimitiveTypeCode.String)) && Primitive(signature.ReturnType, PrimitiveTypeCode.Boolean))
            { Authorize(LibraryIntrinsicCapability.StringEqual); return new(ValueIntrinsic.StringEqual, text, 2); }
            if (integers64 && instruction.OpCode is ("call" or "callvirt") && input.DeclaredName == "get_Chars" && signature.Header == 0x20
                && signature.Parameters.Count == 1 && Primitive(signature.Parameters[0], PrimitiveTypeCode.Int32) && Primitive(signature.ReturnType, PrimitiveTypeCode.Char))
            { Authorize(LibraryIntrinsicCapability.StringCodeUnit); return new(ValueIntrinsic.StringCodeUnit, text, 2); }
            if (integers64 && instruction.OpCode == "call" && signature.Header == 0 && signature.Parameters.Count == 2
                && signature.Parameters.All(p => Primitive(p, PrimitiveTypeCode.String)))
            {
                if (input.DeclaredName == "Concat" && Primitive(signature.ReturnType, PrimitiveTypeCode.String))
                { Authorize(LibraryIntrinsicCapability.StringConcat); return new(ValueIntrinsic.StringConcat, text, 2); }
                if (input.DeclaredName == "op_Inequality" && Primitive(signature.ReturnType, PrimitiveTypeCode.Boolean))
                { Authorize(LibraryIntrinsicCapability.StringNotEqual); return new(ValueIntrinsic.StringNotEqual, text, 2); }
            }
            // Ordinary selected-library bodies use normal access, receiver, fault
            // and IL validation. Runtime storage remains accessible only via helpers.
            if (integers64 && input.HasBody && input.Implementation == MethodImplAttributes.IL
                && (input.Attributes & (MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Final
                    | MethodAttributes.Abstract | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)) == 0
                && (instruction.OpCode == "call" && signature.Header == 0 && (input.Attributes & MethodAttributes.Static) != 0
                    || instruction.OpCode is ("call" or "callvirt") && signature.Header == 0x20 && (input.Attributes & MethodAttributes.Static) == 0))
                return null;
            Fail(work, integers64
                ? "Supported String calls are Length/Chars, static equality/inequality and Concat(string,string)."
                : "This exception profile supports String.Length and static string equality only.", offset); return null;
            void Authorize(LibraryIntrinsicCapability capability) =>
                _ = Require(resolver.AuthorizeLibraryIntrinsic(target.DeclaringType.Definition.Assembly, target.Handle, capability), work, offset);
        }
        private bool MessageInnerSignature(MethodSignature signature) => integers64 && signature.Parameters.Count == 2
            && signature.Parameters[0] is TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
            && signature.Parameters[1] is TypeSignature.Named { Kind: SignatureTypeKind.Class } inner
            && ObjectModelBuilder.Id(inner.Definition) == _exceptionBase;

        private void ValidateExceptionConstructor(ResolvedMethod target, Work work, int offset, int depth)
        {
            var owner = ClosedId(target.DeclaringType, work, offset); var id = new IrMethodId(owner.ContentHash, MetadataTokens.GetToken(target.Handle));
            var input = ExceptionDeclaration(target, work, offset); var signature = input.Signature;
            if (_constructors.Contains(id)) return;
            if (depth >= ObjectModelLimits.MaximumOwnerDepth) Fail(work, "Exception forwarding constructor depth exceeded.", offset, DiagnosticIds.TranslationLimit);
            const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            bool named = _exceptionResources.Any(r => r.Type == owner && r.Kind is ExceptionResourceKind.ArgumentNull or ExceptionResourceKind.ArgumentOutOfRange);
            bool messageInner = MessageInnerSignature(signature);
            int maximum = owner == _argumentBase || named || messageInner ? 2 : 1;
            if (input.DeclaredName != ".ctor" || !(input.Attributes == flags
                    || integers64 && input.Attributes == ((flags & ~MethodAttributes.MemberAccessMask) | MethodAttributes.Assembly)) || signature.Header != 0x20
                || signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.GenericArity != 0 || signature.Parameters.Count > maximum || !messageInner && signature.Parameters.Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }))
                Fail(work, "Exception construction requires an exact accessible string or selected message/inner constructor; other payloads need later lowering.", offset);
            if (_frameworkExceptions.Contains(owner))
            {
                if (messageInner && (owner != _exceptionBase || input.HasBody || input.Implementation != MethodImplAttributes.InternalCall))
                    Fail(work, "Message/inner forwarding requires the exact selected Exception base constructor.", offset);
                _ = Require(resolver.AuthorizeLibraryIntrinsic(target.DeclaringType.Definition.Assembly, target.Handle,
                    LibraryIntrinsicCapability.ExceptionConstructor), work, offset);
                if (owner != _exceptionBase && !_runtimeFaults.Any(b => b.Type == owner) && !_exceptionResources.Any(r => r.Type == owner))
                    Fail(work, "This framework exception constructor has no selected message resource lowering.", offset);
                _constructors.Add(id); return;
            }
            if (_metadata.Remaining == 0 || _bytes >= ProgramLimits.MaximumBodyBytes || _instructions >= ProgramLimits.MaximumInstructions)
                Fail(work, "Exception constructor validation budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var body = Require(resolver.ReadClosedMethodInput(target, library, limits with {
                MaximumInitializerMembers = Math.Min(limits.MaximumInitializerMembers, _metadata.Remaining),
                MaximumBodyBytes = Math.Min(limits.MaximumBodyBytes, ProgramLimits.MaximumBodyBytes - _bytes),
                MaximumInstructions = Math.Min(limits.MaximumInstructions, ProgramLimits.MaximumInstructions - _instructions) }), work, offset);
            _bytes += body.CodeSize; _instructions += body.Instructions.Length;
            if (!_metadata.TryConsume(body.MetadataMembersRead)) Fail(work, "Exception constructor metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var instructions = body.Instructions.Where(i => i.OpCode != "nop").ToArray(); int count = signature.Parameters.Count;
            if (integers64 && count == 1 && body.ExceptionRegionCount == 0 && body.Locals.IsEmpty
                && !body.HasTypeInitializer && !body.HasModuleInitializer && body.Implementation == MethodImplAttributes.IL
                && TryExceptionMessageConstruction(target, work, offset, instructions) is { } messageConstruction)
            {
                _exceptionMessages.Add(id, messageConstruction); _constructors.Add(id); return;
            }
            if (body.ExceptionRegionCount != 0 || !body.Locals.IsEmpty || body.HasTypeInitializer || body.HasModuleInitializer
                || body.Implementation != MethodImplAttributes.IL || instructions.Length != count + 3
                || instructions[0].OpCode != "ldarg.0" || count >= 1 && instructions[1].OpCode != "ldarg.1"
                || count == 2 && instructions[2].OpCode != "ldarg.2"
                || instructions[^2].OpCode != "call" || instructions[^1].OpCode != "ret")
                Fail(work, "Custom exception constructors must only forward their unchanged arguments to the direct base constructor; additional behavior cannot be skipped.", offset);
            var called = Require(resolver.ResolveClosedCall(target, MetadataTokens.EntityHandle(instructions[^2].IntegerOperand ?? 0), _metadata), work, offset);
            var parent = Snapshot(target.DeclaringType.Definition, work, offset).BaseType as TypeSignature.Named;
            if (parent is null || ObjectModelBuilder.Id(parent.Definition) != ClosedId(called.DeclaringType, work, offset) || called.Signature.Parameters.Count != count
                || !called.Signature.Parameters.SequenceEqual(signature.Parameters))
                Fail(work, "Custom exception constructor must call its direct base with the same signature.", offset);
            if (_exceptionResources.Any(r => r.Type == ClosedId(called.DeclaringType, work, offset)
                && !(r.Kind == ExceptionResourceKind.System && count == 1)))
                Fail(work, "Custom forwarding to additional runtime exception constructors requires preserved constructor semantics.", offset);
            ValidateExceptionConstructor(called, work, offset, depth + 1);
            if (_exceptionMessages.TryGetValue(new(ClosedId(called.DeclaringType, work, offset).ContentHash, MetadataTokens.GetToken(called.Handle)), out var inheritedMessage))
                _exceptionMessages.Add(id, inheritedMessage);
            _constructors.Add(id);
        }
    }
}
