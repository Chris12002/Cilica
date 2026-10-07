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
        private ValueIntrinsicCall? TupleShapeCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "TupleShape`1").Value) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Tuple shape metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Tuple shape metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (work.Assembly != library || instruction.OpCode != "call" || input.DeclaredName != "IsTuple"
                || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.Parameters.Count != 0 || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.Boolean))
                Fail(work, "Tuple shape checks require the exact private owned declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ClosedTupleShape), work, offset);
            var argument = target.DeclaringType.Arguments[0]; CheckArgumentAccess(argument, work, offset);
            _ = Storage(argument, work, offset);
            bool tuple = false;
            if (argument is TypeSignature.Generic generic && generic.Definition.Definition.Assembly == library)
                foreach (int arity in new[] { 1, 2, 5, 8 })
                    if (generic.Arguments.Count == arity && generic.Definition.Definition == resolver.ResolveType(library, "System", "ValueTuple`" + arity).Value) tuple = true;
            // This is a nominal test. A default ValueTuple<...,Int32> still is a
            // tuple, even though invoking its own constructor would reject Rest.
            return new(ValueIntrinsic.ClosedTupleShape, ObjectModelBuilder.Id(Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset)), 0)
                { ConstantValue = tuple ? 1 : 0 };
        }
    }
}
