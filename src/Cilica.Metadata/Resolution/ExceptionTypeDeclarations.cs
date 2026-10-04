using System;
using System.Reflection;

namespace Cilica.Metadata.Resolution;

public sealed record ExceptionTypeDeclarations(bool HasVirtualMembers);

public sealed partial class AssemblyResolver
{
    /// <summary>Bounds custom exception member inspection before using intrinsic Message semantics.</summary>
    public ResolutionResult<ExceptionTypeDeclarations> ReadExceptionTypeDeclarations(ResolvedType type, MemberResolutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(budget); CheckOwner(type.Assembly);
        return Bind(type.Assembly, type.Handle, () =>
        {
            var reader = type.Assembly.Image.Reader; var definition = reader.GetTypeDefinition(type.Handle);
            bool virtualMembers = definition.GetMethodImplementations().Count != 0;
            foreach (var handle in definition.GetMethods())
            {
                if (!budget.TryConsume(1)) throw Failure(DiagnosticIds.TranslationLimit, "Exception member metadata budget exhausted.");
                virtualMembers |= (reader.GetMethodDefinition(handle).Attributes & MethodAttributes.Virtual) != 0;
            }
            return new ExceptionTypeDeclarations(virtualMembers);
        });
    }
}
