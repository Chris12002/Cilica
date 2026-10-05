using System.Reflection;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private bool EmptyStringField(Work work, MethodInstruction instruction, ResolvedField field, TypeInput owner, FieldInput declaration)
        {
            var selected = resolver.ResolveRuntimeType(library, System.Reflection.Metadata.PrimitiveTypeCode.String);
            if (selected.Value != field.DeclaringType.Definition) return false;
            int offset = instruction.Offset;
            if (instruction.OpCode != "ldsfld" || declaration.Name != "Empty"
                || declaration.Attributes != (FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly)
                || declaration.Signature is not TypeSignature.Primitive { Code: System.Reflection.Metadata.PrimitiveTypeCode.String }
                || declaration.HasExplicitStorage || field.DeclaringType.Arguments.Count != 0
                || owner.OwnerGenericArity != 0 || owner.HasTypeInitializer || owner.HasModuleInitializer || owner.IsReferenceAssembly)
                Fail(work, "Selected String static fields require an exact readonly Empty read without explicit storage or initialization.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(field.DeclaringType.Definition.Assembly, field.Handle,
                LibraryIntrinsicCapability.StringEmpty), work, offset);
            _ = IncludeString(work, offset);
            return true;
        }
    }
}
