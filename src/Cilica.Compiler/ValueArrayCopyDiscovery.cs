using System;
using System.Linq;
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
        private void ArraySectionCopy(Work work, MethodInstruction instruction, int callIndex, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System", "Array").Value
                || target.Signature.Parameters.Count is not (3 or 5)) return;
            int offset = instruction.Offset, count = target.Signature.Parameters.Count, destination = count == 3 ? 1 : 2;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Array.Copy metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Array.Copy metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName != "Copy") return;
            var arrayDefinition = target.DeclaringType.Definition;
            bool ArrayParameter(TypeSignature signature) => signature is TypeSignature.Named { Kind: SignatureTypeKind.Class } array
                && array.Definition == arrayDefinition;
            if (instruction.OpCode != "call" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || target.MethodArguments.Count != 0 || target.DeclaringType.Arguments.Count != 0
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters.Where((_, i) => i == 0 || i == destination).Any(p => !ArrayParameter(p))
                || input.Signature.Parameters.Where((_, i) => i != 0 && i != destination).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                Fail(work, "Array.Copy requires the exact selected static Array and Int32 declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ArraySectionCopy), work, offset);
            var arguments = VectorCallArguments(work, callIndex, count);
            if (arguments is null) Fail(work, "Array.Copy requires same-block typed vector producers; joined argument stacks require later lowering.", offset);
            TypeSignature? Element(int index)
            {
                var argument = arguments![index];
                if (argument.LiteralNull) return null;
                if (argument.Signature is TypeSignature.Vector vector) return vector.Element;
                Fail(work, "Array.Copy requires same-block typed vector producers; general Array aliases require later lowering.", offset);
                return null;
            }
            var sourceElement = Element(0); var destinationElement = Element(destination);
            var element = sourceElement ?? destinationElement ?? new TypeSignature.Primitive(PrimitiveTypeCode.Int32);
            CheckArgumentAccess(element, work, offset);
            if (sourceElement is not null && destinationElement is not null
                && Vector(sourceElement, work, offset) != Vector(destinationElement, work, offset))
                Fail(work, "Array.Copy currently requires the same declared vector element type; cross-element widening and boxing require later lowering.", offset);
            _ = Vector(element, work, offset);
            var owner = Require(resolver.ResolveType(library, "Cilica.Runtime", "ArraySections`1"), work, offset);
            var helper = Require(resolver.ResolveClosedDeclaration(new(owner, new([element])), count == 3 ? "CopyWhole" : "CopySection", count, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, helper.Handle, LibraryIntrinsicCapability.ArraySectionCopyBridge), work, offset);
            var body = Read(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 1 || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || body.Implementation != MethodImplAttributes.IL
                || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || body.Signature.Parameters.Count != count
                || body.Signature.Parameters.Where((_, i) => i == 0 || i == destination).Any(p => p is not TypeSignature.Vector { Element: TypeSignature.Variable { IsMethod: false, Index: 0 } })
                || body.Signature.Parameters.Where((_, i) => i != 0 && i != destination).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                Fail(work, "Array.Copy requires the exact managed typed-vector bridge.", offset);
            target = helper; // Ordinary call lowering independently checks actual stack types and access.
        }
    }
}
