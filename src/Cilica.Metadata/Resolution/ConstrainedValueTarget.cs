using System;
using System.Reflection;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

/// <summary>Metadata evidence for a direct, unboxed value body; no CIL body is executed or validated.</summary>
public sealed record ConstrainedValueTarget(ResolvedMethod Declaration, ConstructedType Receiver,
    ResolvedMethod Implementation, bool IsExplicit)
{
    public const string Profile = "constrained-value-method-target-v2";
}

public sealed partial class AssemblyResolver
{
    /// <summary>Selects exact interface bodies or Object/ValueType overrides on an ordinary closed value type.</summary>
    public ResolutionResult<ConstrainedValueTarget> ResolveConstrainedValueTarget(ResolvedMethod declaration,
        ConstructedType receiver, ResolvedAssembly runtimeLibrary, MemberResolutionBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(declaration); ArgumentNullException.ThrowIfNull(receiver);
        CheckOwner(receiver.Definition.Assembly); CheckOwner(runtimeLibrary);
        budget ??= new();
        var assembly = declaration.DeclaringType.Definition.Assembly;
        return Bind(assembly, declaration.Handle, () =>
        {
            var validate = ClosedDispatchValidation(runtimeLibrary, budget);
            validate(receiver); validate(declaration.DeclaringType);
            var authenticated = Method(assembly, declaration.Handle,
                new(declaration.DeclaringType, declaration.MethodArguments), declaredOnly: true, budget: budget);
            if (authenticated != declaration)
                throw new BadImageFormatException("Constrained declaration must match its exact metadata owner and signature.");
            var valueBase = ResolveType(runtimeLibrary, "System", "ValueType");
            if (valueBase.Value is null) throw new BindingFailureException(valueBase.Diagnostics);
            var objectBase = ResolveRuntimeType(runtimeLibrary, PrimitiveTypeCode.Object);
            if (objectBase.Value is null) throw new BindingFailureException(objectBase.Diagnostics);
            InspectMember(budget);
            var type = receiver.Definition.Assembly.Image.Reader.GetTypeDefinition(receiver.Definition.Handle);
            var parent = type.BaseType.IsNil ? null : new SignatureDecoder(this, receiver.Definition.Assembly).Type(type.BaseType);
            if ((type.Attributes & (TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Interface)) != TypeAttributes.Sealed
                || parent is not TypeSignature.Named baseType || baseType.Definition != valueBase.Value)
                throw new UnsupportedBindingException("Constrained direct value targets require a sealed direct child of the selected ValueType; class, enum and stack-only receivers require other lowering.");

            bool contract = (declaration.DeclaringType.Definition.Assembly.Image.Reader
                .GetTypeDefinition(declaration.DeclaringType.Definition.Handle).Attributes & TypeAttributes.Interface) != 0;
            // Authenticate all interface rows, including explicit mappings, even for an Object override.
            var interfaces = ReadInterfaceHierarchy([receiver], budget, contract ? declaration.DeclaringType : null, validate);
            ResolvedMethod? body; bool explicitBody = false;
            if (contract)
            {
                ValidateInterfaceMethod(declaration, closed: true);
                if (!interfaces.Implemented.Contains(declaration.DeclaringType))
                    throw Failure(DiagnosticIds.MemberNotFound, "Constrained receiver does not implement the exact interface declaration.");
                explicitBody = interfaces.Mappings[receiver].TryGetValue(declaration, out body);
                if (!explicitBody) body = InterfaceNameMatch(receiver, declaration, budget, closed: true);
            }
            else
            {
                if (declaration.DeclaringType.Arguments.Count != 0
                    || declaration.DeclaringType.Definition != objectBase.Value && declaration.DeclaringType.Definition != valueBase.Value)
                    throw new UnsupportedBindingException("Constrained class declarations require the selected Object or ValueType virtual member.");
                ValidateClassVirtual(declaration, closed: true);
                body = InterfaceNameMatch(receiver, declaration, budget, closed: true);
                if (body is not null)
                {
                    ValidateClassVirtual(body, closed: true);
                    if ((MethodFlags(body) & MethodAttributes.NewSlot) != 0)
                        throw new UnsupportedBindingException("A newslot value method does not override the selected Object/ValueType declaration.");
                }
            }
            if (body is null)
                throw new UnsupportedBindingException("Constrained value call requires boxing or a runtime/default-interface fallback; only an exact direct value body is implemented.");
            if (body.DeclaringType != receiver || body.Signature != declaration.Signature)
                throw new BadImageFormatException("Constrained value body must belong to its receiver and preserve the declaration signature.");
            return new ConstrainedValueTarget(declaration, receiver, body, explicitBody);
        });
    }
}
