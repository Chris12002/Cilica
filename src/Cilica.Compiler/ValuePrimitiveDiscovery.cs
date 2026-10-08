using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private PrimitiveReceiver? Primitive(ResolvedType owner, Work? caller, int offset)
        {
            if (owner.Assembly != library) return null;
            foreach (var code in new[] { PrimitiveTypeCode.Char, PrimitiveTypeCode.Int32, PrimitiveTypeCode.UInt32, PrimitiveTypeCode.Int64, PrimitiveTypeCode.UInt64 })
            {
                if (owner != Require(resolver.ResolveRuntimeType(library, code), caller, offset)) continue;
                var input = Snapshot(owner, caller, offset);
                var valueBase = Require(resolver.ResolveType(library, "System", "ValueType"), caller, offset);
                if (input.IsReferenceAssembly || input.IsByRefLike || input.PackingSize != 0 || input.ClassSize is not (0 or 1) || input.OwnerGenericArity != 0 || !input.ExternallyVisible
                    || (input.Attributes & TypeAttributes.LayoutMask) != TypeAttributes.SequentialLayout
                    || input.HasTypeInitializer || input.HasModuleInitializer
                    || input.BaseType is not TypeSignature.Named parent || parent.Definition != valueBase
                    || (input.Attributes & (TypeAttributes.VisibilityMask | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Interface))
                        != (TypeAttributes.Public | TypeAttributes.Sealed))
                    Fail(caller, "Primitive instance bodies require the exact selected public sealed scalar value without initialization.", offset);
                return new(input.Source, code);
            }
            return null;
        }
    }
}
