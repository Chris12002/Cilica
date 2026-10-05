using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace Cilica.Metadata.Resolution;

/// <summary>Original declaration and its mapped declaration, with both constructed environments retained.</summary>
public sealed record LibraryMethodBinding(ResolvedMethod Contract, ResolvedMethod Implementation);
public sealed record LibraryFieldBinding(ResolvedField Contract, ResolvedField Implementation);

public sealed partial class AssemblyResolver
{
    /// <summary>Matches declared members before generic substitution. Does not grant execution/intrinsic authority.</summary>
    public ResolutionResult<LibraryMethodBinding> ResolveLibraryMethod(LibraryBindingPlan plan,
        ResolvedAssembly assembly, EntityHandle handle, MemberResolutionBudget? budget = null) =>
        ResolveLibraryMethodCore(plan, assembly, handle, null, budget);

    private ResolutionResult<LibraryMethodBinding> ResolveLibraryMethodCore(LibraryBindingPlan plan,
        ResolvedAssembly assembly, EntityHandle handle, BindingContext? context, MemberResolutionBudget? budget)
    {
        CheckLibraryPlan(plan); budget ??= new();
        return Bind(assembly, handle, () =>
        {
            CheckLibraryImage(plan, assembly);
            var source = Method(assembly, handle, context, declaredOnly: true, budget: budget);
            var owner = ProjectLibraryOwner(plan, source.DeclaringType, budget);
            var arguments = new SignatureList<TypeSignature>(source.MethodArguments.Select(a => ProjectLibrarySignature(plan, a, budget)));
            var sourceImage = source.DeclaringType.Definition.Assembly;
            var sourceDefinition = sourceImage.Image.Reader.GetMethodDefinition(source.Handle);
            string name = sourceImage.Image.Reader.GetString(sourceDefinition.Name);
            var wanted = ProjectLibraryMethodSignature(plan, MethodDefinitionSignature(sourceImage, source.Handle), budget);
            var targetImage = owner.Definition.Assembly;
            var reader = targetImage.Image.Reader;
            MethodDefinitionHandle selected = default;
            foreach (var candidate in reader.GetTypeDefinition(owner.Definition.Handle).GetMethods())
            {
                InspectMember(budget);
                var definition = reader.GetMethodDefinition(candidate);
                if (!reader.StringComparer.Equals(definition.Name, name)) continue;
                var signature = ProjectLibraryMethodSignature(plan, MethodDefinitionSignature(targetImage, candidate), budget);
                if (signature != wanted) continue;
                if (!selected.IsNil) throw Failure(DiagnosticIds.MemberConflict, "Multiple implementation methods match the mapped declaration: " + name);
                selected = candidate;
            }
            if (selected.IsNil) throw Failure(DiagnosticIds.MemberNotFound, "No implementation method matches the mapped owner and declaration signature: " + name);
            var targetDefinition = reader.GetMethodDefinition(selected);
            const MethodAttributes flags = MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.Virtual
                | MethodAttributes.Abstract | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            if ((sourceDefinition.Attributes & flags) != (targetDefinition.Attributes & flags))
                throw InvalidLibraryBinding("Mapped method access, instance/static, dispatch or special-name attributes differ: " + name);
            CompareLibraryParameters(plan, sourceImage, source.Handle, targetImage, selected, budget);
            return new LibraryMethodBinding(source, new(owner, selected, arguments, Substitute(wanted, owner.Arguments, arguments)));
        });
    }

    public ResolutionResult<LibraryFieldBinding> ResolveLibraryField(LibraryBindingPlan plan,
        ResolvedAssembly assembly, EntityHandle handle, MemberResolutionBudget? budget = null) =>
        ResolveLibraryFieldCore(plan, assembly, handle, null, budget);

