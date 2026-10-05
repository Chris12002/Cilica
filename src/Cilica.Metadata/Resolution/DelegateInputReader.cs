using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed record DelegateDeclarationInput(ImmutableArray<MethodDeclarationInput> Methods);

public sealed partial class AssemblyResolver
{
    // A name is only a candidate filter. Variance permission requires the exact
    // selected runtime base, including its image identity.
    private bool IsRuntimeDelegate(ResolvedType type, ResolvedAssembly library, Action member)
    {
        member();
        var baseHandle = type.Assembly.Image.Reader.GetTypeDefinition(type.Handle).BaseType;
        if (baseHandle.IsNil || new SignatureDecoder(this, type.Assembly).Type(baseHandle) is not TypeSignature.Named parent)
            return false;
        member();
        var reader = parent.Definition.Assembly.Image.Reader;
        var definition = reader.GetTypeDefinition(parent.Definition.Handle);
        if (!reader.StringComparer.Equals(definition.Namespace, "System") || !reader.StringComparer.Equals(definition.Name, "MulticastDelegate"))
            return false;
        member();
        var selected = ResolveType(library, "System", "MulticastDelegate");
        if (!selected.Success) throw new BindingFailureException(selected.Diagnostics);
        return parent.Definition == selected.Value;
    }

    /// <summary>Close constructor/Invoke signatures; executable constraints are checked by ReadClosedTypeInput.</summary>
    public ResolutionResult<DelegateDeclarationInput> ReadDelegateDeclarations(ConstructedType type, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(type);
        var input = ReadDelegateDeclarations(type.Definition, budget);
        if (input.Value is not { } declarations) return input;
        return Bind(type.Definition.Assembly, type.Definition.Handle, () =>
        {
            if (!budget.TryConsume(1))
                throw Failure(DiagnosticIds.TranslationLimit, "Closed delegate declaration metadata limit exceeded.");
            if (type.Arguments.Count != type.Definition.Assembly.Image.Reader.GetTypeDefinition(type.Definition.Handle).GetGenericParameters().Count)
                throw new BadImageFormatException("Closed delegate argument count does not match its declaration.");
            string key;
            try { key = ClosedGenericKey.Create(type.Arguments, SignatureList<TypeSignature>.Empty); }
            catch (ArgumentException error) { throw new UnsupportedBindingException(error.Message); }
            return new DelegateDeclarationInput([.. declarations.Methods.Select(method => method with {
                Source = method.Source with { Specialization = key },
                Signature = Substitute(method.Signature, type.Arguments, SignatureList<TypeSignature>.Empty) })]);
        });
    }

    /// <summary>Reads only constructor/Invoke declarations within a caller-owned metadata budget; never reads IL.</summary>
    public ResolutionResult<DelegateDeclarationInput> ReadDelegateDeclarations(ResolvedType type, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(budget);
        CheckOwner(type.Assembly);
        return Bind(type.Assembly, type.Handle, () =>
        {
            var reader = type.Assembly.Image.Reader;
            var declarations = ImmutableArray.CreateBuilder<MethodDeclarationInput>();
            foreach (var handle in reader.GetTypeDefinition(type.Handle).GetMethods())
            {
                if (!budget.TryConsume(1) || budget.Remaining == 0)
                    throw Failure(DiagnosticIds.TranslationLimit, "Delegate declaration metadata limit exceeded.");
                var row = reader.GetMethodDefinition(handle);
                if (!reader.StringComparer.Equals(row.Name, ".ctor") && !reader.StringComparer.Equals(row.Name, "Invoke")) continue;
                var result = ReadMethodDeclarationInput(type.Assembly, handle, new(MaximumMembers: budget.Remaining));
                if (result.Value is not { } declaration) throw new BindingFailureException(result.Diagnostics);
                if (!budget.TryConsume(declaration.MetadataMembersRead))
                    throw Failure(DiagnosticIds.TranslationLimit, "Delegate declaration metadata limit exceeded.");
                declarations.Add(declaration);
            }
            return new DelegateDeclarationInput(declarations.ToImmutable());
        });
    }
}
