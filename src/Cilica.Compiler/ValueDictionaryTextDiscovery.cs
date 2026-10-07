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
        private void DictionaryKeyText(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "DictionaryKeyText`1").Value) return;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Dictionary key text metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Dictionary key text metadata limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (work.Assembly != library || instruction.OpCode != "call" || input.DeclaredName != "Render"
                || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 1 || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] != new TypeSignature.Variable(false, 0)
                || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.String))
                Fail(work, "Dictionary key text requires the exact private owned declaration.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.DictionaryKeyText), work, offset);
            var argument = target.DeclaringType.Arguments[0]; CheckArgumentAccess(argument, work, offset);
            if (argument is TypeSignature.Primitive primitive)
            {
                string? method = primitive.Code switch {
                    PrimitiveTypeCode.Int32 => "Int32Invariant", PrimitiveTypeCode.UInt32 => "UInt32Invariant",
                    PrimitiveTypeCode.Int64 => "Int64Invariant", PrimitiveTypeCode.UInt64 => "UInt64Invariant",
                    PrimitiveTypeCode.String => "Render", _ => null };
                if (method is null) Fail(work, "Dictionary key text requires an admitted integer, string or ordinary named value.", offset);
                var helper = Require(resolver.ResolveType(library, primitive.Code == PrimitiveTypeCode.String ? "Cilica.Runtime" : "Cilica.Library",
                    primitive.Code == PrimitiveTypeCode.String ? "StringKeyText" : "IntegerFormatting"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(helper, SignatureList<TypeSignature>.Empty), method!, 1, _metadata), work, offset);
            }
            else
            {
                _ = FormattingReceiver(argument, work, offset);
                var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "BoxedKeyText`1"), work, offset);
                target = Require(resolver.ResolveClosedDeclaration(new(helper, new([argument])), "Render", 1, _metadata), work, offset);
            }
        }
    }
}
