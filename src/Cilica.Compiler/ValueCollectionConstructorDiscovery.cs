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
        private ObjectTypeId? CollectionConstructor(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library || target.Signature.Parameters.Count != 1
                || target.Signature.Parameters[0] is not TypeSignature.Generic) return null;
            var list = resolver.ResolveType(library, "System.Collections.Generic", "List`1").Value;
            if (list is null || target.DeclaringType.Definition != list) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "List constructor metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "List constructor metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (input.DeclaredName != ".ctor") return null;
            var enumerable = Require(resolver.ResolveType(library, "System.Collections.Generic", "IEnumerable`1"), work, offset);
            bool Contract(TypeSignature signature, ResolvedType definition) => signature is TypeSignature.Generic generic
                && generic.Definition.Kind == SignatureTypeKind.Class && generic.Definition.Definition == definition
                && generic.Arguments.Count == 1 && generic.Arguments[0] is TypeSignature.Variable { IsMethod: false, Index: 0 };
            if (instruction.OpCode != "newobj" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Signature.Header != 0x20 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters.Count != 1 || !Contract(input.Signature.Parameters[0], enumerable))
                Fail(work, "List collection construction requires the exact selected IEnumerable<T> newobj declaration.", offset);
            var owner = target.DeclaringType;
            var collection = Require(resolver.ResolveType(library, "System.Collections.Generic", "ICollection`1"), work, offset);
            target = Require(resolver.ResolveClosedDeclaration(owner, "CopyCollection", 1, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ListCollectionConstruction), work, offset);
            if (_metadata.Remaining == 0) Fail(work, "List bridge metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var bridge = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(bridge.MetadataMembersRead)) Fail(work, "List bridge metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (bridge.Signature.Header != 0 || bridge.Signature.GenericArity != 0 || bridge.Signature.Parameters.Count != 1
                || !Contract(bridge.Signature.Parameters[0], collection) || !Contract(bridge.Signature.ReturnType, list)
                || bridge.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || !bridge.HasBody || bridge.Implementation != MethodImplAttributes.IL)
                Fail(work, "List collection bridge requires the exact selected managed copy declaration.", offset);
            return IncludeReference(owner, work, offset);
        }
    }
}
