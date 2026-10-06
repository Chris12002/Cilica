using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class MemberResolutionTests
{
    private static ResolutionAssemblyBuilder Builder() => new(new AssemblyIdentity("Members", new Version(1, 0, 0, 0)));
    private static ResolvedAssembly Open(ResolutionAssemblyBuilder builder, ResolverFiles files, AssemblyResolver resolver) =>
        Assert.IsType<ResolvedAssembly>(resolver.OpenInput(files.Write("Members.dll", builder.Build())).Value);
    private static TypeSignature.Primitive Int32 => new(PrimitiveTypeCode.Int32);
    private static TypeSignature.Primitive String => new(PrimitiveTypeCode.String);

    [Fact]
    public void Generic_overloads_are_matched_before_instantiation()
    {
        var builder = Builder();
        builder.Type("", "C`1", arity: 1); // TypeDef 2 => coded token 8.
        var generic = builder.Method("M", [0, 1, 1, 0x13, 0]);
        var concrete = builder.Method("M", [0, 1, 1, 8]);
        var genericField = builder.Field("F", 6, 0x13, 0);
        var concreteField = builder.Field("F", 6, 8);
        var owner = builder.Specification(0x15, 0x12, 8, 1, 8);
        var genericRef = builder.Member(owner, "M", 0, 1, 1, 0x13, 0);
        var concreteRef = builder.Member(owner, "M", 0, 1, 1, 8);
        var genericFieldRef = builder.Member(owner, "F", 6, 0x13, 0);
        var concreteFieldRef = builder.Member(owner, "F", 6, 8);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        var first = Assert.IsType<ResolvedMethod>(resolver.ResolveMethod(assembly, genericRef).Value);
        var second = Assert.IsType<ResolvedMethod>(resolver.ResolveMethod(assembly, concreteRef).Value);
        Assert.Equal(generic, first.Handle);
        Assert.Equal(concrete, second.Handle);
        Assert.Equal(first.Signature, second.Signature);
        Assert.Equal(Int32, first.Signature.Parameters[0]);
        Assert.Equal(genericField, resolver.ResolveField(assembly, genericFieldRef).Value!.Handle);
        Assert.Equal(concreteField, resolver.ResolveField(assembly, concreteFieldRef).Value!.Handle);
        Assert.Equal(Int32, resolver.ResolveField(assembly, genericFieldRef).Value!.Signature);
    }

    [Fact]
    public void MethodSpec_does_not_capture_caller_method_variables_in_owner_arguments()
    {
        var builder = Builder();
        builder.Type("", "C`1", arity: 1);
        builder.Method("M", [0x10, 1, 2, 1, 0x13, 0, 0x1e, 0], arity: 1);
        // In a generic caller: C<!!0>.M<string>(!0, !!0).
        var owner = builder.Specification(0x15, 0x12, 8, 1, 0x1e, 0);
        var member = builder.Member(owner, "M", 0x10, 1, 2, 1, 0x13, 0, 0x1e, 0);
        var instance = builder.Instantiate(member, 0x0a, 1, 0x0e);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var result = resolver.ResolveMethod(Open(builder, files, resolver), instance);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(new TypeSignature.Variable(true, 0), result.Value!.Signature.Parameters[0]);
        Assert.Equal(String, result.Value.Signature.Parameters[1]);
    }

    [Fact]
    public void Inherited_members_substitute_the_base_type_arguments()
    {
        var builder = Builder();
        var parent = builder.Type("", "Base`1", arity: 1);
        var method = builder.Method("M", [0, 1, 1, 0x13, 0]);
        var field = builder.Field("F", 6, 0x13, 0);
        var baseSpec = builder.Specification(0x15, 0x12, 8, 1, 0x1d, 0x13, 0);
        builder.Type("", "Derived`1", baseType: baseSpec, arity: 1);
        var owner = builder.Specification(0x15, 0x12, 12, 1, 8);
        var member = builder.Member(owner, "M", 0, 1, 1, 0x1d, 0x13, 0);
        var fieldRef = builder.Member(owner, "F", 6, 0x1d, 0x13, 0);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        var result = resolver.ResolveMethod(assembly, member);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(method, result.Value!.Handle);
        Assert.Equal(parent, result.Value.DeclaringType.Definition.Handle);
        Assert.Equal(new TypeSignature.Vector(Int32), result.Value.Signature.Parameters[0]);
        var resolvedField = resolver.ResolveField(assembly, fieldRef);
        Assert.True(resolvedField.Success, string.Join("; ", resolvedField.Diagnostics));
        Assert.Equal(field, resolvedField.Value!.Handle);
        Assert.Equal(new TypeSignature.Vector(Int32), resolvedField.Value.Signature);
    }

    [Theory]
    [InlineData(".ctor")]
    [InlineData(".cctor")]
    public void Constructors_are_not_inherited(string name)
    {
        var builder = Builder();
        var parent = builder.Type("", "Base");
        builder.Method(name, [0, 0, 1]);
        var child = builder.Type("", "Derived", baseType: parent);
        var member = builder.Member(child, name, 0, 0, 1);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var result = resolver.ResolveMethod(Open(builder, files, resolver), member);
        Assert.Equal(DiagnosticIds.MemberNotFound, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Duplicate_members_are_an_error()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        builder.Method("M", [0, 0, 1]);
        builder.Method("M", [0, 0, 1]);
        builder.Field("F", 6, 8);
        builder.Field("F", 6, 8);
        var member = builder.Member(type, "M", 0, 0, 1);
        var field = builder.Member(type, "F", 6, 8);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        Assert.Equal(DiagnosticIds.MemberConflict, Assert.Single(resolver.ResolveMethod(assembly, member).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.MemberConflict, Assert.Single(resolver.ResolveField(assembly, field).Diagnostics).Id);
    }

    [Fact]
    public void Cyclic_base_types_fail_when_searching_for_a_missing_member()
    {
        var builder = Builder();
        var type = builder.Type("", "C", baseType: MetadataTokens.TypeDefinitionHandle(2));
        var member = builder.Member(type, "M", 0, 0, 1);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        Assert.Equal(DiagnosticIds.TypeResolutionCycle,
            Assert.Single(resolver.ResolveMethod(Open(builder, files, resolver), member).Diagnostics).Id);
    }

    [Theory]
    [InlineData(new byte[] { 0, 1, 1, 8 }, new byte[] { 0, 1, 1, 9 })] // signedness
    [InlineData(new byte[] { 0, 1, 1, 8 }, new byte[] { 0, 1, 1, 0x10, 8 })] // byref
    [InlineData(new byte[] { 0, 1, 1, 0x1d, 8 }, new byte[] { 0, 1, 1, 0x14, 8, 1, 0, 0 })] // SZ vs MD
    [InlineData(new byte[] { 0, 0, 8 }, new byte[] { 0, 0, 9 })] // return type
    [InlineData(new byte[] { 0, 0, 1 }, new byte[] { 0x20, 0, 1 })] // hasthis
    [InlineData(new byte[] { 0, 1, 1, 0x1b, 1, 0, 1 }, new byte[] { 0, 1, 1, 0x1b, 2, 0, 1 })] // function pointer convention
    [InlineData(new byte[] { 0, 1, 1, 0x1f, 8, 8 }, new byte[] { 0, 1, 1, 0x20, 8, 8 })] // required vs optional modifier
    public void Signature_distinctions_prevent_false_matches(byte[] definition, byte[] reference)
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        builder.Method("M", definition);
        var matching = builder.Member(type, "M", definition);
        var member = builder.Member(type, "M", reference);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        Assert.True(resolver.ResolveMethod(assembly, matching).Success);
        Assert.Equal(DiagnosticIds.MemberNotFound,
            Assert.Single(resolver.ResolveMethod(assembly, member).Diagnostics).Id);
    }

    [Theory]
    [InlineData(new byte[] { 0, 1, 1 })] // truncated parameter
    [InlineData(new byte[] { 0, 0, 1, 8 })] // trailing bytes
    [InlineData(new byte[] { 0x80, 0, 1 })] // invalid flags
    [InlineData(new byte[] { 0x40, 0, 1 })] // explicitthis without hasthis
    [InlineData(new byte[] { 0, 1, 1, 0x13, 0 })] // !0 on nongeneric owner
    [InlineData(new byte[] { 0, 1, 1, 0x1e, 0 })] // !!0 on nongeneric method
    [InlineData(new byte[] { 0, 1, 1, 0x1d, 1 })] // void array
    [InlineData(new byte[] { 0, 1, 1, 0x10, 0x10, 8 })] // byref byref
    [InlineData(new byte[] { 0, 1, 1, 0x12, 0 })] // nil coded token
    public void Malformed_signatures_are_diagnostics(byte[] signature)
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var member = builder.Member(type, "M", signature);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        Assert.Equal(DiagnosticIds.InvalidSignature,
            Assert.Single(resolver.ResolveMethod(Open(builder, files, resolver), member).Diagnostics).Id);
    }

    [Theory]
    [InlineData(new byte[] { 0x0a, 0 })]
    [InlineData(new byte[] { 0x0a, 2, 8, 8 })]
    [InlineData(new byte[] { 0x0a, 1, 8, 8 })]
    public void Invalid_MethodSpec_arguments_are_diagnosed(byte[] signature)
    {
        var builder = Builder();
        builder.Type("", "C");
        var method = builder.Method("M", [0x10, 1, 0, 1], arity: 1);
        var instance = builder.Instantiate(method, signature);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        Assert.Equal(DiagnosticIds.InvalidSignature,
            Assert.Single(resolver.ResolveMethod(Open(builder, files, resolver), instance).Diagnostics).Id);
    }

    [Fact]
    public void Deep_signature_fails_without_host_stack_exhaustion()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var member = builder.Member(type, "M", new byte[] { 0, 1, 1 }.Concat(Enumerable.Repeat((byte)0x1d, 20000)).Append((byte)8).ToArray());
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var result = resolver.ResolveMethod(Open(builder, files, resolver), member);
        Assert.Equal(DiagnosticIds.InvalidSignature, Assert.Single(result.Diagnostics).Id);
        Assert.Contains("limit", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void Signature_nesting_limit_has_an_accepted_boundary(int nesting, bool accepted)
    {
        var builder = Builder();
        var specification = builder.Specification(Enumerable.Repeat((byte)0x1d, nesting).Append((byte)8).ToArray());
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var result = resolver.ResolveSignatureType(Open(builder, files, resolver), specification);
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal(DiagnosticIds.InvalidSignature, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Generic_function_pointer_and_recursive_TypeSpec_tokens_are_explicitly_unsupported()
    {
        var builder = Builder();
        var pointer = builder.Specification(0x1b, 0x10, 1, 0, 1);
        var recursive = builder.Specification(0x12, 10); // TypeSpec 2 refers to itself.
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(resolver.ResolveSignatureType(assembly, pointer).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(resolver.ResolveSignatureType(assembly, recursive).Diagnostics).Id);
    }

    [Fact]
    public void Structural_arrays_compare_by_value_and_retain_bounds()
    {
        var builder = Builder();
        var first = builder.Specification(0x14, 8, 2, 1, 3, 2, 0x7f, 0); // int32[ -1...1, 0... ]
        var second = builder.Specification(0x14, 8, 2, 1, 3, 2, 0x7f, 0);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        var one = Assert.IsType<TypeSignature.Array>(resolver.ResolveSignatureType(assembly, first).Value);
        var two = Assert.IsType<TypeSignature.Array>(resolver.ResolveSignatureType(assembly, second).Value);
        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.Equal(new[] { -1, 0 }, one.LowerBounds);
        Assert.Equal(new[] { 3 }, one.Sizes);
    }

    [Fact]
    public void Missing_signature_dependency_retains_the_resolution_diagnostic()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var missing = builder.Reference(new AssemblyIdentity("Missing", new Version(1, 0, 0, 0)));
        builder.TypeReference("", "Absent", missing);
        var member = builder.Member(type, "M", 0, 1, 1, 0x12, 5);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var result = resolver.ResolveMethod(Open(builder, files, resolver), member);
        Assert.Equal(DiagnosticIds.AssemblyNotFound, Assert.Single(result.Diagnostics).Id);
        Assert.Contains("token 0x0a", result.Diagnostics[0].Location, StringComparison.Ordinal);
        Assert.Contains("type reference .Absent", result.Diagnostics[0].Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Varargs_and_array_intrinsic_members_fail_explicitly()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var vararg = builder.Member(type, "M", 5, 0, 1);
        var array = builder.Specification(0x14, 8, 1, 0, 0);
        var intrinsic = builder.Member(array, "Get", 0x20, 1, 8, 8);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(resolver.ResolveMethod(assembly, vararg).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(resolver.ResolveMethod(assembly, intrinsic).Diagnostics).Id);
    }

    [Fact]
    public void Invalid_rows_and_nil_handles_are_diagnostics()
    {
        var builder = Builder();
        builder.Type("", "C");
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        Assert.False(resolver.ResolveMethod(assembly, MetadataTokens.MethodDefinitionHandle(9999)).Success);
        Assert.False(resolver.ResolveMethod(assembly, MetadataTokens.MemberReferenceHandle(9999)).Success);
        Assert.False(resolver.ResolveMethod(assembly, MetadataTokens.MethodSpecificationHandle(9999)).Success);
        Assert.False(resolver.ResolveField(assembly, MetadataTokens.FieldDefinitionHandle(9999)).Success);
        Assert.False(resolver.ResolveSignatureType(assembly, MetadataTokens.TypeSpecificationHandle(9999)).Success);
        Assert.False(resolver.ResolveMethod(assembly, default).Success);
    }

    [Fact]
    public void Seeded_signature_corruption_returns_results_without_host_exceptions()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var members = new List<MemberReferenceHandle>();
        var random = new Random(1709);
        for (int i = 0; i < Fuzz.Iterations; i++)
        {
            var bytes = new byte[random.Next(1, 40)];
            random.NextBytes(bytes);
            if (i % 2 == 0) bytes[0] = 0;
            members.Add(builder.Member(type, "M", bytes));
        }
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        foreach (var member in members)
        {
            var result = resolver.ResolveMethod(assembly, member);
            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
        }
    }

    [Fact]
    public void Same_named_parameter_types_from_different_assemblies_do_not_match()
    {
        var firstId = new AssemblyIdentity("First", new Version(1, 0, 0, 0));
        var secondId = new AssemblyIdentity("Second", new Version(1, 0, 0, 0));
        var first = new ResolutionAssemblyBuilder(firstId);
        first.Type("", "C");
        var method = first.Method("M", [0, 1, 1, 0x12, 12]);
        first.Type("", "X");
        var second = new ResolutionAssemblyBuilder(secondId);
        second.Type("", "X");
        var root = Builder();
        var one = root.Reference(firstId);
        var two = root.Reference(secondId);
        var owner = root.TypeReference("", "C", one);
        root.TypeReference("", "X", one);
        root.TypeReference("", "X", two);
        var matching = root.Member(owner, "M", 0, 1, 1, 0x12, 9);
        var different = root.Member(owner, "M", 0, 1, 1, 0x12, 13);
        using var files = new ResolverFiles();
        files.Write("First.dll", first.Build());
        files.Write("Second.dll", second.Build());
        using var resolver = files.Open("refs");
        var assembly = Open(root, files, resolver);
        var result = resolver.ResolveMethod(assembly, matching);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(firstId, result.Value!.DeclaringType.Definition.Assembly.Identity);
        Assert.Equal(method, result.Value.Handle);
        Assert.Equal(DiagnosticIds.MemberNotFound, Assert.Single(resolver.ResolveMethod(assembly, different).Diagnostics).Id);
    }

    [Fact]
    public void Bare_type_tokens_do_not_invent_a_class_or_value_type_encoding()
    {
        var builder = Builder();
        var definition = builder.Type("", "C");
        var asClass = builder.Specification(0x12, 8);
        var asValueType = builder.Specification(0x11, 8);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        var token = Assert.IsType<TypeSignature.Named>(resolver.ResolveSignatureType(assembly, definition).Value);
        var classSignature = Assert.IsType<TypeSignature.Named>(resolver.ResolveSignatureType(assembly, asClass).Value);
        var valueSignature = Assert.IsType<TypeSignature.Named>(resolver.ResolveSignatureType(assembly, asValueType).Value);
        Assert.Equal(SignatureTypeKind.Unknown, token.Kind);
        Assert.Equal(SignatureTypeKind.Class, classSignature.Kind);
        Assert.Equal(SignatureTypeKind.ValueType, valueSignature.Kind);
        Assert.Equal(token.Definition, classSignature.Definition);
        Assert.Equal(token.Definition, valueSignature.Definition);
        Assert.NotEqual(classSignature, valueSignature);
    }

    [Fact]
    public void Byte_identical_input_and_reference_copies_have_the_same_type_identity()
    {
        var identity = new AssemblyIdentity("Members", new Version(1, 0, 0, 0));
        var builder = Builder();
        var type = builder.Type("", "C");
        var method = builder.Method("M", [0, 1, 1, 0x12, 8]);
        var self = builder.Reference(identity);
        builder.TypeReference("", "C", self);
        var member = builder.Member(type, "M", 0, 1, 1, 0x12, 5);
        byte[] bytes = builder.Build();
        using var files = new ResolverFiles();
        files.Write("copy.dll", bytes);
        string input = files.Write("input.dll", bytes, "root");
        using var resolver = files.Open("refs");
        var assembly = resolver.OpenInput(input).Value!;
        var result = resolver.ResolveMethod(assembly, member);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(method, result.Value!.Handle);
        var inputType = resolver.ResolveType(assembly, type).Value!;
        var copyType = resolver.ResolveType(resolver.ResolveAssembly(identity).Value!, type).Value!;
        Assert.Equal(inputType, copyType);
        Assert.Equal(inputType.GetHashCode(), copyType.GetHashCode());

        var different = Builder();
        different.Type("", "C");
        different.Field("Extra", 6, 8);
        var differentImage = resolver.OpenInput(files.Write("different.dll", different.Build(), "other-root")).Value!;
        Assert.NotEqual(inputType, resolver.ResolveType(differentImage, type).Value);
    }
}