    private ResolutionResult<LibraryFieldBinding> ResolveLibraryFieldCore(LibraryBindingPlan plan,
        ResolvedAssembly assembly, EntityHandle handle, BindingContext? context, MemberResolutionBudget? budget)
    {
        CheckLibraryPlan(plan); budget ??= new();
        return Bind(assembly, handle, () =>
        {
            CheckLibraryImage(plan, assembly);
            var source = Field(assembly, handle, context, declaredOnly: true, budget: budget);
            var owner = ProjectLibraryOwner(plan, source.DeclaringType, budget);
            var sourceImage = source.DeclaringType.Definition.Assembly;
            var sourceDefinition = sourceImage.Image.Reader.GetFieldDefinition(source.Handle);
            string name = sourceImage.Image.Reader.GetString(sourceDefinition.Name);
            var wanted = ProjectLibrarySignature(plan, new SignatureDecoder(this, sourceImage).Field(sourceDefinition.Signature), budget);
            var targetImage = owner.Definition.Assembly; var reader = targetImage.Image.Reader;
            FieldDefinitionHandle selected = default;
            foreach (var candidate in reader.GetTypeDefinition(owner.Definition.Handle).GetFields())
            {
                InspectMember(budget); var definition = reader.GetFieldDefinition(candidate);
                if (!reader.StringComparer.Equals(definition.Name, name)) continue;
                var signature = ProjectLibrarySignature(plan, new SignatureDecoder(this, targetImage).Field(definition.Signature), budget);
                if (signature != wanted) continue;
                if (!selected.IsNil) throw Failure(DiagnosticIds.MemberConflict, "Multiple implementation fields match the mapped declaration: " + name);
                selected = candidate;
            }
            if (selected.IsNil) throw Failure(DiagnosticIds.MemberNotFound, "No implementation field matches the mapped owner and declaration signature: " + name);
            var targetDefinition = reader.GetFieldDefinition(selected);
            const FieldAttributes flags = FieldAttributes.FieldAccessMask | FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Literal;
            if ((sourceDefinition.Attributes & flags) != (targetDefinition.Attributes & flags))
                throw InvalidLibraryBinding("Mapped field access, static, readonly or literal attributes differ: " + name);
            if ((sourceDefinition.Attributes & FieldAttributes.Literal) != 0
                && LibraryConstant(sourceImage, sourceDefinition.GetDefaultValue(), budget) != LibraryConstant(targetImage, targetDefinition.GetDefaultValue(), budget))
                throw InvalidLibraryBinding("Mapped literal field values differ: " + name);
            return new LibraryFieldBinding(source, new(owner, selected, Substitute(wanted, owner.Arguments, SignatureList<TypeSignature>.Empty)));
        });
    }

    private static ConstructedType ProjectLibraryOwner(LibraryBindingPlan plan, ConstructedType owner, MemberResolutionBudget budget) =>
        new(CanonicalLibraryType(plan, owner.Definition), new(owner.Arguments.Select(a => ProjectLibrarySignature(plan, a, budget))));

    private static MethodSignature ProjectLibraryMethodSignature(LibraryBindingPlan plan, MethodSignature signature, MemberResolutionBudget budget) =>
        signature with { ReturnType = ProjectLibrarySignature(plan, signature.ReturnType, budget),
            Parameters = new(signature.Parameters.Select(p => ProjectLibrarySignature(plan, p, budget))) };

    private static string LibraryConstant(ResolvedAssembly assembly, ConstantHandle handle, MemberResolutionBudget budget)
    {
        InspectMember(budget);
        if (handle.IsNil) throw InvalidLibraryBinding("A mapped literal field must have a constant value.");
        var constant = assembly.Image.Reader.GetConstant(handle);
        var blob = assembly.Image.Reader.GetBlobReader(constant.Value);
        if (blob.Length > 4096) throw Failure(DiagnosticIds.TranslationLimit, "Mapped literal constant exceeds 4096 bytes.");
        return constant.TypeCode.ToString() + ":" + Convert.ToHexString(blob.ReadBytes(blob.Length));
    }
}
