using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    // Compare declaration obligations before closing either owner. Equality here
    // grants structural correspondence only; executable argument proof remains
    // the responsibility of the closed-snapshot validator.
    private void CompareLibraryParameters(LibraryBindingPlan plan, ResolvedAssembly source, EntityHandle sourceOwner,
        ResolvedAssembly target, EntityHandle targetOwner, MemberResolutionBudget budget)
    {
        (GenericParameterHandleCollection Parameters, int Types, int Methods) OwnerParameters(ResolvedAssembly assembly, EntityHandle owner)
        {
            InspectMember(budget); var reader = assembly.Image.Reader;
            if (owner.Kind == HandleKind.TypeDefinition)
            {
                var parameters = reader.GetTypeDefinition((TypeDefinitionHandle)owner).GetGenericParameters();
                return (parameters, parameters.Count, 0);
            }
            var method = reader.GetMethodDefinition((MethodDefinitionHandle)owner);
            InspectMember(budget);
            return (method.GetGenericParameters(), reader.GetTypeDefinition(method.GetDeclaringType()).GetGenericParameters().Count,
                method.GetGenericParameters().Count);
        }
        var aOwner = OwnerParameters(source, sourceOwner); var bOwner = OwnerParameters(target, targetOwner);
        if (aOwner.Types != bOwner.Types || aOwner.Methods != bOwner.Methods)
            throw InvalidLibraryBinding("Mapped generic declaration arities differ.");
        var left = aOwner.Parameters.GetEnumerator(); var right = bOwner.Parameters.GetEnumerator();
        int index = 0;
        while (left.MoveNext() && right.MoveNext())
        {
            InspectMember(budget); InspectMember(budget);
            var a = source.Image.Reader.GetGenericParameter(left.Current); var b = target.Image.Reader.GetGenericParameter(right.Current);
            if (a.Index != index || b.Index != index || a.Parent != sourceOwner || b.Parent != targetOwner || a.Attributes != b.Attributes)
                throw InvalidLibraryBinding("Mapped generic parameter indices, owners or attributes differ.");
            var first = Constraints(source, left.Current, a, aOwner.Types, aOwner.Methods);
            var second = Constraints(target, right.Current, b, bOwner.Types, bOwner.Methods);
            if (!first.SetEquals(second))
                throw InvalidLibraryBinding("Mapped generic constraint sets differ after explicit type projection.");
            index++;
        }

        HashSet<TypeSignature> Constraints(ResolvedAssembly assembly, GenericParameterHandle owner, GenericParameter parameter, int types, int methods)
        {
            var result = new HashSet<TypeSignature>();
            foreach (var handle in parameter.GetConstraints())
            {
                InspectMember(budget); var row = assembly.Image.Reader.GetGenericParameterConstraint(handle);
                if (row.Parameter != owner) throw InvalidLibraryBinding("Generic constraint has an unexpected parameter owner.");
                var signature = new SignatureDecoder(this, assembly).Type(row.Type);
                Validate(signature, types, methods, constraint: true);
                if (!result.Add(ProjectLibrarySignature(plan, signature, budget)))
                    throw InvalidLibraryBinding("Duplicate generic constraint after explicit type projection.");
            }
            return result;
        }
        void Validate(TypeSignature signature, int types, int methods, bool constraint = false)
        {
            InspectMember(budget);
            switch (signature)
            {
                case TypeSignature.Variable variable:
                    if (variable.Index < 0 || variable.Index >= (variable.IsMethod ? methods : types))
                        throw InvalidLibraryBinding("Generic constraint variable exceeds its declaration arity.");
                    break;
                case TypeSignature.Named named:
                    InspectMember(budget);
                    if (named.Definition.Assembly.Image.Reader.GetTypeDefinition(named.Definition.Handle).GetGenericParameters().Count != 0)
                        throw InvalidLibraryBinding("A generic constraint cannot use an uninstantiated generic definition.");
                    break;
                case TypeSignature.Generic generic:
                    InspectMember(budget);
                    if (generic.Arguments.Count == 0 || generic.Arguments.Count != generic.Definition.Definition.Assembly.Image.Reader
                        .GetTypeDefinition(generic.Definition.Definition.Handle).GetGenericParameters().Count)
                        throw InvalidLibraryBinding("Generic constraint type has mismatched argument arity.");
                    foreach (var argument in generic.Arguments) Validate(argument, types, methods);
                    break;
                case TypeSignature.Modified modified:
                    Validate(modified.Modifier, types, methods); Validate(modified.Element, types, methods, constraint);
                    break;
                case TypeSignature.Vector vector when !constraint: Validate(vector.Element, types, methods); break;
                case TypeSignature.Primitive { Code: not (PrimitiveTypeCode.Void or PrimitiveTypeCode.TypedReference) } when !constraint: break;
                default: throw InvalidLibraryBinding("Generic constraint contains an unsupported structural signature.");
            }
        }
    }
}
