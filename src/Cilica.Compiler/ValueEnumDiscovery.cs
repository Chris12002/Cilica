using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<ResolvedType, bool> _enumScalars = new();
        private bool EnumScalar(TypeSignature signature, Work? caller, int offset)
        {
            if (!integers64 || signature is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named) return false;
            if (_enumScalars.TryGetValue(named.Definition, out bool known)) return known;
            try
            {
                bool scalar = Int32EnumStorage.Matches(signature, resolver.ResolveType(library, "System", "Enum").Value,
                    type => Snapshot(type, caller, offset));
                _enumScalars.Add(named.Definition, scalar); return scalar;
            }
            catch (ArgumentException error) { Fail(caller,error.Message,offset); return false; }
        }
    }
}
