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
        private bool _defaultTextUsed;
        private void ValidateDefaultTextNames(ValueTypeCatalog catalog, Work root)
        {
            if (!_defaultTextUsed) return;
            foreach (var layout in catalog.References.Types)
            {
                string name=layout.Source.RuntimeName ?? layout.Source.Name.Replace('/', '+');
                if (name.Length>ObjectStringLimits.MaximumLiteralCodeUnits || name.Length>ObjectStringLimits.MaximumTotalLiteralCodeUnits-_literalUnits)
                    Fail(root, "Dynamic default type names exceed the string literal budget.", code: DiagnosticIds.TranslationLimit);
                _literalUnits+=name.Length;
            }
        }
        private void RejectRuntimeTypeText(ConstructedType receiver, Work work, int offset)
        {
            var type = Require(resolver.ResolveType(library, "System", "Type"), work, offset);
            var member = Require(resolver.ResolveType(library, "System.Reflection", "MemberInfo"), work, offset);
            var implementation = Require(resolver.ResolveType(library, "Cilica.Runtime", "RuntimeTypeObject"), work, offset);
            if (receiver.Definition == type || receiver.Definition == member || receiver.Definition == implementation)
                Fail(work, "Runtime type text requires separate reflected-name semantics.", offset);
        }
        private ValueIntrinsicCall? ObjectDefaultTextCall(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "ObjectText").Value) return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Default object text metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Default object text metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var obj = Require(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Object), work, offset);
            var caller = Require(resolver.ResolveClosedDeclaration(new(obj, SignatureList<TypeSignature>.Empty), "ToString", 0, _metadata), work, offset);
            if (instruction.OpCode != "call" || work.Assembly != library || work.Resolved != caller
                || input.DeclaredName != "Default" || input.IsReferenceAssembly || input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || target.DeclaringType.Arguments.Count != 0 || target.MethodArguments.Count != 0
                || input.Attributes != (MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Implementation != MethodImplAttributes.InternalCall || input.Signature.Header != 0 || input.Signature.GenericArity != 0
                || input.Signature.Parameters.Count != 1 || input.Signature.Parameters[0] != new TypeSignature.Primitive(PrimitiveTypeCode.Object)
                || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.String))
                Fail(work, "Default object text requires its exact private declaration and Object.ToString caller.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.ObjectDefaultText), work, offset);
            _defaultTextUsed=true;
            return new(ValueIntrinsic.ObjectDefaultText, IncludeString(work, offset), 1);
        }
    }
}
