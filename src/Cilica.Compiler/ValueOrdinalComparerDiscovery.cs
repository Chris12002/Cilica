using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private IrMethodId OrdinalComparerGetter(Work work, int offset)
        {
            var strings = Require(resolver.ResolveType(library, "System", "StringComparer"), work, offset);
            var getter = Require(resolver.ResolveClosedDeclaration(new(strings, SignatureList<TypeSignature>.Empty), "get_Ordinal", 0, _metadata), work, offset);
            if (_metadata.Remaining == 0) Fail(work, "Ordinal comparer metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var source = Require(resolver.ReadMethodDeclarationInput(library, getter.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(source.MetadataMembersRead)) Fail(work, "Ordinal comparer metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (source.IsReferenceAssembly || !source.HasBody || source.HasTypeInitializer || source.HasModuleInitializer
                || source.OwnerGenericArity != 0 || source.Signature.Header != 0 || source.Signature.GenericArity != 0
                || source.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName)
                || source.Implementation != MethodImplAttributes.IL || source.Signature.Parameters.Count != 0
                || source.Signature.ReturnType is not TypeSignature.Named { Kind: SignatureTypeKind.Class } returned || returned.Definition != strings)
                Fail(work, "Ordinal comparison requires the exact selected comparer getter.", offset);
            return IrMethodId.From(source.Source);
        }
    }
}
