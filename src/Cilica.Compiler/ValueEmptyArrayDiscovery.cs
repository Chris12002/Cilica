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
        private ValueIntrinsicCall? EmptyArrayCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library) return null;
            var owner = resolver.ResolveType(library, "System", "Array").Value;
            if (owner is null || owner != target.DeclaringType.Definition) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Empty-array metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Empty-array metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            // Ordinary managed Array methods use normal body validation. The
            // Empty intrinsic retains its exact declaration and grant checks.
            if (input.DeclaredName != "Empty") return null;
            if (instruction.OpCode != "call" || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 1
                || input.DeclaredName != "Empty" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer || input.OwnerGenericArity != 0
                || input.Signature.Header != 0x10 || input.Signature.GenericArity != 1 || input.Signature.Parameters.Count != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Vector { Element: TypeSignature.Variable { IsMethod: true, Index: 0 } })
                Fail(work, "Only the exact selected Array.Empty<T> declaration is implemented.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ArrayEmpty), work, offset);
            // Vector retains exact reference/value element identities and rejects
            // unsigned and narrow elements whose runtime descriptors are not distinct.
            return new(ValueIntrinsic.EmptyArray, Vector(target.MethodArguments[0], work, offset), 0);
        }
    }
}
