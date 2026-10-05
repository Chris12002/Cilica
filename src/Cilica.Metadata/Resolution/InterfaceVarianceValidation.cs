using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    // Validate declaration positions before a compiler can use out/in flags as
    // assignment rules. Nested interface and selected-runtime delegate signatures
    // are checked by the same bounded walk.
    private int ValidateTypeVariance(ResolvedType root, int maximumWork, ResolvedAssembly runtimeLibrary)
    {
        int work = 0;
        var flags = new Dictionary<ResolvedType, GenericParameterAttributes[]>();
        var pending = new Queue<ResolvedType>();
        void Member()
        {
            if (++work > maximumWork) throw Failure(DiagnosticIds.TranslationLimit, "Type variance validation exceeds the metadata-member limit.");
        }
        GenericParameterAttributes[] Parameters(ResolvedType type)
        {
            if (flags.TryGetValue(type, out var cached)) return cached;
            if (flags.Count >= 256) throw Failure(DiagnosticIds.TranslationLimit, "Type variance validation exceeds 256 declarations.");
            Member(); var reader = type.Assembly.Image.Reader; var definition = reader.GetTypeDefinition(type.Handle);
            var parameters = definition.GetGenericParameters();
            if (parameters.Count > 4096) throw Failure(DiagnosticIds.TranslationLimit, "Type variance declaration exceeds 4096 parameters.");
            var result = new GenericParameterAttributes[parameters.Count]; var seen = new HashSet<int>();
            foreach (var handle in parameters)
            {
                Member(); var parameter = reader.GetGenericParameter(handle);
                if (parameter.Index >= result.Length || !seen.Add(parameter.Index)) throw new BadImageFormatException("Invalid variance parameter indices.");
                var variance = parameter.Attributes & GenericParameterAttributes.VarianceMask;
                if (variance == GenericParameterAttributes.VarianceMask) throw new BadImageFormatException("Conflicting variance directions.");
                result[parameter.Index] = variance;
            }
            if (result.Any(v => v != GenericParameterAttributes.None))
            {
                if ((definition.Attributes & TypeAttributes.Interface) == 0 && !IsRuntimeDelegate(type, runtimeLibrary, Member))
                    throw new UnsupportedBindingException("Variant signatures require interfaces or delegates derived from the selected runtime base.");
                pending.Enqueue(type);
            }
            flags.Add(type, result); return result;
        }
        _ = Parameters(root);
        while (pending.TryDequeue(out var type))
        {
            var owner = flags[type]; var reader = type.Assembly.Image.Reader; var definition = reader.GetTypeDefinition(type.Handle);
            void Walk(TypeSignature signature, int position, int depth = 0)
            {
                Member();
                if (depth > 64) throw Failure(DiagnosticIds.TranslationLimit, "Type variance signature depth exceeded.");
                switch (signature)
                {
                    case TypeSignature.Variable { IsMethod: false } variable:
                        if (variable.Index >= owner.Length) throw new BadImageFormatException("Variant type parameter is outside its declaration.");
                        if (owner[variable.Index] == GenericParameterAttributes.Covariant && position != 1
                            || owner[variable.Index] == GenericParameterAttributes.Contravariant && position != -1)
                            throw new UnsupportedBindingException("Variant type parameter appears in an invalid or unsupported variance position.");
                        break;
                    case TypeSignature.Generic generic:
                        var nested = Parameters(generic.Definition.Definition);
                        if (nested.Length != generic.Arguments.Count) throw new BadImageFormatException("Generic variance signature has mismatched arity.");
                        for (int index = 0; index < nested.Length; ++index)
                            Walk(generic.Arguments[index], position * (nested[index] == GenericParameterAttributes.Covariant ? 1
                                : nested[index] == GenericParameterAttributes.Contravariant ? -1 : 0), depth + 1);
                        break;
                    case TypeSignature.Vector vector: Walk(vector.Element, position, depth + 1); break;
                    case TypeSignature.Array array: Walk(array.Element, position, depth + 1); break;
                    case TypeSignature.ByReference reference: Walk(reference.Element, 0, depth + 1); break;
                    case TypeSignature.Pointer pointer: Walk(pointer.Element, 0, depth + 1); break;
                    case TypeSignature.Pinned pinned: Walk(pinned.Element, 0, depth + 1); break;
                    case TypeSignature.Modified modified: Walk(modified.Element, position, depth + 1); break;
                    case TypeSignature.FunctionPointer pointer:
                        Walk(pointer.Signature.ReturnType, 0, depth + 1);
                        foreach (var parameter in pointer.Signature.Parameters) Walk(parameter, 0, depth + 1);
                        break;
                }
            }
            foreach (var handle in definition.GetInterfaceImplementations())
            {
                Member(); Walk(new SignatureDecoder(this, type.Assembly).Type(reader.GetInterfaceImplementation(handle).Interface), 1);
            }
            foreach (var handle in definition.GetMethods())
            {
                Member(); var method = reader.GetMethodDefinition(handle);
                // Delegate variance describes Invoke. Legacy asynchronous helper
                // declarations are not part of the admitted callable contract.
                if ((definition.Attributes & TypeAttributes.Interface) == 0 && !reader.StringComparer.Equals(method.Name, "Invoke")) continue;
                var signature = MethodDefinitionSignature(type.Assembly, handle);
                Walk(signature.ReturnType, 1);
                foreach (var parameter in signature.Parameters) Walk(parameter, -1);
                foreach (var parameterHandle in method.GetGenericParameters())
                {
                    Member();
                    foreach (var constraintHandle in reader.GetGenericParameter(parameterHandle).GetConstraints())
                    {
                        Member(); Walk(new SignatureDecoder(this, type.Assembly).Type(reader.GetGenericParameterConstraint(constraintHandle).Type), 0);
                    }
                }
            }
        }
        return work;
    }
}
