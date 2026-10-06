using System.Linq;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;

namespace Cilica.Compiler;

public static partial class ValueProgramDiscovery
{
    private sealed partial class Session
    {
        private bool _nullableRead;
        private ResolvedType? _nullable;
        private ResolvedType? _nullableBoxing;
        private bool _nullableBoxingRead;

        private IrMethodId NullableBoxCall(TypeSignature signature, string opcode, Work work, int offset)
        {
            if (!integers64 || opcode == "unbox")
                Fail(work, "Nullable boxing and unboxing require the integer-object copy profile; raw nullable unbox needs temporary-location lifetime semantics before execution.", offset);
            var nullable = (TypeSignature.Generic)signature;
            CheckArgumentAccess(signature, work, offset);
            var payload = nullable.Arguments[0];
            var storage = Storage(payload, work, offset);
            if (storage.Kind != IrTypeKind.Value)
                Fail(work, "Nullable boxing and unboxing require an already supported named-struct payload; primitive/enum boxes need nominal payload descriptors before execution.", offset);
            // Resolve managed code, not a new intrinsic. The helper remains subject
            // to ordinary discovery, verification, resource bounds and root planning.
            var helper = NullableBoxingHelper(work, offset);
            if (helper is null) Fail(work, "Nullable boxing requires the selected core NullableBoxing<T> managed helper.", offset);
            var target = Require(resolver.ResolveClosedDeclaration(new(helper!, new([payload])),
                opcode == "box" ? "Box" : "Unbox", 1, _metadata), work, offset);
            var callee = Discover(target, work, offset);
            var nullableStorage = Storage(signature, work, offset);
            var objectStorage = IrType.Reference(ObjectBase());
            if (callee.Binding.Kind != ObjectMethodKind.Static || callee.Parameters.Length != 1
                || callee.Parameters[0] != (opcode == "box" ? nullableStorage : objectStorage)
                || callee.ReturnType != (opcode == "box" ? objectStorage : nullableStorage)
                || !ObjectMethodAccess.Allowed(work.Binding, callee.Binding, integers64))
                Fail(work, "Nullable boxing helper requires the exact accessible static copy signature.", offset);
            if (++_arguments > ProgramLimits.MaximumCallArguments)
                Fail(work, "Aggregate nullable helper argument limit exceeded.", offset, DiagnosticIds.TranslationLimit);
            return callee.Binding.Method;
        }

        private ResolvedType? NullableBoxingHelper(Work work, int offset)
        {
            if (!_nullableBoxingRead)
            {
                if (!_metadata.TryConsume(1)) Fail(work, "Nullable helper lookup exceeds the metadata budget.", offset, DiagnosticIds.TranslationLimit);
                var result = resolver.ResolveType(library, "Cilica.Runtime", "NullableBoxing`1");
                if (result.Value is null && result.Diagnostics.Any(d => d.Id != DiagnosticIds.TypeNotFound))
                    _ = Require(result, work, offset);
                _nullableBoxing = result.Value; _nullableBoxingRead = true;
            }
            return _nullableBoxing;
        }

        private bool NullableUnboxHelper(Work work, TypeSignature payload, int offset) =>
            work.Assembly == library && work.Input.DeclaredName == "Unbox"
            && work.Resolved.DeclaringType.Arguments.Count == 1
            && work.Resolved.DeclaringType.Definition == NullableBoxingHelper(work, offset)
            && work.Resolved.DeclaringType.Arguments[0] == payload;

        private bool IsNullable(TypeSignature signature, Work work, int offset)
        {
            if (signature is not TypeSignature.Generic type) return false;
            if (!_nullableRead)
            {
                if (!_metadata.TryConsume(1)) Fail(work, "Nullable identity lookup exceeds the metadata budget.", offset, DiagnosticIds.TranslationLimit);
                var result = resolver.ResolveType(library, "System", "Nullable`1");
                if (result.Value is null && result.Diagnostics.Any(d => d.Id != DiagnosticIds.TypeNotFound))
                    _ = Require(result, work, offset);
                _nullable = result.Value;
                _nullableRead = true;
            }
            return type.Definition.Definition == _nullable;
        }
    }
}
