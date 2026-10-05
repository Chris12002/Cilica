using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Metadata.IL;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private ValueIntrinsicCall? CultureCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var candidate = resolver.ResolveType(library, "System.Globalization", "CultureInfo").Value;
            if (candidate is null || candidate != target.DeclaringType.Definition) return null;
            int offset = instruction.Offset;
            var input = ExceptionDeclaration(target, work, offset);
            if (instruction.OpCode != "call" || input.DeclaredName != "get_InvariantCulture"
                || target.DeclaringType.Arguments.Count != 0 || input.Signature.Header != 0 || input.Signature.Parameters.Count != 0
                || input.Signature.ReturnType is not TypeSignature.Named { Kind: SignatureTypeKind.Class } returned || returned.Definition != candidate
                || (input.Attributes & (MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.Virtual))
                    != (MethodAttributes.Static | MethodAttributes.SpecialName))
                Fail(work, "Only the selected CultureInfo.InvariantCulture identity is implemented; culture construction, mutation and other members require later lowering.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, candidate.Handle, LibraryIntrinsicCapability.CultureInfoLayout), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.InvariantCulture), work, offset);
            var snapshot = Snapshot(candidate, work, offset);
            var objectBase = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset);
            if (snapshot.OwnerGenericArity != 0 || !snapshot.ExternallyVisible || snapshot.BaseType is not TypeSignature.Named parent
                || parent.Definition != objectBase || (snapshot.Attributes & (TypeAttributes.Abstract | TypeAttributes.Interface)) != 0)
                Fail(work, "Invariant culture requires the selected public nongeneric concrete CultureInfo with direct Object ancestry.", offset);
            var id = ObjectModelBuilder.Id(candidate);
            // The intrinsic descriptor carries invariant-provider identity, not a
            // platform CultureInfo's private mutable fields or static initializer.
            _snapshots[id] = snapshot with { Fields = [], PackingSize = 0, ClassSize = 0,
                Attributes = TypeAttributes.Public | TypeAttributes.BeforeFieldInit,
                HasTypeInitializer = false, HasModuleInitializer = false };
            IncludeReference(candidate, work, offset);
            return new(ValueIntrinsic.InvariantCulture, id, 0);
        }
    }
}
