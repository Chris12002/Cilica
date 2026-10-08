using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private void ArraySectionClear(Work work, MethodInstruction instruction, int callIndex, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System", "Array").Value
                || target.Signature.Parameters.Count != 3) return;
            int offset = instruction.Offset;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Array.Clear metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Array.Clear metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName != "Clear") return;
            if (instruction.OpCode != "call" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || target.MethodArguments.Count != 0 || target.DeclaringType.Arguments.Count != 0
                || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || input.Signature.Parameters[0] is not TypeSignature.Named { Kind: SignatureTypeKind.Class } array
                || array.Definition != target.DeclaringType.Definition
                || input.Signature.Parameters.Skip(1).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                Fail(work, "Array.Clear requires the exact selected static Array, Int32, Int32 declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ArraySectionClear), work, offset);
            var element = ClearArrayElement(work, callIndex);
            if (element is null)
                Fail(work, "Array.Clear requires a typed vector producer in the same block; general Array aliases and joined argument stacks require later lowering.", offset);
            CheckArgumentAccess(element!, work, offset);
            _ = Vector(element!, work, offset);
            var owner = Require(resolver.ResolveType(library, "Cilica.Runtime", "ArraySections`1"), work, offset);
            var helper = Require(resolver.ResolveClosedDeclaration(new(owner, new([element!])), "ClearSection", 3, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, helper.Handle, LibraryIntrinsicCapability.ArraySectionClearBridge), work, offset);
            var body = Read(helper);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 1 || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig)
                || body.Implementation != MethodImplAttributes.IL
                || body.Signature.ReturnType is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Void }
                || body.Signature.Parameters.Count != 3
                || body.Signature.Parameters[0] is not TypeSignature.Vector { Element: TypeSignature.Variable { IsMethod: false, Index: 0 } }
                || body.Signature.Parameters.Skip(1).Any(p => p is not TypeSignature.Primitive { Code: PrimitiveTypeCode.Int32 }))
                Fail(work, "Array.Clear requires the exact managed typed-vector bridge.", offset);
            // Ordinary call lowering independently requires the actual stack value
            // to be assignable to this exact closed vector parameter.
            target = helper;
        }

        private TypeSignature? ClearArrayElement(Work work, int callIndex)
        {
            var arguments = VectorCallArguments(work, callIndex, 3);
            if (arguments is null) return null;
            var first = arguments[0];
            return first.LiteralNull ? new TypeSignature.Primitive(PrimitiveTypeCode.Int32)
                : first.Signature is TypeSignature.Vector vector ? vector.Element : null;
        }
    }
}
