using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    // Constraint evidence uses declarations only: no body execution, dispatch plan
    // construction or recursive ReadClosedTypeInput calls. Every inspected owner
    // is queued for the caller's shared closed-constraint validation ledger.
    private HashSet<ConstructedType> ReadConstraintInterfaces(ConstructedType root, Action member,
        Action<ConstructedType> validate)
    {
        var interfaces = new HashSet<ConstructedType>();
        var complete = new HashSet<ConstructedType>();
        var active = new HashSet<ResolvedType>();
        void Walk(ConstructedType type, bool? expectedInterface, int depth)
        {
            CheckOwner(type.Definition.Assembly); member();
            var reader = type.Definition.Assembly.Image.Reader;
            var definition = reader.GetTypeDefinition(type.Definition.Handle);
            bool isInterface = (definition.Attributes & TypeAttributes.Interface) != 0;
            if (expectedInterface is { } expected && expected != isInterface)
                throw new BadImageFormatException("Constraint ancestry has an interface/class edge mismatch.");
            if (isInterface && ((definition.Attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed)) != TypeAttributes.Abstract
                || !definition.BaseType.IsNil))
                throw new BadImageFormatException("Constraint interfaces must be abstract, unsealed and without a class base.");
            if (active.Contains(type.Definition))
                throw Failure(DiagnosticIds.TypeResolutionCycle, "Cyclic nominal ancestry in interface constraints.");
            if (complete.Contains(type)) return;
            if (depth > 64 || complete.Count + active.Count >= 256)
                throw Failure(DiagnosticIds.TranslationLimit, "Interface constraint ancestry exceeds 64 levels or 256 types.");
            // Authenticate arity and supported closed signatures even for phantom edges.
            if (definition.GetGenericParameters().Count != type.Arguments.Count)
                throw new BadImageFormatException("Interface constraint ancestry has mismatched generic arity.");
            try { ClosedGenericKey.Create(type.Arguments, SignatureList<TypeSignature>.Empty); }
            catch (ArgumentException error) { throw new UnsupportedBindingException(error.Message); }
            active.Add(type.Definition); validate(type);
            if (isInterface) interfaces.Add(type);
            var edges = new HashSet<ConstructedType>();
            foreach (var handle in definition.GetInterfaceImplementations())
            {
                member();
                var signature = new SignatureDecoder(this, type.Definition.Assembly).Type(reader.GetInterfaceImplementation(handle).Interface);
                if (signature is TypeSignature.Named { Kind: SignatureTypeKind.ValueType }
                    or TypeSignature.Generic { Definition.Kind: SignatureTypeKind.ValueType })
                    throw new BadImageFormatException("An interface edge cannot use a value-type encoding.");
                var parent = Owner(Substitute(signature, type.Arguments, SignatureList<TypeSignature>.Empty));
                if (!edges.Add(parent)) throw new BadImageFormatException("Duplicate direct interface in constraint ancestry.");
                Walk(parent, true, depth + 1);
            }
            if (!definition.BaseType.IsNil)
            {
                member();
                BaseTypes(type, type, out var parent, out _);
                Walk(parent, false, depth + 1);
            }
            active.Remove(type.Definition); complete.Add(type);
        }
        Walk(root, null, 0);
        return interfaces;
    }
}
