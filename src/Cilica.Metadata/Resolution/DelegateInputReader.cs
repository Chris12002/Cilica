using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Resolution;

public sealed record DelegateDeclarationInput(ImmutableArray<MethodDeclarationInput> Methods);

public sealed partial class AssemblyResolver
{
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
