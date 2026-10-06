using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private bool _nullableRead;
        private ResolvedType? _nullable;

        private bool IsNullable(TypeSignature signature, Work work, int offset)
        {
            if (signature is not TypeSignature.Generic type) return false;
            if (!_nullableRead)
            {
                if (!_metadata.TryConsume(1)) Fail(work, "Nullable identity lookup exceeds the metadata budget.", offset, DiagnosticIds.TranslationLimit);
                var result = resolver.ResolveType(library, "System", "Nullable`1");
                if (result.Value is null && result.Diagnostics.Any(d => d.Id != DiagnosticIds.TypeNotFound))
                    _ = Require(result, work, offset);
                _nullable = result.Value;
                _nullableRead = true;
            }
            return type.Definition.Definition == _nullable;
        }
    }
}
