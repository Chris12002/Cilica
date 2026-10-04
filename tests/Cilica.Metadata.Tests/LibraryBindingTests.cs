using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryBindingTests
{
    private static AssemblyIdentity Identity(string name) => new(name, new Version(1, 0, 0, 0));
    private static byte[] Image(string name, bool reference, Action<ResolutionAssemblyBuilder>? types = null)
    {
        var builder = new ResolutionAssemblyBuilder(Identity(name));
        if (reference) builder.MarkReferenceAssembly();
        if (types is null) builder.Type("Example", "Api"); else types(builder);
        return builder.Build();
    }
    private static ResolvedType Type(AssemblyResolver resolver, ResolvedAssembly assembly, params string[] names)
    {
        var result = resolver.ResolveType(assembly, "Example", names);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }
    private static T Success<T>(ResolutionResult<T> result) where T : class
    {
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }
    private static void Error<T>(ResolutionResult<T> result, string id = DiagnosticIds.LibraryBindingInvalid) where T : class
    {
        Assert.Null(result.Value); Assert.Equal(id, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Explicit_mapping_preserves_strict_resolution_and_retains_exact_detached_evidence()
    {
        using var files = new ResolverFiles();
        string sourcePath = files.Write("contract.dll", Image("Contract", true));
        string targetPath = files.Write("runtime.dll", Image("Cilica.Core", false, b => { b.Type("", "Padding"); b.Type("Example", "Api"); }));
        var facade = new ResolutionAssemblyBuilder(Identity("Facade"));
        facade.Forward("Example", "Api", facade.Reference(Identity("Contract")));
        files.Write("facade.dll", facade.Build());
        var caller = new ResolutionAssemblyBuilder(Identity("Application"));
        var reference = caller.TypeReference("Example", "Api", caller.Reference(Identity("Facade")));
        caller.Type("Example", "Api"); // unrelated user namesake
        using var resolver = files.Open("refs");
        var source = resolver.OpenInput(sourcePath).Value!; var target = resolver.OpenInput(targetPath).Value!;
        var input = resolver.OpenInput(files.Write("app.dll", caller.Build(), "input")).Value!;
        var contract = Type(resolver, source, "Api"); var implementation = Type(resolver, target, "Api");
        var plan = Success(resolver.CreateLibraryBindingPlan([new(contract, implementation)]));
        Assert.Equal(contract, Success(resolver.ResolveType(input, reference)));
        Assert.Equal(implementation, Success(resolver.ResolveLibraryType(plan, input, reference)));
        Assert.Equal(implementation, Success(resolver.ResolveLibraryType(plan, source, contract.Handle)));
        var user = Type(resolver, input, "Api");
        Assert.Equal(user, Success(resolver.ResolveLibraryType(plan, input, user.Handle)));
        Assert.Equal(contract, Success(resolver.ResolveType(input, reference))); // no session mutation
        var evidence = Assert.Single(plan.Bindings);
        Assert.Equal(source.Identity, evidence.Contract.Assembly);
        Assert.Equal(source.ContentHash, evidence.Contract.ContentHash);
        Assert.Equal(target.ContentHash, evidence.Implementation.ContentHash);
        Assert.NotEqual(evidence.Contract.Token, evidence.Implementation.Token);
        Assert.Equal(new[] { "Api" }, evidence.Names);
        Assert.DoesNotContain(files.Root, JsonSerializer.Serialize(plan.Bindings), StringComparison.Ordinal);
    }

    [Fact]
    public void Closed_generic_vectors_modifiers_and_function_pointers_preserve_signature_structure()
    {
        using var files = new ResolverFiles();
        Action<ResolutionAssemblyBuilder> shape = b => { b.Type("Example", "Box`1", arity: 1); b.Type("Example", "Modifier"); };
        string sourcePath = files.Write("contract.dll", Image("Contract", true, shape));
        string targetPath = files.Write("runtime.dll", Image("Cilica.Core", false, shape));
        var caller = new ResolutionAssemblyBuilder(Identity("Application"));
        var scope = caller.Reference(Identity("Contract"));
        caller.TypeReference("Example", "Box`1", scope); caller.TypeReference("Example", "Modifier", scope);
        var vector = caller.Specification(0x1d, 0x15, 0x12, 5, 1, 8); // Box<int>[]
        var array = caller.Specification(0x14, 0x12, 9, 2, 1, 3, 1, 0); // Modifier[rank=2,size=3,lower=0]
        var modifier = caller.Specification(0x1f, 9, 0x0f, 0x12, 9); // modreq(Modifier) Modifier*
        var pointer = caller.Specification(0x1b, 0, 1, 0x12, 9, 0x10, 0x12, 9); // Modifier (Modifier&)
        var open = caller.Specification(0x15, 0x12, 5, 1, 0x13, 0); // Box<!0>
        using var resolver = files.Open("refs");
        var source = resolver.OpenInput(sourcePath).Value!; var target = resolver.OpenInput(targetPath).Value!;
        var input = resolver.OpenInput(files.Write("app.dll", caller.Build(), "input")).Value!;
        var box = Type(resolver, target, "Box`1"); var marker = Type(resolver, target, "Modifier");
        var plan = Success(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Box`1"), box), new(Type(resolver, source, "Modifier"), marker)]));
        var mapped = Assert.IsType<TypeSignature.Vector>(Success(resolver.ResolveLibrarySignatureType(plan, input, vector, new(4))));
        var instance = Assert.IsType<TypeSignature.Generic>(mapped.Element);
        Assert.Equal(box, instance.Definition.Definition);
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), Assert.Single(instance.Arguments));
        Error(resolver.ResolveLibrarySignatureType(plan, input, vector, new(3)), DiagnosticIds.TranslationLimit);
        var matrix = Assert.IsType<TypeSignature.Array>(Success(resolver.ResolveLibrarySignatureType(plan, input, array)));
        Assert.Equal(marker, Assert.IsType<TypeSignature.Named>(matrix.Element).Definition);
        Assert.Equal(2, matrix.Rank); Assert.Equal(3, Assert.Single(matrix.Sizes)); Assert.Equal(0, Assert.Single(matrix.LowerBounds));
        var modified = Assert.IsType<TypeSignature.Modified>(Success(resolver.ResolveLibrarySignatureType(plan, input, modifier)));
        Assert.True(modified.Required); Assert.Equal(marker, modified.Modifier.Definition);
        Assert.Equal(marker, Assert.IsType<TypeSignature.Named>(Assert.IsType<TypeSignature.Pointer>(modified.Element).Element).Definition);
        var function = Assert.IsType<TypeSignature.FunctionPointer>(Success(resolver.ResolveLibrarySignatureType(plan, input, pointer)));
        Assert.Equal(marker, Assert.IsType<TypeSignature.Named>(function.Signature.ReturnType).Definition);
        Assert.Equal(marker, Assert.IsType<TypeSignature.Named>(Assert.IsType<TypeSignature.ByReference>(Assert.Single(function.Signature.Parameters)).Element).Definition);
        var generic = Assert.IsType<TypeSignature.Generic>(Success(resolver.ResolveLibrarySignatureType(plan, input, open)));
        Assert.Equal(new TypeSignature.Variable(false, 0), Assert.Single(generic.Arguments));
    }

    [Theory]
    [InlineData("namespace")]
    [InlineData("name")]
    [InlineData("interface")]
    [InlineData("sealed")]
    [InlineData("visibility")]
    [InlineData("arity")]
    [InlineData("constraints")]
    public void Incompatible_type_headers_are_rejected(string change)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true, b => b.Type("Example", "Api", arity: 1)))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false, b => b.Type(
            change == "namespace" ? "Other" : "Example", change == "name" ? "Other" : "Api",
            arity: change == "arity" ? 0 : 1,
            attributes: change switch { "interface" => TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
                "sealed" => TypeAttributes.Public | TypeAttributes.Sealed, "visibility" => TypeAttributes.NotPublic, _ => TypeAttributes.Public },
            genericAttributes: change == "constraints" ? GenericParameterAttributes.ReferenceTypeConstraint : GenericParameterAttributes.None)))).Value!;
        var destination = Success(resolver.ResolveType(target, MetadataTokens.TypeDefinitionHandle(2)));
        Error(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), destination)]));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Only_reference_to_implementation_roles_are_admitted(bool sourceReference, bool targetReference)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", sourceReference))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", targetReference))).Value!;
        Error(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), Type(resolver, target, "Api"))]));
    }

    [Fact]
    public void Base_and_nested_owner_correspondence_require_their_own_mappings()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        Action<ResolutionAssemblyBuilder> shape = b =>
        {
            var parent = b.Type("Example", "Parent"); b.Type("", "Child", parent: parent);
            b.Type("Example", "Derived", baseType: parent);
        };
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true, shape))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false, shape))).Value!;
        var parent = new LibraryTypeMapping(Type(resolver, source, "Parent"), Type(resolver, target, "Parent"));
        var child = new LibraryTypeMapping(Type(resolver, source, "Parent", "Child"), Type(resolver, target, "Parent", "Child"));
        var derived = new LibraryTypeMapping(Type(resolver, source, "Derived"), Type(resolver, target, "Derived"));
        Error(resolver.CreateLibraryBindingPlan([child]), DiagnosticIds.LibraryTypeUnmapped);
        Error(resolver.CreateLibraryBindingPlan([derived]), DiagnosticIds.LibraryTypeUnmapped);
        var plan = Success(resolver.CreateLibraryBindingPlan([derived, child, parent]));
        Assert.Equal(3, plan.Bindings.Length);
        Assert.Equal(new[] { "Parent", "Child" }, plan.Bindings[1].Names);
    }

    [Fact]
    public void Changed_base_is_rejected_even_when_headers_match()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true, b =>
        { var parent = b.Type("Example", "Parent"); b.Type("Example", "Child", baseType: parent); }))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false, b =>
        { b.Type("Example", "Parent"); b.Type("Example", "Child"); }))).Value!;
        Error(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Parent"), Type(resolver, target, "Parent")),
            new(Type(resolver, source, "Child"), Type(resolver, target, "Child"))]));
    }

    [Fact]
    public void Nominal_constraints_remain_an_explicit_unsupported_obligation()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        Action<ResolutionAssemblyBuilder> shape = b =>
        {
            var parent = b.Type("Example", "Parent"); b.Type("Example", "Api`1", arity: 1);
            b.GenericConstraint(MetadataTokens.GenericParameterHandle(1), parent);
        };
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true, shape))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false, shape))).Value!;
        var result = resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Parent"), Type(resolver, target, "Parent")),
            new(Type(resolver, source, "Api`1"), Type(resolver, target, "Api`1"))]);
        Error(result);
        Assert.Contains("Nominal/dependent", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unmapped_selected_types_and_substituted_image_bytes_fail_closed()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true, b =>
        { b.Type("Example", "Api"); b.Type("Example", "Missing"); }))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false))).Value!;
        var changed = resolver.OpenInput(files.Write("changed.dll", Image("Contract", true), "other")).Value!;
        var plan = Success(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), Type(resolver, target, "Api"))]));
        Error(resolver.ResolveLibraryType(plan, source, Type(resolver, source, "Missing").Handle), DiagnosticIds.LibraryTypeUnmapped);
        Error(resolver.ResolveLibraryType(plan, changed, Type(resolver, changed, "Api").Handle));
        var changedTarget = resolver.OpenInput(files.Write("changed-target.dll", Image("Cilica.Core", false, b =>
        { b.Type("Example", "Api"); b.Type("", "Extra"); }), "other")).Value!;
        Error(resolver.ResolveLibraryType(plan, changedTarget, Type(resolver, changedTarget, "Api").Handle));
        Error(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), Type(resolver, target, "Api")),
            new(Type(resolver, changed, "Api"), Type(resolver, target, "Api"))]));
    }

    [Fact]
    public void Opening_mapping_images_does_not_add_reference_authority_and_versions_stay_exact()
    {
        using var files = new ResolverFiles();
        string sourcePath = files.Write("contract.dll", Image("Contract", true));
        string targetPath = files.Write("runtime.dll", Image("Cilica.Core", false));
        var caller = new ResolutionAssemblyBuilder(Identity("Application"));
        var reference = caller.TypeReference("Example", "Api", caller.Reference(Identity("Contract")));
        var wrongVersion = caller.TypeReference("Example", "Api", caller.Reference(new("Contract", new Version(2, 0, 0, 0))));
        string inputPath = files.Write("app.dll", caller.Build(), "input");
        foreach (bool catalogued in new[] { false, true })
        {
            using var resolver = catalogued ? files.Open("refs") : files.Open();
            var source = resolver.OpenInput(sourcePath).Value!; var target = resolver.OpenInput(targetPath).Value!;
            var input = resolver.OpenInput(inputPath).Value!;
            var plan = Success(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), Type(resolver, target, "Api"))]));
            if (catalogued) Assert.Equal(Type(resolver, target, "Api"), Success(resolver.ResolveLibraryType(plan, input, reference)));
            else Error(resolver.ResolveLibraryType(plan, input, reference), DiagnosticIds.AssemblyNotFound);
            Error(resolver.ResolveLibraryType(plan, input, wrongVersion), DiagnosticIds.AssemblyNotFound);
        }
    }

    [Fact]
    public void Catalog_conflicts_cannot_be_overridden_by_explicit_mapping_selection()
    {
        using var files = new ResolverFiles();
        string sourcePath = files.Write("contract.dll", Image("Contract", true));
        files.Write("runtime.dll", Image("Cilica.Core", false));
        string changedPath = files.Write("changed.dll", Image("Cilica.Core", false, b =>
        { b.Type("Example", "Api"); b.Type("", "Extra"); }), "other");
        foreach (bool includeConflict in new[] { false, true })
        {
            using var resolver = includeConflict ? files.Open("refs", "other") : files.Open("refs");
            var source = resolver.OpenInput(sourcePath).Value!; var target = resolver.OpenInput(changedPath).Value!;
            Error(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), Type(resolver, target, "Api"))]),
                includeConflict ? DiagnosticIds.AssemblyConflict : DiagnosticIds.LibraryBindingInvalid);
        }
    }

    [Fact]
    public void Changed_pinned_input_is_rejected_even_when_its_reference_resolves_to_an_approved_definition()
    {
        using var files = new ResolverFiles();
        string sourcePath = files.Write("contract.dll", Image("Contract", true));
        string targetPath = files.Write("runtime.dll", Image("Cilica.Core", false));
        var changed = new ResolutionAssemblyBuilder(Identity("Cilica.Core"));
        var reference = changed.TypeReference("Example", "Api", changed.Reference(Identity("Contract")));
        var signature = changed.Specification(0x1d, 0x12, 5);
        using var resolver = files.Open("refs");
        var source = resolver.OpenInput(sourcePath).Value!; var target = resolver.OpenInput(targetPath).Value!;
        var input = resolver.OpenInput(files.Write("changed.dll", changed.Build(), "other")).Value!;
        var plan = Success(resolver.CreateLibraryBindingPlan([new(Type(resolver, source, "Api"), Type(resolver, target, "Api"))]));
        Assert.True(resolver.ResolveType(input, reference).Success);
        Error(resolver.ResolveLibraryType(plan, input, reference));
        Error(resolver.ResolveLibrarySignatureType(plan, input, signature));
    }

    [Fact]
    public void Conflicting_targets_fail_while_duplicate_pairs_and_alias_contracts_are_deterministic()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true))).Value!;
        var alias = resolver.OpenInput(files.Write("alias.dll", Image("Alias", true))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false))).Value!;
        var other = resolver.OpenInput(files.Write("other.dll", Image("Other", false))).Value!;
        var first = new LibraryTypeMapping(Type(resolver, source, "Api"), Type(resolver, target, "Api"));
        var second = new LibraryTypeMapping(Type(resolver, alias, "Api"), first.Implementation);
        Error(resolver.CreateLibraryBindingPlan([first, new(first.Contract, Type(resolver, other, "Api"))]));
        string json = JsonSerializer.Serialize(Success(resolver.CreateLibraryBindingPlan([first, second, first])).Bindings);
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(json, JsonSerializer.Serialize(Success(resolver.CreateLibraryBindingPlan([second, first])).Bindings));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void Plans_enforce_session_lifetime_and_keep_detached_evidence_after_disposal()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open(); using var other = files.Open();
        string sourcePath = files.Write("contract.dll", Image("Contract", true));
        var source = resolver.OpenInput(sourcePath).Value!;
        var foreign = other.OpenInput(sourcePath).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false))).Value!;
        var pair = new LibraryTypeMapping(Type(resolver, source, "Api"), Type(resolver, target, "Api"));
        Assert.Throws<ArgumentException>(() => resolver.CreateLibraryBindingPlan([new(Type(other, foreign, "Api"), pair.Implementation)]));
        var plan = Success(resolver.CreateLibraryBindingPlan([pair]));
        string evidence = JsonSerializer.Serialize(plan.Bindings);
        Assert.Throws<ArgumentException>(() => other.ResolveLibraryType(plan, foreign, pair.Contract.Handle));
        Assert.Throws<ArgumentException>(() => resolver.ResolveLibraryType(plan, foreign, pair.Contract.Handle));
        resolver.Dispose();
        Assert.Equal(evidence, JsonSerializer.Serialize(plan.Bindings));
        Assert.Throws<ObjectDisposedException>(() => resolver.ResolveLibraryType(plan, source, pair.Contract.Handle));
    }

    [Fact]
    public void Work_limits_reject_partial_plans_and_count_duplicate_selections()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var source = resolver.OpenInput(files.Write("contract.dll", Image("Contract", true))).Value!;
        var target = resolver.OpenInput(files.Write("runtime.dll", Image("Cilica.Core", false))).Value!;
        var pair = new LibraryTypeMapping(Type(resolver, source, "Api"), Type(resolver, target, "Api"));
        Assert.Single(Success(resolver.CreateLibraryBindingPlan([pair], new(3))).Bindings);
        Error(resolver.CreateLibraryBindingPlan([pair], new(2)), DiagnosticIds.TranslationLimit);
        Error(resolver.CreateLibraryBindingPlan(Enumerable.Repeat(pair, LibraryBindingPlan.MaximumMappings + 1)), DiagnosticIds.TranslationLimit);
        Assert.Single(Success(resolver.CreateLibraryBindingPlan(Enumerable.Repeat(pair, LibraryBindingPlan.MaximumMappings))).Bindings);
        Error(resolver.CreateLibraryBindingPlan([]));
    }
}
