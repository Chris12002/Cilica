using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>
    /// Follows explicit MethodDef/FieldDef roots. A TypeDef root selects all its declared
    /// methods and fields. Roots are sorted and deduplicated; ordinary FIFO expansion
    /// alternates with dispatch pairs within the configured limits.
    /// Explicit contract images defer implementation expansion and always leave
    /// HasUnresolvedDependencies true; they do not add reference directories.
    /// </summary>
    public ReachabilityReport AnalyzeReachability(ResolvedAssembly assembly, IEnumerable<EntityHandle> roots,
        ReachabilityLimits? limits = null, ResolvedAssembly? runtimeLibrary = null, IEnumerable<ResolvedAssembly>? contractAssemblies = null)
    {
        CheckOwner(assembly);
        ArgumentNullException.ThrowIfNull(roots);
        if (runtimeLibrary is not null) CheckOwner(runtimeLibrary);
        return new ReachabilityWalker(this, limits ?? new(), runtimeLibrary, ValidateContractAssemblies(assembly, contractAssemblies)).Run(assembly, roots, publicSurface: false);
    }

    /// <summary>Selects externally visible types and public/protected members as roots.</summary>
    public ReachabilityReport AnalyzePublicSurface(ResolvedAssembly assembly, ReachabilityLimits? limits = null, ResolvedAssembly? runtimeLibrary = null,
        IEnumerable<ResolvedAssembly>? contractAssemblies = null)
    {
        CheckOwner(assembly);
        if (runtimeLibrary is not null) CheckOwner(runtimeLibrary);
        return new ReachabilityWalker(this, limits ?? new(), runtimeLibrary, ValidateContractAssemblies(assembly, contractAssemblies)).Run(assembly, [], publicSurface: true);
    }

    private sealed partial class ReachabilityWalker
    {
        private readonly AssemblyResolver _resolver;
        private readonly ReachabilityLimits _limits;
        private readonly ResolvedAssembly? _runtimeLibrary;
        private readonly List<ReachabilityNode> _nodes = [];
        private readonly List<int> _roots = [];
        private readonly List<ReachabilityEdge> _edges = [];
        private readonly HashSet<ReachabilityEdge> _edgeSet = [];
        private readonly List<ReachabilityIssue> _issues = [];
        private readonly HashSet<ReachabilityIssue> _issueSet = [];
        private readonly Dictionary<ReachableEntity, int> _ids = [];
        private readonly Queue<int> _pending = [];
        private readonly HashSet<int> _selectedTypes = [];
        private readonly Dictionary<(ResolvedType, MethodDefinitionHandle), int> _instances = [];
        private readonly HashSet<(ResolvedType, MethodDefinitionHandle)> _limitedMethods = [];
        private readonly HashSet<int> _bodyRequired = [];
        private readonly HashSet<int> _processed = [];
        private bool _stopped;
        private long _bodyBytes;

        internal ReachabilityWalker(AssemblyResolver resolver, ReachabilityLimits limits, ResolvedAssembly? runtimeLibrary,
            ImmutableArray<ResolvedAssembly> contracts)
        {
            if (limits.MaximumNodes <= 0 || limits.MaximumEdges <= 0 || limits.MaximumMethodInstances <= 0
                || limits.MaximumBodyBytes <= 0 || limits.MaximumTotalBodyBytes <= 0 || limits.MaximumDispatchPairs <= 0)
                throw new ArgumentOutOfRangeException(nameof(limits), "Reachability limits must be positive.");
            _resolver = resolver;
            _limits = limits;
            _runtimeLibrary = runtimeLibrary;
            _contractAssemblies = contracts;
            _contractImages = contracts.Select(a => (a.Identity, a.ContentHash)).ToHashSet();
        }

        internal ReachabilityReport Run(ResolvedAssembly assembly, IEnumerable<EntityHandle> roots, bool publicSurface)
        {
            if (_runtimeLibrary is not null)
            {
                var profile = _resolver.ValidateRuntimeLibrary(_runtimeLibrary);
                _runtimeLibraryUnavailable = !profile.Success;
                foreach (Diagnostic diagnostic in profile.Diagnostics) Issue(null, null, diagnostic.Id, diagnostic.Message);
            }
            var selected = new List<EntityHandle>();
            Guard(null, null, () => selected.AddRange(publicSurface ? PublicRoots(assembly) : roots));
            if (selected.Count == 0) Issue(null, null, DiagnosticIds.ReachabilityIncomplete, "No reachability roots were selected.");
            foreach (EntityHandle root in selected.Distinct().OrderBy(MetadataTokens.GetToken))
            {
                if (_stopped) break;
                Guard(null, null, () =>
                {
                    if (root.IsNil) throw new BadImageFormatException("Nil reachability root token.");
                    ReachableEntity entity = root.Kind switch
                    {
                        HandleKind.TypeDefinition => new ReachableEntity.Type(_resolver.Owner(assembly, root)),
                        HandleKind.MethodDefinition => new ReachableEntity.Method(_resolver.Method(assembly, root)),
                        HandleKind.FieldDefinition => new ReachableEntity.Field(_resolver.Field(assembly, root)),
                        _ => throw new UnsupportedBindingException("Reachability roots must be TypeDef, MethodDef or FieldDef tokens."),
                    };
                    int? id = Add(entity, null, DependencyKind.SelectedTypeMember, null);
                    if (id is null) return;
                    _roots.Add(id.Value);
                    if (!publicSurface && entity is ReachableEntity.Type) _selectedTypes.Add(id.Value);
                }, $"root 0x{MetadataTokens.GetToken(root):x8} in {assembly.Identity.DisplayName}");
            }
            while (!_stopped && (_pending.Count > 0 || _readyDispatch.Count > 0))
            {
                if (_pending.TryDequeue(out int id))
                {
                    _processed.Add(id);
                    Guard(id, null, () => Expand(id));
                }
                if (!_stopped) ExpandDispatchPair();
            }
            foreach (DispatchSite site in _dispatchSites)
            {
                if (!site.HasTarget) Issue(site.Caller, site.Offset, DiagnosticIds.ReachabilityIncomplete,
                    "No supported concrete receiver target was established for this dispatch site.");
                if (_hasSyntheticReceivers && (site.Constraint is null
                    || (TypeDefinition(site.Constraint).Attributes & TypeAttributes.Sealed) == 0))
                    Issue(site.Caller, site.Offset, DiagnosticIds.ReachabilityIncomplete,
                    "Array/string receiver synthesis is outside the named-type dispatch profile; the receiver set may be incomplete.");
            }
            return new([.. _nodes], [.. _roots.Distinct()], [.. _edges], [.. _issues])
            {
                ContractAssemblies = _contractAssemblies,
                DeferredNodes = [.. _deferredNodes],
                RuntimeMappings = [.. _runtimeTypes.Where(p => p.Value.Success).OrderBy(p => p.Key)
                    .Select(p => new RuntimeTypeMapping(p.Key, p.Value.Value!))],
                Features = [.. _features.OrderBy(f => f.Node).ThenBy(f => f.ILOffset)
                    .ThenBy(f => f.Feature, StringComparer.Ordinal).ThenBy(f => f.Evidence, StringComparer.Ordinal)],
            };
        }

        private static IEnumerable<EntityHandle> PublicRoots(ResolvedAssembly assembly)
        {
            MetadataReader reader = assembly.Image.Reader;
            foreach (TypeDefinitionHandle type in reader.TypeDefinitions)
            {
                if (!Visible(type)) continue;
                yield return type;
                TypeDefinition definition = reader.GetTypeDefinition(type);
                foreach (MethodDefinitionHandle method in definition.GetMethods())
                    if ((reader.GetMethodDefinition(method).Attributes & MethodAttributes.MemberAccessMask)
                        is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem) yield return method;
                foreach (FieldDefinitionHandle field in definition.GetFields())
                    if ((reader.GetFieldDefinition(field).Attributes & FieldAttributes.FieldAccessMask)
                        is FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem) yield return field;
            }

            bool Visible(TypeDefinitionHandle type)
            {
                var seen = new HashSet<TypeDefinitionHandle>();
                while (!type.IsNil)
                {
                    if (!seen.Add(type) || seen.Count > 64) throw new BadImageFormatException("Cyclic or over-limit nested type visibility chain.");
                    TypeDefinition definition = reader.GetTypeDefinition(type);
                    if ((definition.Attributes & TypeAttributes.VisibilityMask) is not (TypeAttributes.Public
                        or TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem)) return false;
                    type = definition.GetDeclaringType();
                }
                return true;
            }
        }

        private int? Add(ReachableEntity entity, int? from, DependencyKind kind, int? offset,
            int? receiverType = null, int? constraintType = null)
        {
            if (_stopped) return null;
            bool exists = _ids.TryGetValue(entity, out int id);
            if (!exists)
            {
                if (_nodes.Count >= _limits.MaximumNodes) { Stop(from, offset, "Reachability node limit reached."); return null; }
                if (entity is ReachableEntity.Method method)
                {
                    var key = (method.Definition.DeclaringType.Definition, method.Definition.Handle);
                    int count = _instances.GetValueOrDefault(key);
                    if (count >= _limits.MaximumMethodInstances)
                    {
                        if (_limitedMethods.Add(key)) Issue(from, offset, DiagnosticIds.ReachabilityLimit, "Method instantiation limit reached; further instances were not expanded.");
                        return null;
                    }
                    _instances[key] = count + 1;
                }
                id = _nodes.Count;
                _ids.Add(entity, id);
                _nodes.Add(new(id, entity, from is null ? null : _edges.Count));
                _pending.Enqueue(id);
            }
            if (from is int parent)
            {
                var edge = new ReachabilityEdge(parent, id, kind, offset, receiverType, constraintType);
                if (!_edgeSet.Contains(edge))
                {
                    if (_edges.Count >= _limits.MaximumEdges)
                    {
                        // Roll back a newly discovered node so it never lacks its reason.
                        if (!exists) { _nodes.RemoveAt(id); _ids.Remove(entity); }
                        Stop(from, offset, "Reachability edge limit reached.");
                        return null;
                    }
                    _edgeSet.Add(edge);
                    _edges.Add(edge);
                }
            }
            // Register only after the incoming edge succeeds: rolled-back nodes
            // must never become receiver evidence.
            if (!exists && IsContract(entity)) _deferredNodes.Add(id);
            if (!exists && entity is ReachableEntity.Type or ReachableEntity.ArrayReceiver) RegisterReceiver(id);
            if (entity is ReachableEntity.Method required && kind != DependencyKind.DispatchDeclaration && _bodyRequired.Add(id)
                && exists && _processed.Contains(id) && (MethodDefinition(required.Definition).Attributes & MethodAttributes.Abstract) != 0)
                _pending.Enqueue(id);
            return id;
        }

        private void Expand(int id)
        {
            switch (_nodes[id].Entity)
            {
                case ReachableEntity.Type type: ExpandType(id, type.Definition); break;
                case ReachableEntity.ArrayReceiver array:
                    Add(new ReachableEntity.Type(array.BaseType), id, DependencyKind.BaseType, null);
                    break;
                case ReachableEntity.Method method: ExpandMethod(id, method.Definition); break;
                case ReachableEntity.Field field:
                    Add(new ReachableEntity.Type(field.Definition.DeclaringType), id, DependencyKind.DeclaringType, null);
                    Signature(id, field.Definition.Signature, DependencyKind.Signature);
                    break;
            }
        }

        private void ExpandType(int id, ConstructedType type)
        {
            ResolvedAssembly assembly = type.Definition.Assembly;
            MetadataReader reader = assembly.Image.Reader;
            TypeDefinition definition = reader.GetTypeDefinition(type.Definition.Handle);
            ObserveType(id, reader.GetString(definition.Namespace), reader.GetString(definition.Name));
            var context = new BindingContext(type, SignatureList<TypeSignature>.Empty);
            foreach (TypeSignature argument in type.Arguments) Signature(id, argument, DependencyKind.Signature);
            if (!definition.BaseType.IsNil) TypeOperand(id, assembly, definition.BaseType, context, DependencyKind.BaseType);
            foreach (InterfaceImplementationHandle handle in definition.GetInterfaceImplementations())
                Guard(id, null, () => TypeOperand(id, assembly, reader.GetInterfaceImplementation(handle).Interface, context, DependencyKind.Interface));
            Constraints(id, assembly, definition.GetGenericParameters(), context);
            // Retain the declaration's ancestry and substituted signature types,
            // but do not infer required library internals from Microsoft's layout.
            if (IsContract(assembly)) return;
            // Layout and initialization are conservative: include every field's type and
            // the initializer of every included type, even when execution may not trigger it.
            foreach (FieldDefinitionHandle field in definition.GetFields())
                Guard(id, null, () => Add(new ReachableEntity.Field(_resolver.Field(assembly, field, context)), id, DependencyKind.FieldLayout, null));
            foreach (MethodDefinitionHandle method in definition.GetMethods())
            {
                Guard(id, null, () =>
                {
                    bool initializer = reader.StringComparer.Equals(reader.GetMethodDefinition(method).Name, ".cctor");
                    if (initializer || _selectedTypes.Contains(id))
                        Add(new ReachableEntity.Method(_resolver.Method(assembly, method, context)), id,
                            initializer ? DependencyKind.TypeInitializer : DependencyKind.SelectedTypeMember, null);
                });
            }
        }

        private void Constraints(int id, ResolvedAssembly assembly, GenericParameterHandleCollection parameters, BindingContext context)
        {
            MetadataReader reader = assembly.Image.Reader;
            foreach (GenericParameterHandle parameter in parameters)
                foreach (GenericParameterConstraintHandle constraint in reader.GetGenericParameter(parameter).GetConstraints())
                    Guard(id, null, () => TypeOperand(id, assembly, reader.GetGenericParameterConstraint(constraint).Type, context, DependencyKind.GenericConstraint));
        }

        private void ExpandMethod(int id, ResolvedMethod method)
        {
            ResolvedAssembly assembly = method.DeclaringType.Definition.Assembly;
            MetadataReader reader = assembly.Image.Reader;
            MethodDefinition definition = reader.GetMethodDefinition(method.Handle);
            if ((definition.Attributes & MethodAttributes.Virtual) != 0 && method.Signature.GenericArity > 0)
                Observe(id, null, "generic-virtual", "Generic virtual method declaration.");
            ObserveDynamicMethod(id, method);
            var context = new BindingContext(method.DeclaringType, method.MethodArguments);
            Add(new ReachableEntity.Type(method.DeclaringType), id, DependencyKind.DeclaringType, null);
            Signature(id, method.Signature.ReturnType, DependencyKind.Signature);
            foreach (TypeSignature parameter in method.Signature.Parameters) Signature(id, parameter, DependencyKind.Signature);
            foreach (TypeSignature argument in method.MethodArguments) Signature(id, argument, DependencyKind.Signature);
            Constraints(id, assembly, definition.GetGenericParameters(), context);
            if (IsContract(assembly)) return;
            ModuleInitializer(id, assembly);
            if ((definition.Attributes & MethodAttributes.Abstract) != 0 && !_bodyRequired.Contains(id)) return;
            if (assembly.IsReferenceAssembly || definition.RelativeVirtualAddress == 0
                || (definition.ImplAttributes & (MethodImplAttributes.CodeTypeMask | MethodImplAttributes.InternalCall | MethodImplAttributes.Unmanaged)) != MethodImplAttributes.IL
                || (definition.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                Issue(id, null, DiagnosticIds.ReachabilityIncomplete, assembly.IsReferenceAssembly
                    ? "Reference-assembly declarations do not establish an implementation body."
                    : "Abstract, runtime-provided, native or missing method body cannot establish an implementation closure.");
                return;
            }
            var body = assembly.Image.PEReader.GetMethodBody(definition.RelativeVirtualAddress);
            BlobReader il = body.GetILReader();
            if (il.Length > _limits.MaximumBodyBytes) { Issue(id, null, DiagnosticIds.ReachabilityLimit, "Method body exceeds the byte limit."); return; }
            _bodyBytes += il.Length;
            if (_bodyBytes > _limits.MaximumTotalBodyBytes) { Stop(id, null, "Total decoded method body byte limit reached."); return; }
            int localCount = 0;
            Guard(id, null, () =>
            {
                if (body.LocalSignature.IsNil) return;
                var locals = new SignatureDecoder(_resolver, assembly).Locals(reader.GetStandaloneSignature(body.LocalSignature).Signature);
                localCount = locals.Count;
                foreach (TypeSignature local in locals) Signature(id, InContext(local, context), DependencyKind.Local);
            });
            foreach (ExceptionRegion region in body.ExceptionRegions)
                if (region.Kind == ExceptionRegionKind.Catch)
                    TypeOperand(id, assembly, region.CatchType, context, DependencyKind.CatchType, region.HandlerOffset);
            List<Instruction> instructions = ILDecoder.Decode(il, out string? error);
            if (error is not null) Issue(id, null, DiagnosticIds.MalformedMethodBody, error);
            var calls = new Dictionary<int, ResolvedMethod>();
            var constraints = ConstrainedPrefixes(id, instructions);
            foreach (Instruction instruction in instructions)
            {
                if (_stopped) break;
                Guard(id, instruction.Offset, () => Instruction(id, assembly, instruction, context, calls,
                    constraints.GetValueOrDefault(instruction.Offset)));
            }
            // Reuse the existing structural validator; conservative dependency discovery
            // scans all decoded instructions, including blocks it labels unreachable.
            if (error is null && !_stopped)
            {
                if ((long)instructions.Count * Math.Max(1, body.ExceptionRegions.Length) > 1000000)
                {
                    Issue(id, null, DiagnosticIds.ReachabilityLimit, "Control-flow validation exceeds the instruction/exception-region work limit.");
                    return;
                }
                var analyzer = new ControlFlowAnalyzer(instructions, il.Length, body.MaxStack,
                    method.Signature.Parameters.Count + ((method.Signature.Header & 0x20) != 0 ? 1 : 0), localCount,
                    !IsVoid(method.Signature.ReturnType), instruction => calls.TryGetValue(instruction.Offset, out var target)
                        ? new CallShape(target.Signature.Parameters.Count + (instruction.Code.Name != "newobj" && (target.Signature.Header & 0x20) != 0 ? 1 : 0),
                            instruction.Code.Name == "newobj" || !IsVoid(target.Signature.ReturnType) ? 1 : 0) : null,
                    (code, message, offset) => Issue(id, offset, code, message));
                var flow = analyzer.Analyze(body.ExceptionRegions);
                foreach (var filter in flow.FilterKinds)
                    Observe(id, body.ExceptionRegions[filter.Key].FilterOffset, "filter-" + filter.Value,
                        "Exception filter region " + filter.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
        }

        private void ModuleInitializer(int id, ResolvedAssembly assembly)
        {
            MetadataReader reader = assembly.Image.Reader;
            foreach (MethodDefinitionHandle method in reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1)).GetMethods())
                if (reader.StringComparer.Equals(reader.GetMethodDefinition(method).Name, ".cctor"))
                    Guard(id, null, () => Add(new ReachableEntity.Method(_resolver.Method(assembly, method)), id, DependencyKind.ModuleInitializer, null));
        }

        private void Instruction(int id, ResolvedAssembly assembly, Instruction instruction, BindingContext context,
            Dictionary<int, ResolvedMethod> calls, ConstrainedPrefix? constraint)
        {
            int offset = instruction.Offset;
            ObserveInstruction(id, instruction);
            if (instruction.Code.Name == "newarr")
            {
                TypeSignature element = InContext(new SignatureDecoder(_resolver, assembly).Type(Token(instruction.Token)), context);
                Signature(id, new TypeSignature.Vector(element), DependencyKind.TypeOperand, offset);
                return;
            }
            // Decode the prefix with its following operation. A failed prefix must
            // never silently turn the operation into an unconstrained dispatch.
            if (instruction.Code.Name == "constrained.") return;
            if (instruction.Code.Name is "calli" or "jmp")
                Issue(id, offset, DiagnosticIds.ReachabilityIncomplete, $"'{instruction.Code.Name}' requires additional indirect dispatch analysis.");
            if (instruction.Code.Operand == OperandKind.StringToken)
            {
                if ((uint)instruction.Token >> 24 != 0x70) throw new BadImageFormatException("Invalid user-string token.");
                assembly.Image.Reader.GetUserString(MetadataTokens.UserStringHandle(instruction.Token & 0x00ffffff));
                StringReceiver(id, offset);
                return;
            }
            if (instruction.Code.Operand == OperandKind.SignatureToken)
            {
                if ((uint)instruction.Token >> 24 != 0x11) throw new BadImageFormatException("Invalid standalone signature token.");
                var handle = MetadataTokens.StandaloneSignatureHandle(instruction.Token & 0x00ffffff);
                MethodSignature signature = new SignatureDecoder(_resolver, assembly).Method(assembly.Image.Reader.GetStandaloneSignature(handle).Signature);
                Signature(id, InContext(signature.ReturnType, context), DependencyKind.Signature, offset);
                foreach (var parameter in signature.Parameters) Signature(id, InContext(parameter, context), DependencyKind.Signature, offset);
                return;
            }
            if (instruction.Code.Operand is not (OperandKind.MethodToken or OperandKind.FieldToken or OperandKind.TypeToken or OperandKind.AnyToken)) return;
            EntityHandle token = Token(instruction.Token);
            bool isMethod = token.Kind is HandleKind.MethodDefinition or HandleKind.MethodSpecification
                || token.Kind == HandleKind.MemberReference && assembly.Image.Reader.GetMemberReference((MemberReferenceHandle)token).GetKind() == MemberReferenceKind.Method;
            bool isField = token.Kind == HandleKind.FieldDefinition || token.Kind == HandleKind.MemberReference && !isMethod;
            if (instruction.Code.Operand == OperandKind.MethodToken || instruction.Code.Operand == OperandKind.AnyToken && isMethod)
            {
                ResolvedMethod target = _resolver.Method(assembly, token, context);
                calls[offset] = target;
                if (constraint is not null)
                {
                    ConstrainedCall(id, assembly, instruction, context, target, constraint);
                    return;
                }
                DependencyKind kind = instruction.Code.Name switch
                {
                    "newobj" => DependencyKind.Constructor,
                    "ldftn" or "ldvirtftn" => DependencyKind.MethodAddress,
                    "ldtoken" => DependencyKind.MetadataToken,
                    _ => DependencyKind.Call,
                };
                MethodDefinition definition = target.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(target.Handle);
                bool dispatch = instruction.Code.Name == "ldvirtftn" || instruction.Code.Name == "callvirt" && (definition.Attributes & MethodAttributes.Virtual) != 0;
                Add(new ReachableEntity.Method(target), id, dispatch ? DependencyKind.DispatchDeclaration : kind, offset);
                if (dispatch) RegisterDispatch(new(id, offset, target));
            }
            else if (instruction.Code.Operand == OperandKind.FieldToken || instruction.Code.Operand == OperandKind.AnyToken && isField)
                Add(new ReachableEntity.Field(_resolver.Field(assembly, token, context)), id,
                    instruction.Code.Name == "ldtoken" ? DependencyKind.MetadataToken : DependencyKind.FieldAccess, offset);
            else TypeOperand(id, assembly, token, context,
                instruction.Code.Name == "ldtoken" ? DependencyKind.MetadataToken : DependencyKind.TypeOperand, offset);
        }

        private static EntityHandle Token(int token)
        {
            uint tag = (uint)token >> 24;
            if ((token & 0x00ffffff) == 0 || tag is not (0x01 or 0x02 or 0x04 or 0x06 or 0x0a or 0x1b or 0x2b))
                throw new BadImageFormatException("Invalid type/member instruction token.");
            return MetadataTokens.EntityHandle(token);
        }

        private void TypeOperand(int id, ResolvedAssembly assembly, EntityHandle token, BindingContext context, DependencyKind kind, int? offset = null) =>
            Guard(id, offset, () => Signature(id, InContext(new SignatureDecoder(_resolver, assembly).Type(token), context), kind, offset));

        private void Signature(int id, TypeSignature signature, DependencyKind kind, int? offset = null)
        {
            if (_stopped) return;
            if (signature is TypeSignature.Pointer or TypeSignature.FunctionPointer or TypeSignature.Pinned)
                Observe(id, offset, "unsafe", "Pointer, function-pointer or pinned signature.");
            if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Single or PrimitiveTypeCode.Double })
                Observe(id, offset, "floating-point", "Floating-point signature.");
            switch (signature)
            {
                case TypeSignature.Primitive { Code: PrimitiveTypeCode.String }: StringReceiver(id, offset); break;
                case TypeSignature.Primitive primitive when kind == DependencyKind.TypeOperand
                    && primitive.Code is not (PrimitiveTypeCode.Void or PrimitiveTypeCode.TypedReference):
                    PrimitiveReceiver(id, primitive.Code, DependencyKind.RuntimeType, offset, required: false);
                    break;
                case TypeSignature.Named named: Add(new ReachableEntity.Type(OpenType(named.Definition)), id, kind, offset); break;
                case TypeSignature.Generic generic:
                    Add(new ReachableEntity.Type(new(generic.Definition.Definition, generic.Arguments)), id, kind, offset);
                    foreach (var argument in generic.Arguments) Signature(id, argument, kind, offset);
                    break;
                case TypeSignature.Variable: Issue(id, offset, DiagnosticIds.ReachabilityIncomplete, "Open generic variables require a concrete caller environment; this graph is incomplete."); break;
                case TypeSignature.Vector vector: ArrayReceiver(id, vector, kind, offset); Signature(id, vector.Element, kind, offset); break;
                case TypeSignature.Array array: ArrayReceiver(id, array, kind, offset); Signature(id, array.Element, kind, offset); break;
                case TypeSignature.Pointer pointer: Signature(id, pointer.Element, kind, offset); break;
                case TypeSignature.ByReference reference: Signature(id, reference.Element, kind, offset); break;
                case TypeSignature.Pinned pinned: Signature(id, pinned.Element, kind, offset); break;
                case TypeSignature.Modified modified:
                    Signature(id, modified.Modifier, kind, offset);
                    Signature(id, modified.Element, kind, offset);
                    break;
                case TypeSignature.FunctionPointer pointer:
                    Signature(id, pointer.Signature.ReturnType, kind, offset);
                    foreach (var parameter in pointer.Signature.Parameters) Signature(id, parameter, kind, offset);
                    break;
            }
        }

        private static bool IsVoid(TypeSignature type) => type switch
        {
            TypeSignature.Modified modified => IsVoid(modified.Element),
            TypeSignature.Primitive primitive => primitive.Code == PrimitiveTypeCode.Void,
            _ => false,
        };

        private void Stop(int? id, int? offset, string message)
        {
            Issue(id, offset, DiagnosticIds.ReachabilityLimit, message);
            _stopped = true;
        }

        private void Issue(int? id, int? offset, string code, string message, string? location = null)
        {
            var issue = new ReachabilityIssue(id, offset, new(code, DiagnosticSeverity.Error, message,
                location ?? (id is null ? null : $"node {id}" + (offset is null ? "" : " " + ILDecoder.Label(offset.Value)))));
            if (_issueSet.Add(issue)) _issues.Add(issue);
        }

        private void Guard(int? id, int? offset, Action action, string? location = null)
        {
            if (_stopped) return;
            try { action(); }
            catch (BindingFailureException exception)
            {
                foreach (Diagnostic diagnostic in exception.Diagnostics) Issue(id, offset, diagnostic.Id, diagnostic.Message, location);
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
            {
                Issue(id, offset, exception is UnsupportedBindingException ? DiagnosticIds.ResolutionUnsupported : DiagnosticIds.MalformedEntity,
                    exception.Message, location);
            }
        }
    }
}
