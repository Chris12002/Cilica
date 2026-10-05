using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ByRefLikeAdmissionTests
{
    [Theory]
    [InlineData("Copy")] [InlineData("GenericCopy")]
    public void Actual_stack_only_values_are_rejected_before_layout(string name)
    {
        var result = IntegerObjectTranslationTests.Translate(name, fixture: typeof(ByRefLikeProbe));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("ByRefLike", StringComparison.Ordinal));
    }

    [Fact]
    public void Actual_runtime_permission_flag_accepts_an_ordinary_value_argument()
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var equatable = resolver.ResolveType(library, "System", "IEquatable`1").Value!;
        var input = resolver.ReadClosedTypeInput(new(equatable, new([new TypeSignature.Primitive(PrimitiveTypeCode.Int32)])), library, new());
        Assert.True(input.Success, string.Join("; ", input.Diagnostics));
        Assert.Equal(GenericParameterAttributes.AllowByRefLike, Assert.Single(input.Value!.ClosedParameterAttributes));
    }

    [Theory]
    [InlineData(0x20, false, true)] [InlineData(0x20, true, true)]
    [InlineData(0x28, false, true)] [InlineData(0x28, true, false)]
    [InlineData(0x40, false, false)] [InlineData(0x21, false, false)]
    public void Permission_preserves_constraints_and_unknown_flags_stay_rejected(int flags, bool reference, bool succeeds)
    {
        var b = new ResolutionAssemblyBuilder(new("Permission", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var generic = b.Type("Test", "Container`1", baseType: obj, arity: 1, genericAttributes: (GenericParameterAttributes)flags);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("Permission.dll", b.Build())).Value!;
        var result = resolver.ReadClosedTypeInput(new(resolver.ResolveType(image, generic).Value!,
            new([new TypeSignature.Primitive(reference ? PrimitiveTypeCode.Object : PrimitiveTypeCode.Int32)])), image, new());
        Assert.Equal(succeeds, result.Success);
    }

    [Theory]
    [InlineData("named")] [InlineData("vector")] [InlineData("generic")]
    public void Stack_only_arguments_cannot_hide_in_unused_nested_specializations(string shape)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var input = resolver.OpenInput(typeof(ByRefLikeProbe).Assembly.Location).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var value = resolver.ResolveType(input, "Cilica.Fixtures.Execution", "StackOnlyValue").Value!;
        Assert.True(resolver.ReadTypeInput(input, value.Handle).Value!.IsByRefLike);
        TypeSignature argument = new TypeSignature.Named(value, SignatureTypeKind.ValueType);
        if (shape == "vector") argument = new TypeSignature.Vector(argument);
        if (shape == "generic") argument = new TypeSignature.Generic(new(resolver.ResolveType(input, "Cilica.Fixtures.Execution", "StackOnlyGeneric`1").Value!, SignatureTypeKind.ValueType),
            new([new TypeSignature.Primitive(PrimitiveTypeCode.Int32)]));
        var equatable = resolver.ResolveType(library, "System", "IEquatable`1").Value!;
        var result = resolver.ReadClosedTypeInput(new(equatable, new([argument])), library, new());
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("ByRefLike", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void Mapping_cannot_add_or_remove_the_stack_only_marker(bool sourceMarked, bool embedded)
    {
        byte[] Image(string name, bool contract, bool marker)
        {
            var b = new ResolutionAssemblyBuilder(new(name, new Version(1, 0, 0, 0))); if (contract) b.MarkReferenceAssembly();
            var type = b.Type("Test", "Value");
            if (marker)
            {
                EntityHandle ctor;
                if (embedded)
                {
                    b.Type("System.Runtime.CompilerServices", "IsByRefLikeAttribute");
                    ctor = b.Method(".ctor", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                }
                else
                {
                    var attribute = b.TypeReference("System.Runtime.CompilerServices", "IsByRefLikeAttribute", b.Reference(new("Marker", new Version(1, 0, 0, 0))));
                    ctor = b.Member(attribute, ".ctor", 0x20, 0, 1);
                }
                b.CustomAttribute(type, ctor, 1, 0, 0, 0);
            }
            return b.Build();
        }
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var left = resolver.OpenInput(files.Write("Contract.dll", Image("Contract", true, sourceMarked))).Value!;
        var right = resolver.OpenInput(files.Write("Library.dll", Image("Library", false, !sourceMarked))).Value!;
        var result = resolver.CreateLibraryBindingPlan([new(resolver.ResolveType(left, "Test", "Value").Value!, resolver.ResolveType(right, "Test", "Value").Value!)]);
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.LibraryBindingInvalid, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Method_permission_is_admitted_for_ordinary_arguments_but_not_stack_only_arguments()
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("MethodPermission", new Version(1, 0, 0, 0)));
        b.Type("Test", "Program");
        var keep = b.Method("Keep", [0x10, 1, 0, 8], arity: 1, genericAttributes: GenericParameterAttributes.AllowByRefLike,
            emit: il => { il.LoadConstantI4(7); il.OpCode(ILOpCode.Ret); });
        var call = b.Instantiate(keep, 0x0a, 1, 8);
        var image = resolver.OpenInput(files.Write("MethodPermission.dll", b.Build())).Value!;
        var method = resolver.ResolveDeclaredMethod(image, call).Value!;
        Assert.True(resolver.ReadClosedMethodInput(method, library, new()).Success);
        var fixture = resolver.OpenInput(typeof(ByRefLikeProbe).Assembly.Location).Value!;
        var stackOnly = resolver.ResolveType(fixture, "Cilica.Fixtures.Execution", "StackOnlyValue").Value!;
        var rejected = method with { MethodArguments = new([new TypeSignature.Named(stackOnly, SignatureTypeKind.ValueType)]) };
        var result = resolver.ReadClosedMethodInput(rejected, library, new());
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("ByRefLike", StringComparison.Ordinal));
    }

    [Fact]
    public void Marker_inspection_consumes_the_type_snapshot_member_budget()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("MarkerBudget", new Version(1, 0, 0, 0)));
        var value = b.Type("Test", "Value");
        var marker = b.TypeReference("System.Runtime.CompilerServices", "IsByRefLikeAttribute", b.Reference(new("Marker", new Version(1, 0, 0, 0))));
        b.CustomAttribute(value, b.Member(marker, ".ctor", 0x20, 0, 1), 1, 0, 0, 0);
        var image = resolver.OpenInput(files.Write("MarkerBudget.dll", b.Build())).Value!;
        var full = resolver.ReadTypeInput(image, value).Value!; Assert.True(full.IsByRefLike);
        var shortRead = resolver.ReadTypeInput(image, value, new(MaximumMembers: full.MetadataMembersRead - 1));
        Assert.False(shortRead.Success); Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(shortRead.Diagnostics).Id);
    }
}
