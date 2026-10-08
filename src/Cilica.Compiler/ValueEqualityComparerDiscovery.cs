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
        private void EqualityComparerOperation(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return;
            var helper = resolver.ResolveType(library, "Cilica.Runtime", "EqualityComparerOperations`1").Value;
            if (helper is null || target.DeclaringType.Definition != helper) return;
            int offset = instruction.Offset;
            if (work.Assembly != library || instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0)
                Fail(work, "Default equality comparer creation is private to the selected core implementation.", offset);
            if (_metadata.Remaining == 0) Fail(work, "Equality comparer metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Equality comparer metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            var comparer = Require(resolver.ResolveType(library, "System.Collections.Generic", "EqualityComparer`1"), work, offset);
            var result = new TypeSignature.Generic(new(comparer, SignatureTypeKind.Class), new([new TypeSignature.Variable(false, 0)]));
            if (input.DeclaredName != "Create" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.ReturnType != result || input.Signature.Parameters.Count != 0)
                Fail(work, "Invalid selected default equality comparer factory declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.EquatableComparerCreate), work, offset);
            var argument = target.DeclaringType.Arguments[0]; CheckArgumentAccess(argument, work, offset);
            bool integer = argument is TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 or PrimitiveTypeCode.UInt32 or PrimitiveTypeCode.Int64 or PrimitiveTypeCode.UInt64 };
            if (!integer && (argument is TypeSignature.Primitive or TypeSignature.Vector || IsNullable(argument, work, offset)
                || !ReferenceKeyEquatable(argument, work, offset)))
                Fail(work, "Default equality comparer requires an admitted integer or exact IEquatable<T> contract; specialized string, enum, nullable and object fallbacks need later selection.", offset);
            var factory = Require(resolver.ResolveType(library, "Cilica.Runtime", "EquatableComparerFactory`1"), work, offset);
            target = Require(resolver.ResolveClosedDeclaration(new(factory, new([argument])), "Create", 0, _metadata), work, offset);
        }
    }
}
