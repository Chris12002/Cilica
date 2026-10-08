using System.Collections.Generic;
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
        private readonly Dictionary<ObjectTypeId, (TypeSignature Element, Work Caller, int Offset)> _ordinalOrderingSources = [];

        private IrMethodId? OrdinalOrdering(Work work, MethodInstruction instruction, ref ResolvedMethod target)
        {
            if (integers64 && target.DeclaringType.Definition.Assembly == library && work.Assembly != library
                && target.DeclaringType.Definition == resolver.ResolveType(library, "Cilica.Runtime", "OrdinalOrderBridge`1").Value)
                Fail(work, "Ordinal ordering bridges are compiler-selected; direct application calls are not admitted.", instruction.Offset);
            if (!integers64 || target.DeclaringType.Definition.Assembly != library
                || target.MethodArguments.Count != 2 || target.Signature.Parameters.Count != 3
                || target.MethodArguments[1] is not TypeSignature.Primitive { Code: PrimitiveTypeCode.String }
                || target.DeclaringType.Definition != resolver.ResolveType(library, "System.Linq", "Enumerable").Value) return null;
            int offset = instruction.Offset;
            MethodDeclarationInput Read(ResolvedMethod method)
            {
                if (_metadata.Remaining == 0) Fail(work, "Ordinal ordering metadata budget exhausted.", offset, DiagnosticIds.TranslationLimit);
                var result = Require(resolver.ReadMethodDeclarationInput(library, method.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
                if (!_metadata.TryConsume(result.MetadataMembersRead)) Fail(work, "Ordinal ordering metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
                return result;
            }
            var input = Read(target);
            if (input.DeclaredName is not ("OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending")) return null;
            bool then = input.DeclaredName.StartsWith("Then", System.StringComparison.Ordinal);
            var element = new TypeSignature.Variable(true, 0); var key = new TypeSignature.Variable(true, 1);
            TypeSignature.Generic Contract(string ns, string name, params TypeSignature[] args) => new(new(Require(resolver.ResolveType(library, ns, name), work, offset), SignatureTypeKind.Class), new(args));
            var ordered = Contract("System.Linq", "IOrderedEnumerable`1", element);
            var source = then ? ordered : Contract("System.Collections.Generic", "IEnumerable`1", element);
            if (instruction.OpCode != "call" || input.IsReferenceAssembly || !input.HasBody || input.HasTypeInitializer || input.HasModuleInitializer
                || input.OwnerGenericArity != 0 || input.Signature.Header != 0x10 || input.Signature.GenericArity != 2
                || input.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig) || input.Implementation != MethodImplAttributes.IL
                || input.Signature.ReturnType != ordered || input.Signature.Parameters.Count != 3
                || input.Signature.Parameters[0] != source || input.Signature.Parameters[1] != Contract("System", "Func`2", element, key)
                || input.Signature.Parameters[2] != Contract("System.Collections.Generic", "IComparer`1", key))
                Fail(work, "Ordinal ordering requires an exact selected Enumerable string-key comparer overload.", offset);
            CheckArgumentAccess(target.MethodArguments[0], work, offset);
            var helper = Require(resolver.ResolveType(library, "Cilica.Runtime", "OrdinalOrderBridge`1"), work, offset);
            var bridge = Require(resolver.ResolveClosedDeclaration(new(helper, new([target.MethodArguments[0]])), input.DeclaredName, 3, _metadata), work, offset);
            _ = Require(resolver.AuthorizeLibraryIntrinsic(library, bridge.Handle, LibraryIntrinsicCapability.OrdinalOrdering), work, offset);
            var body = Read(bridge);
            if (body.IsReferenceAssembly || !body.HasBody || body.HasTypeInitializer || body.HasModuleInitializer
                || body.OwnerGenericArity != 1 || body.Signature.Header != 0 || body.Signature.GenericArity != 0
                || body.Attributes != (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig) || body.Implementation != MethodImplAttributes.IL
                || bridge.Signature.ReturnType != target.Signature.ReturnType || !bridge.Signature.Parameters.SequenceEqual(target.Signature.Parameters))
                Fail(work, "Ordinal ordering requires the exact managed comparer-preserving bridge.", offset);
            if (then)
            {
                var closed = new ConstructedType(ordered.Definition.Definition, new([target.MethodArguments[0]]));
                _ordinalOrderingSources.TryAdd(IncludeReference(closed, work, offset), (target.MethodArguments[0], work, offset));
            }
            var getter = OrdinalComparerGetter(work, offset); target = bridge; return getter;
        }

        private void ValidateOrdinalOrderingSources(ValueTypeCatalog catalog)
        {
            if (_ordinalOrderingSources.Count == 0) return;
            var relations = new IrTypeRules(catalog.References);
            var owned = Require(resolver.ResolveType(library, "Cilica.Runtime", "OrderedIterator`2"), _ordinalOrderingSources.First().Value.Caller, _ordinalOrderingSources.First().Value.Offset);
            foreach (var (contract, site) in _ordinalOrderingSources)
            {
                bool IsSource(ObjectTypeId id) => relations.Assignable(IrType.Reference(id), IrType.Reference(contract));
                foreach (var type in catalog.References.Types)
                {
                    var id = ObjectTypeId.From(type.Source);
                    if ((type.Attributes & TypeAttributes.Abstract) != 0 || !IsSource(id)) continue;
                    if (!_knownTypes.TryGetValue(id, out var closed) || closed.Definition != owned || closed.Arguments[0] != site.Element)
                        Fail(site.Caller, "Ordinal ThenBy currently requires only owned OrderedIterator receivers in the closed source universe; custom or covariant IOrderedEnumerable implementations require later lowering.", site.Offset);
                }
                if (catalog.Types.Any(type => type.Interfaces.Any(IsSource)))
                    Fail(site.Caller, "Ordinal ThenBy does not yet admit boxed value-type ordered sources.", site.Offset);
            }
        }
    }
}
