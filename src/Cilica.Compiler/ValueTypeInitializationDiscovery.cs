using System.Collections.Generic;
using Cilica.Metadata.Resolution;
namespace Cilica.Compiler;
public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private readonly Dictionary<ObjectTypeId, IrMethodId> _fieldInitializers = [];
        private IrMethodId FieldInitializer(ConstructedType owner, Work work, int offset)
        {
            var id = ClosedId(owner, work, offset);
            if (_fieldInitializers.TryGetValue(id, out var known)) return known;
            if (_fieldInitializers.Count >= ObjectModelLimits.MaximumTypes)
                Fail(work, "Type initialization owner limit exceeded.", offset);
            var exception = Require(resolver.ResolveType(library, "System", "TypeInitializationException"), work, offset);
            IncludeReference(exception, work, offset); _ = IncludeString(work, offset);
            var factory = Require(resolver.ResolveClosedDeclaration(new(exception, SignatureList<TypeSignature>.Empty), ".ctor", 2, _metadata, publicOnly: true), work, offset);
            _ = TypeInitializationExceptionCall(work, new(offset, 1, "newobj", null, []), factory,
                ObjectModelBuilder.Id(exception), ExceptionDeclaration(factory, work, offset));
            var target = Require(resolver.ResolveClosedDeclaration(owner, ".cctor", 0, _metadata), work, offset);
            var method = Discover(target, work, offset);
            if (method.Binding.TypeInitializerName is null) Fail(work, "Initialized field lacks a valid type initializer.", offset);
            _fieldInitializers.Add(id, method.Binding.Method); return method.Binding.Method;
        }
    }
}
