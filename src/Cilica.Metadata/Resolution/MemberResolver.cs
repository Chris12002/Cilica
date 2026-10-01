using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private sealed record BindingContext(ConstructedType Type, SignatureList<TypeSignature> Methods);

    private static TypeSignature InContext(TypeSignature signature, BindingContext? context) => context is null
        ? signature : Substitute(signature, context.Type.Arguments, context.Methods);

    private static ConstructedType InContext(ConstructedType type, BindingContext? context) =>
        context is not null && type.Definition == context.Type.Definition ? context.Type : type;

    /// <summary>Decodes a structural type; free variables remain in the caller's context.</summary>
    public ResolutionResult<TypeSignature> ResolveSignatureType(ResolvedAssembly assembly, EntityHandle handle) =>
        Bind(assembly, handle, () => new SignatureDecoder(this, assembly).Type(handle));

    /// <summary>Resolves a declaration, not virtual/interface dispatch or generic constraints.</summary>
    public ResolutionResult<ResolvedMethod> ResolveMethod(ResolvedAssembly assembly, EntityHandle handle) =>
        Bind(assembly, handle, () => Method(assembly, handle));

    /// <summary>Resolves only on the referenced owner; inherited-owner obligations remain explicit.</summary>
    public ResolutionResult<ResolvedMethod> ResolveDeclaredMethod(ResolvedAssembly assembly, EntityHandle handle, MemberResolutionBudget? budget = null) =>
        Bind(assembly, handle, () => Method(assembly, handle, declaredOnly: true, budget: budget));

    public ResolutionResult<ResolvedField> ResolveField(ResolvedAssembly assembly, EntityHandle handle) =>
        Bind(assembly, handle, () => Field(assembly, handle));

    /// <summary>Field binding on the exact referenced owner, without inherited-owner fallback.</summary>
    public ResolutionResult<ResolvedField> ResolveDeclaredField(ResolvedAssembly assembly, EntityHandle handle, MemberResolutionBudget? budget = null) =>
        Bind(assembly, handle, () => Field(assembly, handle, declaredOnly: true, budget: budget));

    private ResolutionResult<T> Bind<T>(ResolvedAssembly assembly, EntityHandle handle, Func<T> bind) where T : class
    {
        CheckOwner(assembly);
        string location = $"token 0x{MetadataTokens.GetToken(handle):x8} in {assembly.Identity.DisplayName}";
        try
        {
            if (handle.IsNil) throw new BadImageFormatException("Nil metadata token.");
            return ResolutionResult<T>.Resolved(bind());
        }
        catch (BindingFailureException exception)
        {
            return ResolutionResult<T>.Failed(exception.Diagnostics.Select(d => d with { Location = location }));
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
        {
            return ResolutionResult<T>.Failed(exception is UnsupportedBindingException
                ? DiagnosticIds.ResolutionUnsupported : DiagnosticIds.InvalidSignature, exception.Message, location);
        }
    }

    private ResolvedMethod Method(ResolvedAssembly assembly, EntityHandle handle, BindingContext? context = null, bool declaredOnly = false, MemberResolutionBudget? budget = null)
    {
        InspectMember(budget);
        MetadataReader reader = assembly.Image.Reader;
        var decoder = new SignatureDecoder(this, assembly);
        if (handle.Kind == HandleKind.MethodSpecification)
        {
            MethodSpecification specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
            if (specification.Method.Kind is not (HandleKind.MethodDefinition or HandleKind.MemberReference) || specification.Method.IsNil)
                throw new BadImageFormatException("MethodSpec must refer to a MethodDef or MemberRef.");
            ResolvedMethod definition = Method(assembly, specification.Method, context, declaredOnly, budget);
            SignatureList<TypeSignature> arguments = new(decoder.MethodArguments(specification.Signature).Select(a => InContext(a, context)));
            if (arguments.Count == 0 || arguments.Count != definition.Signature.GenericArity)
                throw new BadImageFormatException("MethodSpec argument count does not match its declaration.");
            // Decode again before simultaneous substitution. Substituting into an already
            // instantiated signature could capture !!n variables belonging to the caller.
            MethodSignature open = MethodDefinitionSignature(definition.DeclaringType.Definition.Assembly, definition.Handle);
            return definition with { MethodArguments = arguments,
                Signature = Substitute(open, definition.DeclaringType.Arguments, arguments) };
        }
        if (handle.Kind == HandleKind.MethodDefinition)
        {
            var token = (MethodDefinitionHandle)handle;
            MethodDefinition method = reader.GetMethodDefinition(token);
            ConstructedType owner = InContext(OpenType(new ResolvedType(assembly, method.GetDeclaringType())), context);
            MethodSignature signature = MethodDefinitionSignature(assembly, token);
            var arguments = Variables(signature.GenericArity, true);
            return new(owner, token, arguments, Substitute(signature, owner.Arguments, arguments));
        }
        if (handle.Kind != HandleKind.MemberReference)
            throw new UnsupportedBindingException("Method resolution requires MethodDef, MemberRef or MethodSpec.");
        MemberReference member = reader.GetMemberReference((MemberReferenceHandle)handle);
        if (member.GetKind() != MemberReferenceKind.Method) throw new BadImageFormatException("Expected a method MemberRef.");
        string name = reader.GetString(member.Name);
        MethodSignature wanted = decoder.Method(member.Signature);
        ConstructedType actual = Owner(assembly, member.Parent, context);
        if (budget is not null) InspectOwner(actual.Definition, budget);
        string requestedOwner = Describe(actual.Definition);
        ConstructedType lookup = OpenType(actual.Definition);
        var methodArguments = Variables(wanted.GenericArity, true);
        // II.9.8: compare the uninstantiated member signature. C<int>.M(!0)
        // must not collapse into the distinct declaration C<int>.M(int32).
        wanted = Substitute(wanted, lookup.Arguments, methodArguments);
        var visited = new HashSet<ResolvedType>();
        while (true)
        {
            Visit(lookup, visited);
            MetadataReader target = lookup.Definition.Assembly.Image.Reader;
            var matches = new List<MethodDefinitionHandle>();
            foreach (MethodDefinitionHandle candidate in target.GetTypeDefinition(lookup.Definition.Handle).GetMethods())
            {
                InspectMember(budget);
                MethodDefinition definition = target.GetMethodDefinition(candidate);
                if (!target.StringComparer.Equals(definition.Name, name)) continue;
                MethodSignature signature = MethodDefinitionSignature(lookup.Definition.Assembly, candidate);
                if (signature.GenericArity != wanted.GenericArity) continue;
                if (Substitute(signature, lookup.Arguments, methodArguments) == wanted) matches.Add(candidate);
            }
            if (matches.Count > 1) throw Failure(DiagnosticIds.MemberConflict, $"Method '{name}' has multiple matching declarations.");
            if (matches.Count == 1)
            {
                MethodSignature signature = MethodDefinitionSignature(actual.Definition.Assembly, matches[0]);
                return new(actual, matches[0], methodArguments, Substitute(signature, actual.Arguments, methodArguments));
            }
            if (name is ".ctor" or ".cctor" || !BaseTypes(lookup, actual, out var nextLookup, out var nextActual))
                throw Failure(DiagnosticIds.MemberNotFound, $"No declaration matches method '{name}' on {requestedOwner} and its structural signature.");
            if (declaredOnly)
                throw new UnsupportedBindingException("Inherited MemberRef owners require access/initialization analysis before executable lowering; reference the declaring type explicitly.");
            lookup = nextLookup;
            actual = nextActual;
        }
    }

    private ResolvedField Field(ResolvedAssembly assembly, EntityHandle handle, BindingContext? context = null, bool declaredOnly = false, MemberResolutionBudget? budget = null)
    {
        InspectMember(budget);
        MetadataReader reader = assembly.Image.Reader;
        var decoder = new SignatureDecoder(this, assembly);
        if (handle.Kind == HandleKind.FieldDefinition)
        {
            var token = (FieldDefinitionHandle)handle;
            FieldDefinition field = reader.GetFieldDefinition(token);
            ConstructedType owner = InContext(OpenType(new ResolvedType(assembly, field.GetDeclaringType())), context);
            return new(owner, token, Substitute(decoder.Field(field.Signature), owner.Arguments, SignatureList<TypeSignature>.Empty));
        }
        if (handle.Kind != HandleKind.MemberReference)
            throw new UnsupportedBindingException("Field resolution requires FieldDef or MemberRef.");
        MemberReference member = reader.GetMemberReference((MemberReferenceHandle)handle);
        if (member.GetKind() != MemberReferenceKind.Field) throw new BadImageFormatException("Expected a field MemberRef.");
        string name = reader.GetString(member.Name);
        TypeSignature wanted = decoder.Field(member.Signature);
        ConstructedType actual = Owner(assembly, member.Parent, context);
        if (budget is not null) InspectOwner(actual.Definition, budget);
        string requestedOwner = Describe(actual.Definition);
        ConstructedType lookup = OpenType(actual.Definition);
        var empty = SignatureList<TypeSignature>.Empty;
        wanted = Substitute(wanted, lookup.Arguments, empty);
        var visited = new HashSet<ResolvedType>();
        while (true)
        {
            Visit(lookup, visited);
            MetadataReader target = lookup.Definition.Assembly.Image.Reader;
            var matches = new List<FieldDefinitionHandle>();
            foreach (FieldDefinitionHandle candidate in target.GetTypeDefinition(lookup.Definition.Handle).GetFields())
            {
                InspectMember(budget);
                FieldDefinition field = target.GetFieldDefinition(candidate);
                if (!target.StringComparer.Equals(field.Name, name)) continue;
                TypeSignature signature = new SignatureDecoder(this, lookup.Definition.Assembly).Field(field.Signature);
                if (Substitute(signature, lookup.Arguments, empty) == wanted) matches.Add(candidate);
            }
            if (matches.Count > 1) throw Failure(DiagnosticIds.MemberConflict, $"Field '{name}' has multiple matching declarations.");
            if (matches.Count == 1)
            {
                TypeSignature signature = new SignatureDecoder(this, actual.Definition.Assembly).Field(target.GetFieldDefinition(matches[0]).Signature);
                return new(actual, matches[0], Substitute(signature, actual.Arguments, empty));
            }
            if (!BaseTypes(lookup, actual, out var nextLookup, out var nextActual))
                throw Failure(DiagnosticIds.MemberNotFound, $"No declaration matches field '{name}' on {requestedOwner} and its structural signature.");
            if (declaredOnly)
                throw new UnsupportedBindingException("Inherited field owners require access/initialization analysis; reference the declaring type explicitly.");
            lookup = nextLookup;
            actual = nextActual;
        }
    }

    private static void InspectOwner(ResolvedType type, MemberResolutionBudget budget)
    {
        var visited = new HashSet<TypeDefinitionHandle>();
        for (var current = type.Handle; !current.IsNil;)
        {
            InspectMember(budget);
            if (!visited.Add(current)) throw Failure(DiagnosticIds.InvalidSignature, "Declaring-type chain is cyclic.");
            if (visited.Count > TypeReadLimits.MaximumSupportedOwnerDepth)
                throw Failure(DiagnosticIds.TranslationLimit, "Member owner exceeds the 64-level formatting limit.");
            current = type.Assembly.Image.Reader.GetTypeDefinition(current).GetDeclaringType();
        }
    }

    private static void InspectMember(MemberResolutionBudget? budget)
    {
        if (budget is not null && !budget.TryConsume(1))
            throw Failure(DiagnosticIds.TranslationLimit, "Member binding exceeds the caller's metadata work budget.");
    }

    private MethodSignature MethodDefinitionSignature(ResolvedAssembly assembly, MethodDefinitionHandle handle)
    {
        MethodDefinition definition = assembly.Image.Reader.GetMethodDefinition(handle);
        MethodSignature signature = new SignatureDecoder(this, assembly).Method(definition.Signature);
        if (signature.GenericArity != definition.GetGenericParameters().Count)
            throw new BadImageFormatException("Method signature arity disagrees with its generic parameter rows.");
        bool isStatic = (definition.Attributes & System.Reflection.MethodAttributes.Static) != 0;
        if (isStatic == ((signature.Header & 0x20) != 0))
            throw new BadImageFormatException("Method signature instance flag disagrees with its attributes.");
        return signature;
    }

    private ConstructedType Owner(ResolvedAssembly assembly, EntityHandle handle, BindingContext? context = null)
    {
        if (handle.IsNil || handle.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
            throw new UnsupportedBindingException("Member parent must be a TypeDef, TypeRef or named generic TypeSpec; global and vararg members are unsupported.");
        TypeSignature signature = new SignatureDecoder(this, assembly).Type(handle);
        return handle.Kind == HandleKind.TypeSpecification
            ? Owner(InContext(signature, context)) : InContext(Owner(signature), context);
    }

    private static ConstructedType Owner(TypeSignature signature) => signature switch
    {
        TypeSignature.Named named => OpenType(named.Definition),
        TypeSignature.Generic generic => new(generic.Definition.Definition, generic.Arguments),
        _ => throw new UnsupportedBindingException("Array, pointer and other intrinsic member owners require an explicit runtime binding profile."),
    };

    private static ConstructedType OpenType(ResolvedType definition) => new(definition,
        Variables(definition.Assembly.Image.Reader.GetTypeDefinition(definition.Handle).GetGenericParameters().Count, false));

    private bool BaseTypes(ConstructedType lookup, ConstructedType actual, out ConstructedType nextLookup, out ConstructedType nextActual)
    {
        nextLookup = nextActual = lookup;
        EntityHandle parent = lookup.Definition.Assembly.Image.Reader.GetTypeDefinition(lookup.Definition.Handle).BaseType;
        if (parent.IsNil) return false;
        TypeSignature signature = new SignatureDecoder(this, lookup.Definition.Assembly).Type(parent);
        nextLookup = Owner(Substitute(signature, lookup.Arguments, SignatureList<TypeSignature>.Empty));
        nextActual = Owner(Substitute(signature, actual.Arguments, SignatureList<TypeSignature>.Empty));
        return true;
    }

    private static void Visit(ConstructedType type, HashSet<ResolvedType> visited)
    {
        if (!visited.Add(type.Definition) || visited.Count > 64)
            throw Failure(DiagnosticIds.TypeResolutionCycle, "Base-type chain is cyclic or exceeds 64 levels.");
    }

    private static SignatureList<TypeSignature> Variables(int count, bool method)
    {
        if (count > 4096) throw new BadImageFormatException("Generic arity exceeds 4096.");
        return new(Enumerable.Range(0, count).Select(i => (TypeSignature)new TypeSignature.Variable(method, i)));
    }

    private static BindingFailureException Failure(string id, string message) =>
        new([new Diagnostic(id, DiagnosticSeverity.Error, message)]);

    private static string Describe(ResolvedType definition) =>
        $"TypeDef 0x{MetadataTokens.GetToken(definition.Handle):x8} in {definition.Assembly.Identity.DisplayName}";

    private static MethodSignature Substitute(MethodSignature signature, SignatureList<TypeSignature> types,
        SignatureList<TypeSignature> methods) => new(signature.Header, signature.GenericArity,
            Substitute(signature.ReturnType, types, methods), new(signature.Parameters.Select(p => Substitute(p, types, methods))));

    private static TypeSignature Substitute(TypeSignature signature, SignatureList<TypeSignature> types,
        SignatureList<TypeSignature> methods, int depth = 0)
    {
        if (depth > 64) throw new BadImageFormatException("Instantiated signature exceeds 64 levels.");
        TypeSignature Recur(TypeSignature type) => Substitute(type, types, methods, depth + 1);
        TypeSignature result = signature switch
        {
            TypeSignature.Variable variable => Argument(variable.IsMethod ? methods : types, variable.Index),
            TypeSignature.Generic generic => generic with { Arguments = new(generic.Arguments.Select(Recur)) },
            TypeSignature.Vector vector => vector with { Element = Recur(vector.Element) },
            TypeSignature.Array array => array with { Element = Recur(array.Element) },
            TypeSignature.Pointer pointer => pointer with { Element = Recur(pointer.Element) },
            TypeSignature.ByReference reference => reference with { Element = Recur(reference.Element) },
            TypeSignature.Pinned pinned => pinned with { Element = Recur(pinned.Element) },
            TypeSignature.Modified modified => modified with { Element = Recur(modified.Element) },
            TypeSignature.FunctionPointer pointer => pointer with { Signature = pointer.Signature with
                { ReturnType = Recur(pointer.Signature.ReturnType), Parameters = new(pointer.Signature.Parameters.Select(Recur)) } },
            _ => signature,
        };
        CheckSize(result);
        return result;
    }

    private static TypeSignature Argument(SignatureList<TypeSignature> arguments, int index) =>
        index < arguments.Count ? arguments[index] : throw new BadImageFormatException("Generic variable index exceeds its declaration's arity.");

    private static void CheckSize(TypeSignature signature)
    {
        var pending = new Stack<(TypeSignature Type, int Depth)>();
        pending.Push((signature, 0));
        int count = 0;
        while (pending.TryPop(out var item))
        {
            if (++count > 4096 || item.Depth > 64) throw new BadImageFormatException("Instantiated signature exceeds the 64-level or 4096-node limit.");
            void Add(TypeSignature type) => pending.Push((type, item.Depth + 1));
            switch (item.Type)
            {
                case TypeSignature.Generic generic: foreach (var argument in generic.Arguments) Add(argument); break;
                case TypeSignature.Vector vector: Add(vector.Element); break;
                case TypeSignature.Array array: Add(array.Element); break;
                case TypeSignature.Pointer pointer: Add(pointer.Element); break;
                case TypeSignature.ByReference reference: Add(reference.Element); break;
                case TypeSignature.Pinned pinned: Add(pinned.Element); break;
                case TypeSignature.Modified modified: Add(modified.Element); break;
                case TypeSignature.FunctionPointer pointer:
                    Add(pointer.Signature.ReturnType);
                    foreach (var parameter in pointer.Signature.Parameters) Add(parameter);
                    break;
            }
        }
    }
}
