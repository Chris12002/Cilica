using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ExactInterfaceConstraintTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly AssemblyResolver Resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        internal readonly ResolvedAssembly Library;
        internal Fixture() => Library = Resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        internal TypeSignature Signature(Type type)
        {
            if (type == typeof(int)) return new TypeSignature.Primitive(PrimitiveTypeCode.Int32);
            if (type == typeof(long)) return new TypeSignature.Primitive(PrimitiveTypeCode.Int64);
            if (type == typeof(string)) return new TypeSignature.Primitive(PrimitiveTypeCode.String);
            if (type == typeof(object)) return new TypeSignature.Primitive(PrimitiveTypeCode.Object);
            var original = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            var image = Resolver.OpenInput(original.Assembly.Location).Value!;
            var named = new TypeSignature.Named(Resolver.ResolveType(image, (TypeDefinitionHandle)MetadataTokens.EntityHandle(original.MetadataToken)).Value!,
                original.IsValueType ? SignatureTypeKind.ValueType : SignatureTypeKind.Class);
            return type.IsGenericType ? new TypeSignature.Generic(named, new(Array.ConvertAll(type.GetGenericArguments(), Signature))) : named;
        }
        internal ConstructedType Closed(Type definition, params Type[] arguments)
        {
            var original = definition.IsGenericType ? definition.GetGenericTypeDefinition() : definition;
            var image = Resolver.OpenInput(original.Assembly.Location).Value!;
            return new(Resolver.ResolveType(image, (TypeDefinitionHandle)MetadataTokens.EntityHandle(original.MetadataToken)).Value!,
                new(Array.ConvertAll(arguments, Signature)));
        }
        public void Dispose() => Resolver.Dispose();
    }

    [Theory]
    [InlineData(typeof(ConstraintParent<int>), true)]
    [InlineData(typeof(ConstraintChild<int>), true)]
    [InlineData(typeof(IConstraintChild<int>), true)]
    [InlineData(typeof(ConstraintChild<long>), false)]
    [InlineData(typeof(object), false)]
    public void Exact_inherited_constraints_match_the_CLR(Type argument, bool succeeds)
    {
        using var f = new Fixture();
        if (succeeds) Assert.NotNull(typeof(ConstraintContainer<>).MakeGenericType(argument));
        else Assert.Throws<ArgumentException>(() => typeof(ConstraintContainer<>).MakeGenericType(argument));
        var result = f.Resolver.ReadClosedTypeInput(f.Closed(typeof(ConstraintContainer<>), argument), f.Library, new());
        Assert.Equal(succeeds, result.Success);
        if (succeeds) Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(typeof(RuntimeEquatableValue), true)]
    [InlineData(typeof(int), true)] [InlineData(typeof(long), true)] [InlineData(typeof(string), true)]
    [InlineData(typeof(object), false)]
    public void Self_constraints_substitute_arguments_and_authenticate_runtime_primitives(Type argument, bool succeeds)
    {
        using var f = new Fixture();
        if (succeeds) Assert.NotNull(typeof(SelfConstraintContainer<>).MakeGenericType(argument));
        else Assert.Throws<ArgumentException>(() => typeof(SelfConstraintContainer<>).MakeGenericType(argument));
        var result = f.Resolver.ReadClosedTypeInput(f.Closed(typeof(SelfConstraintContainer<>), argument), f.Library, new());
        Assert.True(result.Success == succeeds, string.Join("; ", result.Diagnostics));
    }

    [Theory]
    [InlineData(typeof(ConstraintChild<int>), true)] [InlineData(typeof(ConstraintChild<long>), false)]
    public void Method_constraints_use_both_owner_and_method_environments(Type argument, bool succeeds)
    {
        using var f = new Fixture(); var owner = f.Closed(typeof(ConstraintHost<>), typeof(int));
        var reflected = typeof(ConstraintHost<int>).GetMethod("Accept")!;
        if (succeeds) Assert.NotNull(reflected.MakeGenericMethod(argument));
        else Assert.Throws<ArgumentException>(() => reflected.MakeGenericMethod(argument));
        var open = f.Resolver.ResolveDeclaredMethod(owner.Definition.Assembly,
            MetadataTokens.EntityHandle(reflected.MetadataToken)).Value!;
        var method = open with { DeclaringType = owner, MethodArguments = new([f.Signature(argument)]) };
        var result = f.Resolver.ReadClosedMethodInput(method, f.Library, new());
        Assert.True(result.Success == succeeds, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Full_ancestry_inspection_is_charged_to_the_shared_snapshot_budget()
    {
        using var f = new Fixture(); var type = f.Closed(typeof(ConstraintContainer<>), typeof(ConstraintChild<int>));
        var full = f.Resolver.ReadClosedTypeInput(type, f.Library, new()); Assert.True(full.Success);
        var limited = f.Resolver.ReadClosedTypeInput(type, f.Library, new(MaximumMembers: full.Value!.MetadataMembersRead - 1));
        Assert.False(limited.Success); Assert.Contains(limited.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }

    [Theory]
    [InlineData(typeof(int), true)] [InlineData(typeof(long), true)]
    [InlineData(typeof(RuntimeEquatableValue), false)] [InlineData(typeof(object), false)]
    public void Unmanaged_proof_is_bounded_to_integer_primitives(Type argument, bool succeeds)
    {
        using var f = new Fixture();
        var result = f.Resolver.ReadClosedTypeInput(f.Closed(typeof(UnmanagedConstraintContainer<>), argument), f.Library, new());
        Assert.True(result.Success == succeeds, string.Join("; ", result.Diagnostics));
    }

    [Theory]
    [InlineData("cycle")] [InlineData("duplicate")] [InlineData("class-edge")]
    [InlineData("base-cycle")] [InlineData("arity")] [InlineData("base-constraint")] [InlineData("dependent")]
    [InlineData("value-interface")] [InlineData("value-constraint")]
    public void Matching_edge_does_not_hide_invalid_remaining_ancestry(string shape)
    {
        var b = new ResolutionAssemblyBuilder(new("MalformedConstraint", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); b.Type("System", "ValueType", baseType: obj);
        var contract = b.Type("Test", "IMark", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        if (shape == "cycle") b.Implements(contract, contract);
        var generic = b.Type("Test", "IGeneric`1", arity: 1, attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var argument = b.Type("Test", "Argument", baseType: shape == "base-cycle" ? MetadataTokens.TypeDefinitionHandle(6) : obj);
        b.Implements(argument, contract);
        if (shape == "duplicate") b.Implements(argument, contract);
        if (shape == "class-edge") b.Implements(argument, obj);
        if (shape == "arity") b.Implements(argument, generic);
        if (shape == "value-interface") b.Implements(argument, b.Specification(0x15, 0x11, 20, 1, 8));
        var container = b.Type("Test", "Container`1", baseType: obj, arity: 1);
        b.GenericConstraint(MetadataTokens.GenericParameterHandle(2), shape == "base-constraint" ? obj
            : shape == "dependent" ? b.Specification(0x13, 0)
            : shape == "value-constraint" ? b.Specification(0x15, 0x11, 20, 1, 8) : contract);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("bad.dll", b.Build())).Value!;
        var result = resolver.ReadClosedTypeInput(new(resolver.ResolveType(image, container).Value!,
            new([new TypeSignature.Named(resolver.ResolveType(image, argument).Value!, SignatureTypeKind.Class)])), image, new());
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Runtime_interface_generic_helper_matches_native(bool optimized) =>
        IntegerObjectTranslationTests.Verify(optimized, [("Equality", InterfaceConstraintProbe.Equality)], typeof(InterfaceConstraintProbe));
}
