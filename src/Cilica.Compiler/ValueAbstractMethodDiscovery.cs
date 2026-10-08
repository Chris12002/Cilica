using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata;
using Cilica.Metadata.IL;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private sealed record AbstractWork(ResolvedMethod Resolved, ObjectAbstractMethodDeclaration Declaration, ObjectMethodBinding Binding);
        private readonly Dictionary<IrMethodId, AbstractWork> _abstractMethods = [];
        private readonly Dictionary<(string Image, int Token), bool> _abstractFlags = [];

        private ObjectAbstractMethodDeclaration? AbstractDeclaration(ResolvedMethod target, Work work, int offset)
        {
            var owner = target.DeclaringType.Definition;
            var key = (owner.Assembly.ContentHash, MetadataTokens.GetToken(target.Handle));
            if (!_abstractFlags.TryGetValue(key, out bool abstractMethod))
            {
                if (_abstractFlags.Count >= ProgramLimits.MaximumMethods) Fail(work, "Class declaration flags exceed the method limit.", offset, DiagnosticIds.TranslationLimit);
                var flags = Require(resolver.ReadMethodFlags(owner.Assembly, target.Handle, _metadata), work, offset);
                abstractMethod = (flags.Attributes & MethodAttributes.Abstract) != 0;
                _abstractFlags.Add(key, abstractMethod);
            }
            if (!abstractMethod) return null;
            var id = MethodId(target);
            if (_abstractMethods.TryGetValue(id, out var cached))
            {
                if (!ObjectMethodAccess.Allowed(work.Binding, cached.Binding, integers64)) Fail(work, "Abstract call requires an accessible declaration.", offset);
                return cached.Declaration;
            }
            if (_methods.Count + _abstractMethods.Count + _exceptionDeclarations.Count >= ProgramLimits.MaximumMethods || _metadata.Remaining == 0)
                Fail(work, "Abstract declarations share the method and metadata limits.", offset, DiagnosticIds.TranslationLimit);
            var input = Require(resolver.ReadMethodDeclarationInput(owner.Assembly, target.Handle, new(MaximumMembers: _metadata.Remaining)), work, offset);
            if (!_metadata.TryConsume(input.MetadataMembersRead)) Fail(work, "Abstract declaration metadata budget exceeded.", offset, DiagnosticIds.TranslationLimit);
            if (input.HasBody || input.Implementation != MethodImplAttributes.IL || input.IsReferenceAssembly || input.HasModuleInitializer
                || input.OwnerGenericArity != target.DeclaringType.Arguments.Count || target.MethodArguments.Count != 0
                || input.Signature.GenericArity != 0 || input.Signature.Header != 0x20)
                Fail(work, "Abstract class declarations require bodyless ordinary managed instance signatures with no generic method arguments.", offset);
            var exact = ResolveVirtual(target, target.DeclaringType, work, offset);
            var ownerId = IncludeReference(target.DeclaringType, work, offset);
            var binding = new ObjectMethodBinding(id, ownerId, ObjectMethodKind.Instance, input.Attributes, input.OwnerExternallyVisible)
            {
                LexicalOwners = input.LexicalOwners, OwnerAssemblyVisible = input.OwnerAssemblyVisible,
                VirtualSlot = new(MethodId(exact.Slot), ObjectModelBuilder.Id(exact.Slot.DeclaringType)), VirtualName = input.DeclaredName
            };
            if (!ObjectMethodAccess.Allowed(work.Binding, binding, integers64)) Fail(work, "Abstract call requires an accessible declaration.", offset);
            ImmutableArray<IrType> parameters = [IrType.Reference(ownerId), .. target.Signature.Parameters.Select(t => Storage(t, work, offset, borrow: true))];
            var returned = Storage(target.Signature.ReturnType, work, offset, allowVoid: true);
            if (parameters.Any(t => t.Kind is not (IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Reference or IrTypeKind.Value))
                || returned.Kind is not (IrTypeKind.Int32 or IrTypeKind.Int64 or IrTypeKind.Reference or IrTypeKind.Void))
                Fail(work, "Abstract class calls support copied value parameters with scalar/reference/void results; value returns and borrows need later lowering.", offset);
            _parameters += parameters.Length;
            if (_parameters > IrLimits.MaximumParameters) Fail(work, "Aggregate declaration parameter limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            var declaration = new ObjectAbstractMethodDeclaration(input.Source with { Specialization = id.Specialization }, parameters, returned);
            _abstractMethods.Add(id, new(target, declaration, binding));
            return declaration;
        }
    }
}
