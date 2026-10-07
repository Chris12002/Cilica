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
    }
}
