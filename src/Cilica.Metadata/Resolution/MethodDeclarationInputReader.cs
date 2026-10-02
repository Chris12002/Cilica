using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Formatting;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>Snapshots a method signature/owner, including bodyless declarations; never reads or executes IL.</summary>
    public ResolutionResult<MethodDeclarationInput> ReadMethodDeclarationInput(ResolvedAssembly assembly,
        MethodDefinitionHandle handle, TypeReadLimits? limits = null)
    {
        CheckOwner(assembly); limits ??= new();
        if (limits.MaximumMembers <= 0 || limits.MaximumOwnerDepth is <= 0 or > TypeReadLimits.MaximumSupportedOwnerDepth)
            throw new ArgumentOutOfRangeException(nameof(limits), "Declaration input needs positive member work and owner depth 1..64.");
        return Bind<MethodDeclarationInput>(assembly, handle, () =>
        {
            var reader = assembly.Image.Reader; var method = reader.GetMethodDefinition(handle);
            int work = 0;
            void Member()
            {
                if (++work > limits.MaximumMembers) throw Failure(DiagnosticIds.TranslationLimit, "Declaration input exceeds the metadata-member limit.");
            }
            Member();
            var header = ReadMethodHeader(assembly, handle, limits.MaximumOwnerDepth, Member);
            bool Initializer(TypeDefinition definition)
            {
                Member();
                foreach (var candidate in definition.GetMethods())
                {
                    Member();
                    if (reader.StringComparer.Equals(reader.GetMethodDefinition(candidate).Name, ".cctor")) return true;
                }
                return false;
            }
            var owner = reader.GetTypeDefinition(method.GetDeclaringType());
            return new(header.Source, header.Signature, method.Attributes, method.ImplAttributes, header.Arity,
                assembly.IsReferenceAssembly, Initializer(owner), Initializer(reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1))),
                MetadataTokens.GetToken(method.GetDeclaringType()), owner.Attributes, header.Visible,
                method.RelativeVirtualAddress != 0, reader.GetString(method.Name), work);
        });
    }

    // Share provenance/owner inspection with body input while retaining its existing IL/initializer fault order.
    private (MethodSource Source, MethodSignature Signature, int Arity, bool Visible) ReadMethodHeader(
        ResolvedAssembly assembly, MethodDefinitionHandle handle, int maximumOwnerDepth, Action? inspectOwner = null)
    {
        var reader = assembly.Image.Reader; var method = reader.GetMethodDefinition(handle);
        var signature = MethodDefinitionSignature(assembly, handle);
        bool visible = true; int arity = 0;
        var owners = new HashSet<TypeDefinitionHandle>();
        for (var current = method.GetDeclaringType(); !current.IsNil;)
        {
            if (!owners.Add(current)) throw Failure(DiagnosticIds.InvalidSignature, "Declaring-type chain is cyclic.");
            if (owners.Count > maximumOwnerDepth) throw Failure(DiagnosticIds.TranslationLimit, "Declaring-type depth limit exceeded.");
            inspectOwner?.Invoke();
            var definition = reader.GetTypeDefinition(current); arity = checked(arity + definition.GetGenericParameters().Count);
            var parent = definition.GetDeclaringType();
            visible &= (definition.Attributes & TypeAttributes.VisibilityMask) == (parent.IsNil ? TypeAttributes.Public : TypeAttributes.NestedPublic);
            current = parent;
        }
        var source = new MethodSource(assembly.Identity.DisplayName, assembly.ContentHash, MetadataTokens.GetToken(handle),
            new NameFormatter(reader).TypeDefinitionName(method.GetDeclaringType()) + "::" + reader.GetString(method.Name));
        return (source, signature, arity, visible);
    }
}
