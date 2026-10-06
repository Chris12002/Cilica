using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Formatting;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed record MethodDefinitionFlags(MethodAttributes Attributes);

public sealed partial class AssemblyResolver
{
    /// <summary>Reads one owned MethodDef flags row without reading its signature or body.</summary>
    public ResolutionResult<MethodDefinitionFlags> ReadMethodFlags(ResolvedAssembly assembly,
        MethodDefinitionHandle handle, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        return Bind(assembly, handle, () =>
        {
            InspectMember(budget);
            return new MethodDefinitionFlags(assembly.Image.Reader.GetMethodDefinition(handle).Attributes);
        });
    }

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
                method.RelativeVirtualAddress != 0, reader.GetString(method.Name), work) { LexicalOwners = header.Lexical, OwnerAssemblyVisible = header.AssemblyVisible };
        });
    }

    /// <summary>Authenticates a closed declaration and constraints without reading its IL body.</summary>
    public ResolutionResult<MethodDeclarationInput> ReadClosedMethodDeclarationInput(ResolvedMethod method,
        ResolvedAssembly runtimeLibrary, TypeReadLimits limits)
    {
        ArgumentNullException.ThrowIfNull(method); CheckOwner(runtimeLibrary);
        var assembly = method.DeclaringType.Definition.Assembly;
        var read = ReadMethodDeclarationInput(assembly, method.Handle, limits);
        if (read.Value is not { } input) return read;
        return Bind(assembly, method.Handle, () =>
        {
            if (input.OwnerToken != MetadataTokens.GetToken(method.DeclaringType.Definition.Handle)
                || input.OwnerGenericArity != method.DeclaringType.Arguments.Count
                || input.Signature.GenericArity != method.MethodArguments.Count)
                throw new BadImageFormatException("Closed declaration owner or argument count contradicts its metadata.");
            string key;
            try { key = ClosedGenericKey.Create(method.DeclaringType.Arguments, method.MethodArguments); }
            catch (ArgumentException error) { throw new UnsupportedBindingException(error.Message); }
            int work = ValidateClosedConstraints(method.DeclaringType, method, runtimeLibrary,
                limits.MaximumMembers - input.MetadataMembersRead);
            var signature = Substitute(input.Signature, method.DeclaringType.Arguments, method.MethodArguments);
            if (signature != method.Signature)
                throw new BadImageFormatException("Closed declaration signature contradicts its metadata.");
            return input with { Source = input.Source with { Specialization = key }, Signature = signature,
                MetadataMembersRead = checked(input.MetadataMembersRead + work) };
        });
    }

    // Share provenance/owner inspection with body input while retaining its existing IL/initializer fault order.
    private (MethodSource Source, MethodSignature Signature, int Arity, bool Visible, bool AssemblyVisible, ImmutableArray<LexicalTypeOwner> Lexical) ReadMethodHeader(
        ResolvedAssembly assembly, MethodDefinitionHandle handle, int maximumOwnerDepth, Action? inspectOwner = null)
    {
        var reader = assembly.Image.Reader; var method = reader.GetMethodDefinition(handle);
        var signature = MethodDefinitionSignature(assembly, handle);
        bool visible = true, assemblyVisible = true; int arity = 0;
        var lexical = ImmutableArray.CreateBuilder<LexicalTypeOwner>();
        var owners = new HashSet<TypeDefinitionHandle>();
        for (var current = method.GetDeclaringType(); !current.IsNil;)
        {
            if (!owners.Add(current)) throw Failure(DiagnosticIds.InvalidSignature, "Declaring-type chain is cyclic.");
            if (owners.Count > maximumOwnerDepth) throw Failure(DiagnosticIds.TranslationLimit, "Declaring-type depth limit exceeded.");
            inspectOwner?.Invoke();
            var definition = reader.GetTypeDefinition(current);
            lexical.Add(new(MetadataTokens.GetToken(current), definition.Attributes & TypeAttributes.VisibilityMask, definition.GetGenericParameters().Count));
            // Nested declarations repeat inherited parameters; do not count them twice.
            // The maximum also retains the gate for under-declared nested owners.
            arity = Math.Max(arity, definition.GetGenericParameters().Count);
            var parent = definition.GetDeclaringType();
            visible &= (definition.Attributes & TypeAttributes.VisibilityMask) == (parent.IsNil ? TypeAttributes.Public : TypeAttributes.NestedPublic);
            var visibility = definition.Attributes & TypeAttributes.VisibilityMask;
            assemblyVisible &= parent.IsNil ? visibility is TypeAttributes.Public or TypeAttributes.NotPublic
                : visibility is TypeAttributes.NestedPublic or TypeAttributes.NestedAssembly or TypeAttributes.NestedFamORAssem;
            current = parent;
        }
        var source = new MethodSource(assembly.Identity.DisplayName, assembly.ContentHash, MetadataTokens.GetToken(handle),
            new NameFormatter(reader).TypeDefinitionName(method.GetDeclaringType()) + "::" + reader.GetString(method.Name));
        return (source, signature, arity, visible, assemblyVisible, lexical.ToImmutable());
    }
}
