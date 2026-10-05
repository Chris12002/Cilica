using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ConstrainedValueTargetTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly AssemblyResolver Resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        internal readonly ResolvedAssembly Input, Library;
        internal Fixture()
        {
            Input = Resolver.OpenInput(typeof(DirectConstrainedValue<>).Assembly.Location).Value!;
            Library = Resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        }
        internal ConstructedType Type(Type type)
        {
            var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            var image = definition.Assembly == typeof(object).Assembly ? Library : Input;
            var resolved = Resolver.ResolveType(image, (TypeDefinitionHandle)MetadataTokens.EntityHandle(definition.MetadataToken)).Value!;
            return new(resolved, new(type.IsGenericType ? type.GetGenericArguments().Select(Signature) : []));
        }
        private TypeSignature Signature(Type type) => type == typeof(int) ? new TypeSignature.Primitive(PrimitiveTypeCode.Int32)
            : type == typeof(long) ? new TypeSignature.Primitive(PrimitiveTypeCode.Int64)
            : type == typeof(object) ? new TypeSignature.Primitive(PrimitiveTypeCode.Object)
            : new TypeSignature.Named(Type(type).Definition, type.IsValueType ? SignatureTypeKind.ValueType : SignatureTypeKind.Class);
        internal ResolvedMethod Method(Type owner, string name)
        {
            var type = Type(owner);
            var method = owner.GetMethod(name)!;
            var definition = Resolver.ResolveDeclaredMethod(type.Definition.Assembly, MetadataTokens.EntityHandle(method.MetadataToken)).Value!;
            TypeSignature Close(TypeSignature signature) => signature is TypeSignature.Variable { IsMethod: false } variable
                ? type.Arguments[variable.Index] : signature;
            return definition with { DeclaringType = type, Signature = definition.Signature with {
                ReturnType = Close(definition.Signature.ReturnType), Parameters = new(definition.Signature.Parameters.Select(Close)) } };
        }
        public void Dispose() => Resolver.Dispose();
    }

    [Theory]
    [InlineData(typeof(DirectConstrainedValue<int>), typeof(IDirectConstrainedValue<int>))]
    [InlineData(typeof(DirectConstrainedValue<long>), typeof(IDirectConstrainedValue<long>))]
    [InlineData(typeof(DirectConstrainedValue<object>), typeof(IDirectConstrainedValue<object>))]
    [InlineData(typeof(ExplicitConstrainedValue), typeof(IDirectConstrainedValue<int>))]
    [InlineData(typeof(RuntimeEquatableValue), typeof(IEquatable<RuntimeEquatableValue>))]
    public void Exact_closed_interface_targets_match_dotnet_maps(Type receiver, Type contract)
    {
        using var f = new Fixture();
        var result = f.Resolver.ResolveConstrainedValueTarget(f.Method(contract, contract.GetGenericTypeDefinition() == typeof(IEquatable<>) ? "Equals" : "Change"), f.Type(receiver), f.Library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var map = receiver.GetInterfaceMap(contract); var expected = Assert.Single(map.TargetMethods);
        Assert.Equal(expected.MetadataToken, MetadataTokens.GetToken(result.Value!.Implementation.Handle));
        Assert.Equal(expected.IsPrivate, result.Value.IsExplicit);
        Assert.Equal(f.Type(receiver), result.Value.Implementation.DeclaringType);
        Assert.Equal(result.Value.Declaration.Signature, result.Value.Implementation.Signature);
    }

    [Theory]
    [InlineData(typeof(DirectConstrainedValue<int>))] [InlineData(typeof(ExplicitConstrainedValue))]
    public void Object_overrides_select_the_exact_value_body(Type receiver)
    {
        using var f = new Fixture();
        var result = f.Resolver.ResolveConstrainedValueTarget(f.Method(typeof(object), "GetHashCode"), f.Type(receiver), f.Library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal(receiver.GetMethod("GetHashCode")!.MetadataToken, MetadataTokens.GetToken(result.Value!.Implementation.Handle));
        Assert.False(result.Value.IsExplicit);
    }

    [Theory]
    [InlineData(typeof(ConstrainedWithoutOverride))] [InlineData(typeof(ConstrainedClass))] [InlineData(typeof(StackOnlyValue))]
    public void Boxing_class_and_stack_only_fallbacks_are_not_mistaken_for_direct_value_targets(Type receiver)
    {
        using var f = new Fixture();
        var result = f.Resolver.ResolveConstrainedValueTarget(f.Method(typeof(object), "GetHashCode"), f.Type(receiver), f.Library);
        Assert.False(result.Success); Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void A_newslot_with_the_same_signature_cannot_impersonate_an_Object_override()
    {
        using var f = new Fixture(); using var files = new ResolverFiles();
        var b = new ResolutionAssemblyBuilder(new("NewSlotValue", new Version(1, 0, 0, 0)));
        var valueBase = b.TypeReference("System", "ValueType", b.Reference(f.Library.Identity));
        var value = b.Type("Example", "Value", baseType: valueBase,
            attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout);
        b.Method("GetHashCode", [0x20, 0, 8], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot,
            emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        var image = f.Resolver.OpenInput(files.Write("NewSlotValue.dll", b.Build())).Value!;
        var receiver = new ConstructedType(f.Resolver.ResolveType(image, value).Value!, SignatureList<TypeSignature>.Empty);
        var result = f.Resolver.ResolveConstrainedValueTarget(f.Method(typeof(object), "GetHashCode"), receiver, f.Library);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("newslot", StringComparison.Ordinal));
    }

    [Fact]
    public void Forged_declarations_wrong_contracts_and_exhausted_budgets_are_rejected()
    {
        using var f = new Fixture();
        var declaration = f.Method(typeof(IDirectConstrainedValue<int>), "Change"); var receiver = f.Type(typeof(DirectConstrainedValue<int>));
        Assert.False(f.Resolver.ResolveConstrainedValueTarget(declaration with { Signature = declaration.Signature with {
            ReturnType = new TypeSignature.Primitive(PrimitiveTypeCode.Int64) } }, receiver, f.Library).Success);
        Assert.False(f.Resolver.ResolveConstrainedValueTarget(f.Method(typeof(IDirectConstrainedValue<long>), "Change"), receiver, f.Library).Success);
        var limited = f.Resolver.ResolveConstrainedValueTarget(declaration, receiver, f.Library, new(1));
        Assert.False(limited.Success); Assert.Contains(limited.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }
}
