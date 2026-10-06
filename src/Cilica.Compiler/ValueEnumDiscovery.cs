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
                var enumBase = resolver.ResolveType(library, "System", "Enum").Value;
                if (enumBase is null || TypeBase(named.Definition, caller, offset).BaseType is not TypeSignature.Named parent
                    || parent.Definition != enumBase) return false;
                bool scalar = Int32EnumStorage.Matches(signature, enumBase, type => Snapshot(type, caller, offset));
                // Negative answers use the bounded base-fact cache. Positive answers
                // require full snapshots and remain bounded by that unchanged limit.
                if (scalar) _enumScalars.Add(named.Definition, true);
                return scalar;
            }
            catch (ArgumentException error) { Fail(caller,error.Message,offset); return false; }
        }
    }
}
