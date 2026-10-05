using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    // Work is charged back to the same metadata snapshot ledger. No runtime types
    // or constructors are loaded/executed to prove these declaration obligations.
    private int ValidateClosedConstraints(ConstructedType owner, ResolvedMethod? method,
        ResolvedAssembly library, int maximumWork)
    {
        CheckOwner(library);
        int work = 0;
        ResolvedType? valueBase = null, nullable = null;
        bool nullableRead = false;
        var pending = new Queue<ConstructedType>();
        var seen = new HashSet<ConstructedType>();
        void Member()
        {
            if (++work > maximumWork)
                throw Failure(DiagnosticIds.TranslationLimit, "Closed generic constraints exceed the metadata-member limit.");
        }
        ResolvedType ValueBase() => valueBase ??= ResolveType(library, "System", "ValueType").Value
            ?? throw new UnsupportedBindingException("Generic value constraints require the selected System.ValueType.");
        bool IsNullable(ResolvedType type)
        {
            if (!nullableRead)
            {
                Member(); var result = ResolveType(library, "System", "Nullable`1");
                if (result.Value is null && result.Diagnostics.Any(d => d.Id != DiagnosticIds.TypeNotFound))
                    throw new UnsupportedBindingException("Selected Nullable identity could not be resolved for the struct constraint.");
                nullable = result.Value; nullableRead = true;
            }
            return type == nullable;
        }
        void Arguments(SignatureList<TypeSignature> arguments)
        {
            var stack = new Stack<TypeSignature>(arguments); int count = 0;
            while (stack.TryPop(out var signature))
            {
                if (++count > 4096) throw Failure(DiagnosticIds.TranslationLimit, "Generic argument constraints exceed 4096 signature nodes.");
                switch (signature)
                {
                    case TypeSignature.Named named:
                        RejectByRefLike(named.Definition);
                        break;
                    case TypeSignature.Generic generic:
                        pending.Enqueue(new(generic.Definition.Definition, generic.Arguments));
                        break; // That declaration will validate and enqueue its own arguments.
                    case TypeSignature.Vector vector: stack.Push(vector.Element); break;
                }
            }
        }
        void RejectByRefLike(ResolvedType type)
        {
            CheckOwner(type.Assembly); Member();
            if (TypeMarkers.IsByRefLike(type, Member))
                throw new UnsupportedBindingException("ByRefLike type arguments require stack-only lifetime and escape checking before closed specialization.");
        }
        (bool Value, bool Nullable, ResolvedType? Definition) Traits(TypeSignature signature)
        {
            if (signature is TypeSignature.Primitive integer && ClosedGenericKey.IsIntegerArgument(integer.Code)) return (true, false, null);
            if (signature is TypeSignature.Vector) return (false, false, null);
            if (signature is TypeSignature.Primitive { Code: PrimitiveTypeCode.Object or PrimitiveTypeCode.String } reference)
                signature = new TypeSignature.Named(ResolveRuntimeType(library, reference.Code).Value
                    ?? throw new UnsupportedBindingException("Generic reference constraints require the selected Object/String."), SignatureTypeKind.Class);
            var named = signature switch { TypeSignature.Named n => n, TypeSignature.Generic g => g.Definition, _ => null };
            if (named is null) throw new UnsupportedBindingException("Generic constraints require a supported closed argument.");
            CheckOwner(named.Definition.Assembly); Member();
            var definition = named.Definition.Assembly.Image.Reader.GetTypeDefinition(named.Definition.Handle);
            var parent = definition.BaseType.IsNil ? null : new SignatureDecoder(this, named.Definition.Assembly).Type(definition.BaseType);
            bool isValue = parent is TypeSignature.Named baseType && baseType.Definition == ValueBase();
            if ((named.Kind == SignatureTypeKind.ValueType) != isValue)
                throw new UnsupportedBindingException("Generic constraint argument has unsupported value ancestry or a mismatched class/value signature.");
            return (isValue, isValue && IsNullable(named.Definition), named.Definition);
        }
        bool PublicDefaultConstructor(ResolvedType? type)
        {
            if (type is null) return false; // Arrays have no public parameterless constructor.
            Member(); var reader = type.Assembly.Image.Reader;
            var definition = reader.GetTypeDefinition(type.Handle);
            if ((definition.Attributes & (TypeAttributes.Abstract | TypeAttributes.Interface)) != 0) return false;
            foreach (var handle in definition.GetMethods())
            {
                Member(); var candidate = reader.GetMethodDefinition(handle);
                if (!reader.StringComparer.Equals(candidate.Name, ".ctor")
                    || (candidate.Attributes & (MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.Abstract
                        | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))
                        != (MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))
                    continue;
                var signature = MethodDefinitionSignature(type.Assembly, handle);
                if (signature.Header == 0x20 && signature.GenericArity == 0 && signature.Parameters.Count == 0
                    && signature.ReturnType is TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }) return true;
            }
            return false;
        }
        void Parameters(ResolvedAssembly assembly, GenericParameterHandleCollection parameters, SignatureList<TypeSignature> arguments, bool interfaceOwner = false)
        {
            if (parameters.Count != arguments.Count || parameters.Count > 4096)
                throw new BadImageFormatException("Closed generic constraints have mismatched declaration arity.");
            var indices = new HashSet<int>();
            foreach (var handle in parameters)
            {
                Member(); var parameter = assembly.Image.Reader.GetGenericParameter(handle);
                if (parameter.Index >= arguments.Count || !indices.Add(parameter.Index))
                    throw new BadImageFormatException("Generic parameter indices must uniquely cover the declaration.");
                const GenericParameterAttributes allowed = GenericParameterAttributes.ReferenceTypeConstraint
                    | GenericParameterAttributes.NotNullableValueTypeConstraint | GenericParameterAttributes.DefaultConstructorConstraint;
                var variance = parameter.Attributes & GenericParameterAttributes.VarianceMask;
                if ((parameter.Attributes & ~(allowed | GenericParameterAttributes.VarianceMask | GenericParameterAttributes.AllowByRefLike)) != 0
                    || variance == GenericParameterAttributes.VarianceMask || !interfaceOwner && variance != GenericParameterAttributes.None)
                    throw new UnsupportedBindingException("Variance flags require an interface type parameter with one declared direction; unknown flags and variant class/method/delegate parameters require later lowering.");
                // AllowByRefLike broadens declaration admission only. Argument traversal
                // rejects actual byref-like arguments, including nested/unused ones.
                bool reference = (parameter.Attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
                bool value = (parameter.Attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;
                bool construct = (parameter.Attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;
                if (reference && value) throw new BadImageFormatException("A generic parameter cannot require both reference and value types.");
                var traits = (parameter.Attributes & allowed) != GenericParameterAttributes.None ? Traits(arguments[parameter.Index]) : default;
                if (reference && traits.Value || value && (!traits.Value || traits.Nullable)
                    || construct && !traits.Value && !PublicDefaultConstructor(traits.Definition))
                    throw new UnsupportedBindingException("Closed argument does not satisfy its class, struct or public parameterless-constructor constraints.");
                foreach (var constraintHandle in parameter.GetConstraints())
                {
                    Member(); var constraint = assembly.Image.Reader.GetGenericParameterConstraint(constraintHandle);
                    var signature = new SignatureDecoder(this, assembly).Type(constraint.Type);
                    // C# encodes the struct constraint as both flags and a ValueType row.
                    if (!value || signature is not TypeSignature.Named named || named.Definition != ValueBase())
                        throw new UnsupportedBindingException("Nominal base/interface or dependent generic constraints require later constraint lowering.");
                }
            }
            Arguments(arguments);
        }
        pending.Enqueue(owner);
        if (method is not null)
            Parameters(owner.Definition.Assembly, owner.Definition.Assembly.Image.Reader.GetMethodDefinition(method.Handle).GetGenericParameters(), method.MethodArguments);
        while (pending.TryDequeue(out var type))
        {
            if (!seen.Add(type)) continue;
            if (seen.Count > 256) throw Failure(DiagnosticIds.TranslationLimit, "Closed argument constraints exceed 256 declarations.");
            CheckOwner(type.Definition.Assembly);
            RejectByRefLike(type.Definition);
            var declaration = type.Definition.Assembly.Image.Reader.GetTypeDefinition(type.Definition.Handle);
            Parameters(type.Definition.Assembly, declaration.GetGenericParameters(), type.Arguments,
                interfaceOwner: (declaration.Attributes & TypeAttributes.Interface) != 0);
        }
        return work;
    }
}
