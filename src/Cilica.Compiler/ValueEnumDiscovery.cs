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
        private TypeInput ReadEnumInput(ResolvedType type, Work? caller, int offset)
        {
            if (_snapshots.TryGetValue(ObjectModelBuilder.Id(type), out var input)) return input;
            if (_metadata.Remaining == 0)
                Fail(caller, "Enum classification metadata-member limit exceeded.", offset, Cilica.Metadata.DiagnosticIds.TranslationLimit);
            input = Require(resolver.ReadTypeInput(type.Assembly, type.Handle,
                new(MaximumFields: ObjectModelLimits.MaximumFields - _snapshotFields, MaximumMembers: _metadata.Remaining)), caller, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead))
                Fail(caller, "Enum classification metadata-member limit exceeded.", offset, Cilica.Metadata.DiagnosticIds.TranslationLimit);
            _snapshotFields += input.Fields.Length;
            return input;
        }
        private bool EnumScalar(TypeSignature signature, Work? caller, int offset)
        {
            if (!integers64 || signature is not TypeSignature.Named { Kind: SignatureTypeKind.ValueType or SignatureTypeKind.Unknown } named) return false;
            if (_enumScalars.TryGetValue(named.Definition, out bool known)) return known;
            try
            {
                var enumBase = resolver.ResolveType(library, "System", "Enum").Value;
                if (enumBase is null || TypeBase(named.Definition, caller, offset).BaseType is not TypeSignature.Named parent
                    || parent.Definition != enumBase) return false;
                bool scalar = Int32EnumStorage.Matches(signature, enumBase, type => ReadEnumInput(type, caller, offset));
                // Scalar aliases need validated facts, not retained layouts. Cache a
                // bounded number; later declarations are rechecked against the work ledger.
                if (scalar && _enumScalars.Count < ObjectModelLimits.MaximumTypes)
                {
                    _enumScalars.Add(named.Definition, true);
                    _knownTypes.TryAdd(ObjectModelBuilder.Id(named.Definition), new(named.Definition, SignatureList<TypeSignature>.Empty));
                }
                return scalar;
            }
            catch (ArgumentException error) { Fail(caller,error.Message,offset); return false; }
        }
    }
}
