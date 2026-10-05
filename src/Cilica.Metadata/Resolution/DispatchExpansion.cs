using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Analysis;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private sealed partial class ReachabilityWalker
    {
        private sealed class DispatchSite(int caller, int offset, ResolvedMethod declaration,
            ConstructedType? constraint = null, int? constraintId = null)
        {
            internal int Caller { get; } = caller;
            internal int Offset { get; } = offset;
            internal ResolvedMethod Declaration { get; } = declaration;
            internal ConstructedType? Constraint { get; } = constraint;
            internal int? ConstraintId { get; } = constraintId;
            internal bool HasTarget { get; set; }
            internal int NextReceiver { get; set; }
            internal int? ExactReceiver { get; set; }
        }

        private readonly List<DispatchSite> _dispatchSites = [];
        private bool _hasSyntheticReceivers;
        private int _dispatchWork;

        private void CountDispatchWork()
        {
            if (++_dispatchWork <= 1000000) return;
            _stopped = true;
            throw Failure(DiagnosticIds.ReachabilityLimit, "Dispatch metadata work limit reached.");
        }

        private static TypeDefinition TypeDefinition(ConstructedType type) =>
            type.Definition.Assembly.Image.Reader.GetTypeDefinition(type.Definition.Handle);

        private static MethodDefinition MethodDefinition(ResolvedMethod method) =>
            method.DeclaringType.Definition.Assembly.Image.Reader.GetMethodDefinition(method.Handle);

        private void Dispatch(DispatchSite site, ConstructedType receiver, int receiverId)
        {
            TypeAttributes attributes = TypeDefinition(receiver).Attributes;
            if ((attributes & (TypeAttributes.Interface | TypeAttributes.Abstract)) != 0) return;
            DispatchHierarchy(site, Hierarchy(receiver), receiverId);
        }

        private void DispatchHierarchy(DispatchSite site, List<ConstructedType> hierarchy, int receiverId)
        {
            if (site.Constraint is { } constraint)
            {
                bool isInterfaceConstraint = (TypeDefinition(constraint).Attributes & TypeAttributes.Interface) != 0;
                if (isInterfaceConstraint ? !Implements(hierarchy, constraint) : !hierarchy.Contains(constraint)) return;
            }
            foreach (ConstructedType type in hierarchy) RejectClassMethodImpl(type);
            bool isInterface = (TypeDefinition(site.Declaration.DeclaringType).Attributes & TypeAttributes.Interface) != 0;
            if (isInterface)
            {
                if (Implements(hierarchy, site.Declaration.DeclaringType)) InterfaceTargets(site, hierarchy, receiverId);
            }
            else if (hierarchy.Contains(site.Declaration.DeclaringType))
            {
                if ((MethodDefinition(site.Declaration).Attributes & MethodAttributes.Virtual) != 0)
                    ClassTargets(site, hierarchy, site.Declaration, receiverId);
                else IncludeTarget(site, site.Declaration, receiverId);
            }
        }

        private List<ConstructedType> Hierarchy(ConstructedType receiver)
        {
            var result = new List<ConstructedType>();
            var seen = new HashSet<ResolvedType>();
            while (true)
            {
                CountDispatchWork();
                Visit(receiver, seen);
                result.Add(receiver);
                if (!_resolver.BaseTypes(receiver, receiver, out var parent, out _)) return result;
                receiver = parent;
            }
        }

        private bool Implements(List<ConstructedType> hierarchy, ConstructedType contract)
        {
            var pending = new Queue<(ConstructedType Type, HashSet<ResolvedType> Path)>(hierarchy.Select(t => (t, new HashSet<ResolvedType> { t.Definition })));
            var seen = new HashSet<ConstructedType>();
            bool found = false;
            while (pending.TryDequeue(out var entry))
            {
                CountDispatchWork();
                ConstructedType type = entry.Type;
                if (!seen.Add(type)) continue;
                if (seen.Count > 256) throw new UnsupportedBindingException("Interface ancestry exceeds the 256-type dispatch limit.");
                if (type == contract) found = true;
                else if (type.Definition == contract.Definition && TypeDefinition(type).GetGenericParameters().Any(h =>
                    (type.Definition.Assembly.Image.Reader.GetGenericParameter(h).Attributes & GenericParameterAttributes.VarianceMask) != 0))
                    throw new UnsupportedBindingException("Variant interface conversion requires a later dispatch profile.");
                MetadataReader reader = type.Definition.Assembly.Image.Reader;
                foreach (InterfaceImplementationHandle handle in TypeDefinition(type).GetInterfaceImplementations())
                {
                    CountDispatchWork();
                    if (pending.Count + seen.Count >= 256) throw new UnsupportedBindingException("Interface ancestry exceeds the 256-entry dispatch work limit.");
                    ConstructedType parent = _resolver.Owner(type.Definition.Assembly, reader.GetInterfaceImplementation(handle).Interface,
                        new(type, SignatureList<TypeSignature>.Empty));
                    if (entry.Path.Contains(parent.Definition)) throw new BadImageFormatException("Cyclic interface ancestry in dispatch.");
                    pending.Enqueue((parent, new(entry.Path) { parent.Definition }));
                }
            }
            return found;
        }

        private ResolvedMethod Candidate(ConstructedType owner, MethodDefinitionHandle handle, SignatureList<TypeSignature> arguments)
        {
            MethodSignature signature = _resolver.MethodDefinitionSignature(owner.Definition.Assembly, handle);
            if (signature.GenericArity != arguments.Count) throw new BadImageFormatException("Dispatch method arity mismatch.");
            return new(owner, handle, arguments, Substitute(signature, owner.Arguments, arguments));
        }

        // Override comparison substitutes owner arguments but retains method variables.
        // Otherwise M<T>(T) and M<T>(int) would collapse for a call with T=int.
        private MethodSignature OpenSignature(ResolvedMethod method) => Substitute(
            _resolver.MethodDefinitionSignature(method.DeclaringType.Definition.Assembly, method.Handle),
            method.DeclaringType.Arguments, Variables(method.MethodArguments.Count, true));

        private List<ResolvedMethod> Matches(ConstructedType owner, ResolvedMethod target, bool publicOnly)
        {
            var matches = new List<ResolvedMethod>();
            MetadataReader reader = owner.Definition.Assembly.Image.Reader;
            string name = target.DeclaringType.Definition.Assembly.Image.Reader.GetString(MethodDefinition(target).Name);
            MethodSignature wanted = OpenSignature(target);
            foreach (MethodDefinitionHandle handle in TypeDefinition(owner).GetMethods())
            {
                CountDispatchWork();
                MethodDefinition definition = reader.GetMethodDefinition(handle);
                if (!reader.StringComparer.Equals(definition.Name, name) || (definition.Attributes & MethodAttributes.Static) != 0) continue;
                if (publicOnly && (definition.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) continue;
                if (!publicOnly && (definition.Attributes & MethodAttributes.Virtual) == 0) continue;
                if (definition.GetGenericParameters().Count != target.MethodArguments.Count) continue;
                ResolvedMethod candidate = Candidate(owner, handle, target.MethodArguments);
                MethodSignature signature = OpenSignature(candidate);
                if (signature == wanted) matches.Add(candidate);
                else if (signature.Header == wanted.Header && signature.GenericArity == wanted.GenericArity && signature.Parameters.Equals(wanted.Parameters))
                    throw new UnsupportedBindingException("Covariant or otherwise differing dispatch return signatures require a later profile.");
            }
            if (matches.Count > 1) throw new UnsupportedBindingException("Multiple methods match an instantiated dispatch signature.");
            return matches;
        }

        private void ClassTargets(DispatchSite site, List<ConstructedType> hierarchy, ResolvedMethod target, int receiverId)
        {
            int ownerIndex = hierarchy.IndexOf(target.DeclaringType);
            if (ownerIndex < 0) throw new BadImageFormatException("Dispatch method owner is outside the receiver hierarchy.");
            // Start at the operand declaration. Reuse-slot overrides below it belong
            // to this slot until a newslot declaration hides its name/signature.
            IncludeTarget(site, target, receiverId);
            bool hidden = false;
            ResolvedMethod effective = target;
            bool final = (MethodDefinition(target).Attributes & MethodAttributes.Final) != 0;
            for (int i = ownerIndex - 1; i >= 0; i--)
            {
                ConstructedType type = hierarchy[i];
                foreach (ResolvedMethod candidate in Matches(type, target, publicOnly: false))
                {
                    MethodAttributes flags = MethodDefinition(candidate).Attributes;
                    if ((flags & MethodAttributes.NewSlot) != 0)
                    {
                        if ((flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Private)
                            throw new UnsupportedBindingException("Private newslot hiding requires access-aware dispatch analysis.");
                        hidden = true;
                        continue;
                    }
                    if (hidden) continue;
                    if (final) throw new BadImageFormatException("A dispatch override attempts to replace a final virtual method.");
                    IncludeTarget(site, candidate, receiverId);
                    effective = candidate;
                    final = (flags & MethodAttributes.Final) != 0;
                }
            }
            if ((MethodDefinition(effective).Attributes & MethodAttributes.Abstract) != 0)
                throw new UnsupportedBindingException("Concrete receiver has no supported concrete body for the effective virtual slot.");
        }

        private void RejectClassMethodImpl(ConstructedType type)
        {
            MetadataReader reader = type.Definition.Assembly.Image.Reader;
            foreach (MethodImplementationHandle handle in TypeDefinition(type).GetMethodImplementations())
            {
                CountDispatchWork();
                var mapping = reader.GetMethodImplementation(handle);
                ResolvedMethod declaration = ResolveAuditedMethod(type.Definition.Assembly, mapping.MethodDeclaration,
                    new(type, SignatureList<TypeSignature>.Empty));
                if ((TypeDefinition(declaration.DeclaringType).Attributes & TypeAttributes.Interface) == 0)
                    throw new UnsupportedBindingException("Explicit class virtual-slot remapping requires a later dispatch profile.");
            }
        }

        private void InterfaceTargets(DispatchSite site, List<ConstructedType> hierarchy, int receiverId)
        {
            bool found = false;
            foreach (ConstructedType type in hierarchy)
            {
                MetadataReader reader = type.Definition.Assembly.Image.Reader;
                int mappings = 0;
                foreach (MethodImplementationHandle handle in TypeDefinition(type).GetMethodImplementations())
                {
                    CountDispatchWork();
                    var mapping = reader.GetMethodImplementation(handle);
                    var context = new BindingContext(type, SignatureList<TypeSignature>.Empty);
                    ResolvedMethod declaration = ResolveAuditedMethod(type.Definition.Assembly, mapping.MethodDeclaration, context);
                    if (declaration.DeclaringType != site.Declaration.DeclaringType || declaration.Handle != site.Declaration.Handle) continue;
                    if (++mappings > 1) throw new BadImageFormatException("Duplicate explicit implementations for one interface declaration.");
                    ResolvedMethod body = ResolveAuditedMethod(type.Definition.Assembly, mapping.MethodBody, context);
                    if (body.DeclaringType != type || (MethodDefinition(body).Attributes & MethodAttributes.Static) != 0)
                        throw new BadImageFormatException("Explicit interface body must be an instance method on the implementing type.");
                    body = Candidate(type, body.Handle, site.Declaration.MethodArguments);
                    if (OpenSignature(body) != OpenSignature(site.Declaration))
                        throw new UnsupportedBindingException("Explicit interface declaration/body signatures differ.");
                    found = true;
                    ClassTargets(site, hierarchy, body, receiverId);
                }
                // Retain all public matching bodies conservatively, including inherited
                // ones. This avoids pretending to implement the runtime's full interface map.
                foreach (ResolvedMethod candidate in Matches(type, site.Declaration, publicOnly: true))
                {
                    found = true;
                    if ((MethodDefinition(candidate).Attributes & MethodAttributes.Virtual) != 0) ClassTargets(site, hierarchy, candidate, receiverId);
                    else IncludeTarget(site, candidate, receiverId);
                }
            }
            if (!found) throw new UnsupportedBindingException("No class implementation found; default interface dispatch or missing implementation remains unresolved.");
        }

        private void IncludeTarget(DispatchSite site, ResolvedMethod target, int receiverId)
        {
            if ((MethodDefinition(target).Attributes & MethodAttributes.Abstract) != 0) return;
            if (Add(new ReachableEntity.Method(target), site.Caller, DependencyKind.ConservativeDispatch,
                site.Offset, receiverId, site.ConstraintId) is not null)
                site.HasTarget = true;
        }
    }
}
