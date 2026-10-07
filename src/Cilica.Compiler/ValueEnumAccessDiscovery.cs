using System.Collections.Generic;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private void CheckEnumDeclarationAccess(ResolvedType definition, Work work, int offset)
        {
            var declaration = TypeAccess(definition, work, offset);
            if (declaration.ExternallyVisible) return;
            if (declaration.LexicalOwners.Length == 1)
            {
                if (declaration.Source.ContentHash != work.Binding.Owner.ContentHash)
                    Fail(work, "Internal Int32 enum operands require a declaration in the calling assembly.", offset);
            }
            else if (!ObjectLexicalAccess.Visible(work.Binding, ObjectTypeId.From(declaration.Source), declaration.LexicalOwners))
                Fail(work, "Nested Int32 enum operands require access from the caller's lexical declaring scope.", offset);
        }
        private bool OpaqueEnumArgument(ResolvedType definition, Work work, int offset)
        {
            if (!integers64) return false;
            // Declaration signatures and operand tokens are checked before substitution.
            // This only permits transport of an already-bound argument; explicit names
            // in foreign generic code still call CheckEnumDeclarationAccess above.
            var pending = new Stack<TypeSignature>(work.Resolved.DeclaringType.Arguments.Concat(work.Resolved.MethodArguments));
            int nodes = 0;
            while (pending.TryPop(out var signature))
            {
                if (++nodes > 4096) Fail(work, "Opaque enum argument access exceeds the signature-node limit.", offset, DiagnosticIds.TranslationLimit);
                switch (signature)
                {
                    case TypeSignature.Named named when named.Definition == definition: return true;
                    case TypeSignature.Generic generic:
                        foreach (var argument in generic.Arguments) pending.Push(argument);
                        break;
                    case TypeSignature.Vector vector: pending.Push(vector.Element); break;
                }
            }
            return false;
        }
    }
}
