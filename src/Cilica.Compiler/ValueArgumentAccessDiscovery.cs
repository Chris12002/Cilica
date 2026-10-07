using System.Collections.Generic;
using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private void CheckValueDeclarationAccess(ResolvedType definition, Work work, int offset)
        {
            var declaration = TypeAccess(definition, work, offset);
            if (declaration.ExternallyVisible || SourceTypeVisible(work.Binding, ObjectTypeId.From(declaration.Source), declaration.LexicalOwners)
                || OpaqueValueArgument(definition, work, offset)) return;
            Fail(work, "Value type use requires lexical declaring scope or an opaque bound generic argument.", offset);
        }
        private bool OpaqueValueArgument(ResolvedType definition, Work work, int offset)
        {
            if (!integers64) return false;
            // Declaration signatures and operand tokens are checked before substitution.
            // This only permits transport of an already-bound argument; explicit names
            // in foreign generic code still undergo lexical source checks.
            var pending = new Stack<TypeSignature>(work.Resolved.DeclaringType.Arguments.Concat(work.Resolved.MethodArguments));
            int nodes = 0;
            while (pending.TryPop(out var signature))
            {
                if (++nodes > 4096) Fail(work, "Opaque value argument access exceeds the signature-node limit.", offset, DiagnosticIds.TranslationLimit);
                switch (signature)
                {
                    case TypeSignature.Named named when named.Definition == definition: return true;
                    case TypeSignature.Generic generic:
                        if (generic.Definition.Definition == definition) return true;
                        foreach (var argument in generic.Arguments) pending.Push(argument);
                        break;
                    case TypeSignature.Vector vector: pending.Push(vector.Element); break;
                }
            }
            return false;
        }
    }
}
