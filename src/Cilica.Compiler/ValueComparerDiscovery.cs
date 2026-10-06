using System.Reflection;
using System.Reflection.Metadata;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        // Exact private grants select managed factory/CompareTo bodies. No native
        // comparison primitive, reflection fallback or application substitution.
        private void ComparerOperation(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return;
            var helper = resolver.ResolveType(library, "Cilica.Runtime", "ComparerOperations`1").Value;
            if (helper is null || target.DeclaringType.Definition != helper) return;
            int offset = instruction.Offset;
            if (work.Assembly != library || instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0)
                Fail(work, "Default comparer operations are private to the selected core implementation.", offset);
            if (_metadata.Remaining == 0) Fail(work, "Comparer metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Comparer metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            bool create = input.DeclaredName == "Create", compare = input.DeclaredName == "Compare";
            var variable = new TypeSignature.Variable(false, 0);
            var comparer = Require(resolver.ResolveType(library, "System.Collections.Generic", "Comparer`1"), work, offset);
            TypeSignature result = create ? new TypeSignature.Generic(new(comparer, SignatureTypeKind.Class), new([variable]))
                : new TypeSignature.Primitive(PrimitiveTypeCode.Int32);
            if (!(create || compare) || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.ReturnType != result || input.Signature.Parameters.Count != (create ? 0 : 2)
                || compare && (input.Signature.Parameters[0] != new TypeSignature.ByReference(variable) || input.Signature.Parameters[1] != variable))
                Fail(work, "Invalid selected default comparer declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle,
                create ? LibraryIntrinsicCapability.IntegerComparerCreate : LibraryIntrinsicCapability.IntegerComparerCompare), work, offset);
            var argument = target.DeclaringType.Arguments[0]; CheckArgumentAccess(argument, work, offset);
            if (argument is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 })
                Fail(work, "Default comparer requires exact Int32, UInt32, Int64 or UInt64; culture, nullable, enum and other comparers require later lowering.", offset);
            if (create)
            {
                var factory = Require(resolver.ResolveType(library, "Cilica.Runtime", "IntegerComparerFactory`1"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(factory, new([argument])), "Create", 0, _metadata), work, offset);
            }
            else
            {
                var primitive = Require(resolver.ResolveRuntimeType(library, ((TypeSignature.Primitive)argument).Code), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(primitive, SignatureList<TypeSignature>.Empty), "CompareTo", 1, _metadata), work, offset);
            }
        }
    }
}
