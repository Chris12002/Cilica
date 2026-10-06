using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

/// <summary>Original operand type syntax in the caller's image, before generic substitution.</summary>
public sealed record OperandTypeUses(ImmutableArray<TypeSignature> Signatures);

public sealed partial class AssemblyResolver
{
    /// <summary>
    /// Snapshots explicit type uses without replacing generic variables. This is
    /// provenance for an access policy, not permission to execute an operand.
    /// Closed binding separately validates member identity, signatures and constraints.
    /// </summary>
    public ResolutionResult<OperandTypeUses> ReadOperandTypeUses(ResolvedMethod caller, EntityHandle operand,
        MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(caller); ArgumentNullException.ThrowIfNull(budget);
        var assembly = caller.DeclaringType.Definition.Assembly;
        CheckOwner(assembly);
        return Bind(assembly, operand, () =>
        {
            var reader = assembly.Image.Reader;
            var decoder = new SignatureDecoder(this, assembly);
            var types = ImmutableArray.CreateBuilder<TypeSignature>();
            void MethodTypes(MethodSignature signature)
            {
                types.Add(signature.ReturnType); types.AddRange(signature.Parameters);
            }
            // A MethodSpec may wrap exactly one MethodDef or MemberRef, never a
            // second MethodSpec. All rows consume the caller's shared work budget.
            InspectMember(budget);
            if (operand.Kind == HandleKind.MethodSpecification)
            {
                var specification = reader.GetMethodSpecification((MethodSpecificationHandle)operand);
                types.AddRange(decoder.MethodArguments(specification.Signature));
                operand = specification.Method;
                if (operand.Kind is not (HandleKind.MethodDefinition or HandleKind.MemberReference))
                    throw new BadImageFormatException("Method specification needs a MethodDef or MemberRef declaration.");
                InspectMember(budget);
            }
            switch (operand.Kind)
            {
                case HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification:
                    types.Add(decoder.Type(operand)); break;
                case HandleKind.MethodDefinition:
                    var method = reader.GetMethodDefinition((MethodDefinitionHandle)operand);
                    types.Add(decoder.Type(method.GetDeclaringType()));
                    MethodTypes(decoder.Method(method.Signature)); break;
                case HandleKind.FieldDefinition:
                    var field = reader.GetFieldDefinition((FieldDefinitionHandle)operand);
                    types.Add(decoder.Type(field.GetDeclaringType()));
                    types.Add(decoder.Field(field.Signature)); break;
                case HandleKind.MemberReference:
                    var member = reader.GetMemberReference((MemberReferenceHandle)operand);
                    if (member.Parent.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
                        throw new UnsupportedBindingException("Operand type-use snapshots require a nominal member owner.");
                    types.Add(decoder.Type(member.Parent));
                    if (member.GetKind() == MemberReferenceKind.Field) types.Add(decoder.Field(member.Signature));
                    else MethodTypes(decoder.Method(member.Signature));
                    break;
                default:
                    throw new UnsupportedBindingException("Operand type-use snapshots require a type, field or method token.");
            }
            return new OperandTypeUses(types.ToImmutable());
        });
    }
}
