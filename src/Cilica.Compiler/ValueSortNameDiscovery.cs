using System.Collections.Immutable;
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
        private ImmutableArray<ushort>? SortComparisonName(Work work, MethodInstruction instruction, ResolvedMethod target)
        {
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.DeclaringType.Definition != resolver.ResolveType(library, "Cilica.Runtime", "ComparisonSort`1").Value)
                return null;
            int offset = instruction.Offset;
            if (_metadata.Remaining == 0) Fail(work, "Sort comparison name metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(library, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Sort comparison name metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
            if (input.DeclaredName != "ComparisonName") return null;
            if (work.Assembly != library || work.Resolved.DeclaringType != target.DeclaringType || instruction.OpCode != "call"
                || input.IsReferenceAssembly || input.HasTypeInitializer || input.HasModuleInitializer || input.HasBody
                || input.OwnerGenericArity != 1 || input.Signature.GenericArity != 0 || input.Signature.Header != 0
                || target.DeclaringType.Arguments.Count != 1 || target.MethodArguments.Count != 0
                || input.Implementation != MethodImplAttributes.InternalCall
                || input.Attributes != (MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
                || input.Signature.Parameters.Count != 0 || input.Signature.ReturnType != new TypeSignature.Primitive(PrimitiveTypeCode.String))
                Fail(work, "Sort comparison names require the exact private selected declaration and closed-owner caller.", offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, target.Handle, LibraryIntrinsicCapability.SortComparisonName), work, offset);
            var definition = Require(resolver.ResolveType(library, "System", "Comparison`1"), work, offset);
            var declaration = DelegateType(new(definition, target.DeclaringType.Arguments), work, offset);
            string? name = declaration?.Source.RuntimeName;
            if (name is null) Fail(work, "Sort comparison names require an admitted closed Comparison<T> delegate.", offset);
            if (name!.Length > ObjectStringLimits.MaximumLiteralCodeUnits || name.Length > ObjectStringLimits.MaximumTotalLiteralCodeUnits - _literalUnits)
                Fail(work, "Sort comparison name exceeds the string literal budget.", offset, DiagnosticIds.TranslationLimit);
            _literalUnits += name.Length;
            _ = IncludeString(work, offset);
            return [.. name.Select(c => (ushort)c)];
        }
    }
}
