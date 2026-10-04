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
        private ObjectTypeId? _exceptionBase, _stringType;
        private readonly HashSet<ObjectTypeId> _exceptionTypes = [], _frameworkExceptions = [];
        private readonly List<RuntimeExceptionBinding> _runtimeFaults = [];
        private readonly Dictionary<IrMethodId, MethodDeclarationInput> _exceptionDeclarations = [];
        private readonly HashSet<IrMethodId> _constructors = [];
        private int _literalUnits;
        private bool IsException(ObjectTypeId type) => _exceptionTypes.Contains(type);

        private void InitializeExceptions()
        {
            string[] names = ["Exception", "SystemException", "ArithmeticException", "DivideByZeroException", "OverflowException",
                "NullReferenceException", "OutOfMemoryException", "InvalidCastException", "IndexOutOfRangeException", "ArrayTypeMismatchException", "ArgumentException"];
            uint[] statuses = [0, 0, 0, 1, 2, 4, 5, 7, 10, 11, 12];
            for (int i = 0; i < names.Length; ++i)
            {
                var type = Require(resolver.ResolveType(library, "System", names[i])); var id = ObjectModelBuilder.Id(type);
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
            TypeSignature.Named { Kind: SignatureTypeKind.Class or SignatureTypeKind.Unknown } type => IncludeReference(type.Definition, work, offset),
            TypeSignature.Generic { Definition.Kind: SignatureTypeKind.Class } type => IncludeReference(new ConstructedType(type.Definition.Definition, type.Arguments), work, offset),
            _ => throw new Failure(new(DiagnosticIds.ExecutionUnsupported, DiagnosticSeverity.Error,
                "Exception/type-test operands require nominal reference classes." + Path(work), Location(work, offset))),
        };
        private void IncludeExceptionHierarchy(ConstructedType type, Work? work, int offset)
        {
            var chain = new List<(ResolvedType Type, TypeInput Input)>(); var seen = new HashSet<ObjectTypeId>();
            var current = type.Definition;
            while (true)
            {
                var id = ObjectModelBuilder.Id(current);
                if (!seen.Add(id) || seen.Count > ObjectModelLimits.MaximumOwnerDepth) Fail(work, "Cyclic or excessive exception class ancestry.", offset);
                if (_exceptionTypes.Contains(id)) break;
                var input = Snapshot(current, work, offset); chain.Add((current, input));
                if (input.BaseType is not TypeSignature.Named parent) return;
                current = parent.Definition;
            }
            foreach (var (definition, input) in chain)
            {
                if (type.Arguments.Count != 0 || input.OwnerGenericArity != 0 || !input.ExternallyVisible || input.Source.Name.Any(ch => !char.IsLetterOrDigit(ch) && ch is not ('.' or '_'))
                    || input.HasTypeInitializer || input.HasModuleInitializer || !input.Fields.IsEmpty || input.InterfaceCount != 0
                    || input.PackingSize != 0 || input.ClassSize != 0 || input.IsReferenceAssembly || !ObjectModelValidator.ClassAttributes(input.Attributes))
                    Fail(work, "Custom exceptions require public top-level nongeneric fieldless classes without initialization, interfaces or native layout.", offset);
                if (!_references.ContainsKey(ObjectModelBuilder.Id(definition)) && _references.Count + _values.Count >= ObjectModelLimits.MaximumTypes) Fail(work, "Exception type closure exceeds the type budget.", offset, DiagnosticIds.TranslationLimit);
                var declarations = Require(resolver.ReadExceptionTypeDeclarations(definition, _metadata), work, offset);
                if (declarations.HasVirtualMembers) Fail(work, "Custom exception virtual members and Message overrides require dispatch-aware lowering.", offset);
                var id = ObjectModelBuilder.Id(definition); _exceptionTypes.Add(id); _references.TryAdd(id, new(definition, SignatureList<TypeSignature>.Empty));
            }
        }
        private MethodDeclarationInput ExceptionDeclaration(ResolvedMethod target, Work work, int offset)
        {
            var definition = target.DeclaringType.Definition; var id = new IrMethodId(definition.Assembly.ContentHash, MetadataTokens.GetToken(target.Handle));
            if (_exceptionDeclarations.TryGetValue(id, out var cached)) return cached;
            if (_metadata.Remaining == 0 || _exceptionDeclarations.Count + _methods.Count >= ProgramLimits.MaximumMethods)
                Fail(work, "Exception intrinsic metadata/method budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(definition.Assembly, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Intrinsic metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            if (target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0 || input.OwnerGenericArity != 0 || input.Signature.GenericArity != 0
                || !input.OwnerExternallyVisible || input.IsReferenceAssembly
                || (input.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public
                || (input.Implementation & MethodImplAttributes.CodeTypeMask) is not (MethodImplAttributes.IL or MethodImplAttributes.Runtime)
                || (input.Implementation & (MethodImplAttributes.ManagedMask | MethodImplAttributes.Synchronized)) != 0
                || ((input.Attributes & MethodAttributes.Static) == 0) != ((input.Signature.Header & 0x20) != 0))
                Fail(work, "Unsupported exception/string intrinsic declaration flags, visibility or generic signature.", offset);
            _exceptionDeclarations.Add(id, input); return input;
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
                if (instruction.OpCode == "newobj" && input.DeclaredName == ".ctor")
                {
                    ValidateExceptionConstructor(target, work, offset, 0);
                    if (signature.Parameters.Count != 0) _ = IncludeString(work, offset);
                    return new(ValueIntrinsic.NewException, owner, signature.Parameters.Count);
                }
                if (instruction.OpCode == "callvirt" && owner == _exceptionBase && input.DeclaredName == "get_Message"
                    && signature.Header == 0x20 && signature.Parameters.Count == 0 && Primitive(signature.ReturnType, PrimitiveTypeCode.String))
                { _ = IncludeString(work, offset); return new(ValueIntrinsic.ExceptionMessage, owner, 1); }
                Fail(work, "Only verified exception constructors and selected Exception.Message are supported; other members or overrides need explicit lowering.", offset);
            }
            var text = IncludeString(work, offset);
            if (instruction.OpCode == "callvirt" && input.DeclaredName == "get_Length" && signature.Header == 0x20
                && signature.Parameters.Count == 0 && Primitive(signature.ReturnType, PrimitiveTypeCode.Int32))
                return new(ValueIntrinsic.StringLength, text, 1);
            if (instruction.OpCode == "call" && input.DeclaredName is "op_Equality" or "Equals" && signature.Header == 0
                && signature.Parameters.Count == 2 && signature.Parameters.All(p => Primitive(p, PrimitiveTypeCode.String)) && Primitive(signature.ReturnType, PrimitiveTypeCode.Boolean))
                return new(ValueIntrinsic.StringEqual, text, 2);
            Fail(work, "This exception profile supports String.Length and static string equality only.", offset); return null;
        }
        private void ValidateExceptionConstructor(ResolvedMethod target, Work work, int offset, int depth)
        {
            var owner = ClosedId(target.DeclaringType, work, offset); var id = new IrMethodId(owner.ContentHash, MetadataTokens.GetToken(target.Handle));
            if (_constructors.Contains(id)) return;
            if (depth >= ObjectModelLimits.MaximumOwnerDepth) Fail(work, "Exception forwarding constructor depth exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = ExceptionDeclaration(target, work, offset); var signature = input.Signature;
            const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            if (input.DeclaredName != ".ctor" || input.Attributes != flags || signature.Header != 0x20
                || signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.GenericArity != 0 || signature.Parameters.Count > 1 || signature.Parameters.Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }))
                Fail(work, "Exception construction requires an exact public parameterless or single-string constructor.", offset);
            if (_frameworkExceptions.Contains(owner))
            {
                if (owner != _exceptionBase && !_runtimeFaults.Any(b => b.Type == owner))
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
            if (body.ExceptionRegionCount != 0 || !body.Locals.IsEmpty || body.HasTypeInitializer || body.HasModuleInitializer
                || body.Implementation != MethodImplAttributes.IL || instructions.Length != count + 3
                || instructions[0].OpCode != "ldarg.0" || count == 1 && instructions[1].OpCode != "ldarg.1"
                || instructions[^2].OpCode != "call" || instructions[^1].OpCode != "ret")
                Fail(work, "Custom exception constructors must only forward their unchanged arguments to the direct base constructor; additional behavior cannot be skipped.", offset);
            var called = Require(resolver.ResolveClosedCall(target, MetadataTokens.EntityHandle(instructions[^2].IntegerOperand ?? 0), _metadata), work, offset);
            var parent = Snapshot(target.DeclaringType.Definition, work, offset).BaseType as TypeSignature.Named;
            if (parent is null || ObjectModelBuilder.Id(parent.Definition) != ClosedId(called.DeclaringType, work, offset) || called.Signature.Parameters.Count != count)
                Fail(work, "Custom exception constructor must call its direct base with the same signature.", offset);
            ValidateExceptionConstructor(called, work, offset, depth + 1); _constructors.Add(id);
        }
    }
}
