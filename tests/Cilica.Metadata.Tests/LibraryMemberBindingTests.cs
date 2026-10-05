using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryMemberBindingTests
{
    private static AssemblyIdentity Id(string name) => new(name, new Version(1, 0, 0, 0));
    private static T Success<T>(ResolutionResult<T> result) where T : class
    { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Value!; }
    private static void Error<T>(ResolutionResult<T> result, string code) where T : class
    { Assert.Null(result.Value); Assert.Equal(code, Assert.Single(result.Diagnostics).Id); }

    private sealed class Fixture : IDisposable
    {
        internal readonly ResolverFiles Files = new();
        internal readonly AssemblyResolver Resolver;
        internal readonly ResolvedAssembly Contract, Target;
        internal readonly LibraryBindingPlan Plan;
        internal Fixture(Action<ResolutionAssemblyBuilder> source, Action<ResolutionAssemblyBuilder>? target = null)
        {
            var a = new ResolutionAssemblyBuilder(Id("Contract")); a.MarkReferenceAssembly(); source(a);
            var b = new ResolutionAssemblyBuilder(Id("Implementation")); (target ?? source)(b);
            string left = Files.Write("contract.dll", a.Build()), right = Files.Write("target.dll", b.Build());
            Resolver = Files.Open("refs");
            Contract = Success(Resolver.OpenInput(left)); Target = Success(Resolver.OpenInput(right));
            Plan = Success(Resolver.CreateLibraryBindingPlan([new(
                Success(Resolver.ResolveType(Contract, "Example", "Box`1")), Success(Resolver.ResolveType(Target, "Example", "Box`1")))]));
        }
        internal (ResolvedAssembly Assembly, EntityHandle Method, EntityHandle Field, EntityHandle Generic) Caller()
        {
            var b = new ResolutionAssemblyBuilder(Id("Caller"));
            var box = b.TypeReference("Example", "Box`1", b.Reference(Id("Contract")));
            var closed = b.Specification(0x15, 0x12, 5, 1, 8);
            var method = b.Member(closed, "M", 0, 1, 8, 0x13, 0);
            var field = b.Member(closed, "Value", 6, 0x13, 0);
            var generic = b.Member(closed, "Map", 0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0);
            return (Success(Resolver.OpenInput(Files.Write("caller.dll", b.Build(), "input"))), method, field, generic);
        }
        public void Dispose() { Resolver.Dispose(); Files.Dispose(); }
    }

    private static void Shape(ResolutionAssemblyBuilder b)
    {
        b.Type("Example", "Box`1", arity: 1);
        b.Field("Value", 6, 0x13, 0);
        b.Field("Value", 6, 8);
        b.Method("M", [0, 1, 8, 0x13, 0]);
        b.Method("M", [0, 1, 8, 8]);
        b.Method("Map", [0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0], arity: 1);
    }

    [Fact]
    public void Declaration_matches_share_a_budget_but_not_closed_arguments_plans_or_fresh_ledgers()
    {
        using var f = new Fixture(Shape);
        var b = new ResolutionAssemblyBuilder(Id("ManyClosures"));
        b.TypeReference("Example", "Box`1", b.Reference(Id("Contract")));
        var first = b.Specification(0x15, 0x12, 5, 1, 8);
        var second = b.Specification(0x15, 0x12, 5, 1, 10);
        var m1 = b.Member(first, "Map", 0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0);
        var m2 = b.Member(second, "Map", 0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0);
        var one = b.Instantiate(m1, 0x0a, 1, 8); var two = b.Instantiate(m2, 0x0a, 1, 10);
        var input = Success(f.Resolver.OpenInput(f.Files.Write("many.dll", b.Build(), "input")));
        var budget = new MemberResolutionBudget(10000);
        int before = budget.Remaining;
        _ = Success(f.Resolver.ResolveLibraryMethod(f.Plan, input, one, budget));
        int cold = before - budget.Remaining; before = budget.Remaining;
        _ = Success(f.Resolver.ResolveLibraryMethod(f.Plan, input, one, budget));
        int warm = before - budget.Remaining;
        Assert.True(warm > 0 && warm < cold);
        for (int i = 0; i < 100; ++i)
        {
            var binding = Success(f.Resolver.ResolveLibraryMethod(f.Plan, input, i % 2 == 0 ? one : two, budget));
            var expected = new TypeSignature.Primitive(i % 2 == 0 ? PrimitiveTypeCode.Int32 : PrimitiveTypeCode.Int64);
            Assert.Equal(expected, binding.Implementation.Signature.ReturnType);
            Assert.All(binding.Implementation.Signature.Parameters, p => Assert.Equal(expected, p));
            Assert.Equal(expected, Assert.Single(binding.Implementation.DeclaringType.Arguments));
            Assert.Equal(expected, Assert.Single(binding.Implementation.MethodArguments));
        }
        Error(f.Resolver.ResolveLibraryMethod(f.Plan, input, one, new(warm)), DiagnosticIds.TranslationLimit);
        var shortBudget = new MemberResolutionBudget(cold - 1);
        Error(f.Resolver.ResolveLibraryMethod(f.Plan, input, one, shortBudget), DiagnosticIds.TranslationLimit);
        Error(f.Resolver.ResolveLibraryMethod(f.Plan, input, one, shortBudget), DiagnosticIds.TranslationLimit);
        var other = new ResolutionAssemblyBuilder(Id("OtherImplementation")); other.Type("Example", "Box`1", arity: 1);
        other.Method("Different", [0, 0, 1]);
        var image = Success(f.Resolver.OpenInput(f.Files.Write("other.dll", other.Build())));
        var plan = Success(f.Resolver.CreateLibraryBindingPlan([new(Success(f.Resolver.ResolveType(f.Contract, "Example", "Box`1")),
            Success(f.Resolver.ResolveType(image, "Example", "Box`1")))]));
        Error(f.Resolver.ResolveLibraryMethod(plan, input, one, budget), DiagnosticIds.MemberNotFound);
    }

    [Fact]
    public void Generic_declarations_are_matched_before_closing_overloads()
    {
        using var f = new Fixture(Shape); var caller = f.Caller();
        var method = Success(f.Resolver.ResolveLibraryMethod(f.Plan, caller.Assembly, caller.Method));
        Assert.Equal(f.Contract.ContentHash, method.Contract.DeclaringType.Definition.Assembly.ContentHash);
        Assert.Equal(f.Target.ContentHash, method.Implementation.DeclaringType.Definition.Assembly.ContentHash);
        Assert.Equal(MetadataTokens.MethodDefinitionHandle(1), method.Implementation.Handle);
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), Assert.Single(method.Implementation.Signature.Parameters));
        var field = Success(f.Resolver.ResolveLibraryField(f.Plan, caller.Assembly, caller.Field));
        Assert.Equal(MetadataTokens.FieldDefinitionHandle(1), field.Implementation.Handle);
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), field.Implementation.Signature);
        Assert.Equal(f.Contract, Success(f.Resolver.ResolveDeclaredMethod(caller.Assembly, caller.Method)).DeclaringType.Definition.Assembly);
        Assert.Equal(f.Contract, Success(f.Resolver.ResolveDeclaredField(caller.Assembly, caller.Field)).DeclaringType.Definition.Assembly);
        using var scope = f.Resolver.CreateLibraryCompilationScope(f.Plan);
        Assert.Equal(method.Implementation, Success(scope.ResolveDeclaredMethod(caller.Assembly, caller.Method)));
        Assert.Equal(field.Implementation, Success(scope.ResolveDeclaredField(caller.Assembly, caller.Field)));
    }

    [Fact]
    public void Method_specs_keep_owner_and_method_arguments_in_separate_environments()
    {
        using var f = new Fixture(Shape);
        var b = new ResolutionAssemblyBuilder(Id("Caller"));
        b.TypeReference("Example", "Box`1", b.Reference(Id("Contract")));
        var closed = b.Specification(0x15, 0x12, 5, 1, 8);
        var method = b.Member(closed, "Map", 0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0);
        var spec = b.Instantiate(method, 0x0a, 1, 0x1d, 8);
        var input = Success(f.Resolver.OpenInput(f.Files.Write("spec.dll", b.Build(), "input")));
        var mapped = Success(f.Resolver.ResolveLibraryMethod(f.Plan, input, spec)).Implementation;
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), mapped.Signature.Parameters[0]);
        Assert.Equal(new TypeSignature.Vector(new TypeSignature.Primitive(PrimitiveTypeCode.Int32)), mapped.Signature.Parameters[1]);
        Assert.Equal(mapped.Signature.Parameters[1], mapped.Signature.ReturnType);
        Assert.Equal(mapped.Signature.ReturnType, Assert.Single(mapped.MethodArguments));
        using var scope = f.Resolver.CreateLibraryCompilationScope(f.Plan);
        Assert.Equal(mapped, Success(scope.ResolveDeclaredMethod(input, spec)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_implementation_members_do_not_fall_back_to_reference_stubs(bool method)
    {
        using var f = new Fixture(Shape, b => b.Type("Example", "Box`1", arity: 1)); var caller = f.Caller();
        if (method) Error(f.Resolver.ResolveLibraryMethod(f.Plan, caller.Assembly, caller.Method), DiagnosticIds.MemberNotFound);
        else Error(f.Resolver.ResolveLibraryField(f.Plan, caller.Assembly, caller.Field), DiagnosticIds.MemberNotFound);
    }

    [Fact]
    public void Compilation_view_substitutes_closed_caller_arguments_into_mapped_member_owners()
    {
        using var f = new Fixture(Shape);
        var b = new ResolutionAssemblyBuilder(Id("GenericCaller"));
        b.TypeReference("Example", "Box`1", b.Reference(Id("Contract")));
        var box = b.Specification(0x15, 0x12, 5, 1, 0x1e, 0);
        var method = b.Member(box, "M", 0, 1, 8, 0x13, 0);
        var field = b.Member(box, "Value", 6, 0x13, 0);
        b.Type("Example", "Program");
        var run = b.Method("Run", [0x10, 1, 1, 8, 0x1e, 0], arity: 1);
        var spec = b.Instantiate(run, 0x0a, 1, 8);
        using var scope = f.Resolver.CreateLibraryCompilationScope(f.Plan);
        var input = Success(scope.OpenInput(f.Files.Write("generic-caller.dll", b.Build(), "input")));
        var caller = Success(scope.ResolveDeclaredMethod(input, spec));
        var target = Success(scope.ResolveClosedCall(caller, method, new()));
        var storage = Success(scope.ResolveClosedField(caller, field, new()));
        Assert.Equal(f.Target, target.DeclaringType.Definition.Assembly);
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), Assert.Single(target.DeclaringType.Arguments));
        Assert.Equal(Assert.Single(target.DeclaringType.Arguments), storage.Signature);
        Assert.Equal(storage.DeclaringType, target.DeclaringType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Duplicate_implementation_declarations_are_ambiguous(bool method)
    {
        using var f = new Fixture(Shape, b =>
        {
            Shape(b);
            if (method) b.Method("M", [0, 1, 8, 0x13, 0]); else b.Field("Value", 6, 0x13, 0);
        });
        var caller = f.Caller();
        if (method) Error(f.Resolver.ResolveLibraryMethod(f.Plan, caller.Assembly, caller.Method), DiagnosticIds.MemberConflict);
        else Error(f.Resolver.ResolveLibraryField(f.Plan, caller.Assembly, caller.Field), DiagnosticIds.MemberConflict);
    }

    [Theory]
    [InlineData(MethodAttributes.Private | MethodAttributes.Static)]
    [InlineData(MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName)]
    public void Mapped_method_attributes_must_preserve_the_contract(MethodAttributes attributes)
    {
        using var f = new Fixture(Shape, b => { b.Type("Example", "Box`1", arity: 1); b.Method("M", [0, 1, 8, 0x13, 0], attributes: attributes); });
        var caller = f.Caller();
        Error(f.Resolver.ResolveLibraryMethod(f.Plan, caller.Assembly, caller.Method), DiagnosticIds.LibraryBindingInvalid);
    }

    [Theory]
    [InlineData(FieldAttributes.Private)]
    [InlineData(FieldAttributes.Public | FieldAttributes.Static)]
    [InlineData(FieldAttributes.Public | FieldAttributes.InitOnly)]
    public void Mapped_field_attributes_must_preserve_access_storage_and_mutability(FieldAttributes attributes)
    {
        using var f = new Fixture(Shape, b => { b.Type("Example", "Box`1", arity: 1); b.FieldWithAttributes("Value", attributes, 6, 0x13, 0); });
        var caller = f.Caller();
        Error(f.Resolver.ResolveLibraryField(f.Plan, caller.Assembly, caller.Field), DiagnosticIds.LibraryBindingInvalid);
    }

    [Fact]
    public void Definition_tokens_and_unselected_application_owners_preserve_their_scope()
    {
        using var f = new Fixture(Shape);
        var mapped = Success(f.Resolver.ResolveLibraryMethod(f.Plan, f.Contract, MetadataTokens.MethodDefinitionHandle(1)));
        Assert.Equal(f.Target, mapped.Implementation.DeclaringType.Definition.Assembly);
        var b = new ResolutionAssemblyBuilder(Id("App")); b.Type("Example", "Box`1", arity: 1);
        var method = b.Method("M", [0, 1, 8, 0x13, 0]);
        var field = b.Field("Value", 6, 0x13, 0);
        var input = Success(f.Resolver.OpenInput(f.Files.Write("app.dll", b.Build(), "input")));
        Assert.Equal(input, Success(f.Resolver.ResolveLibraryMethod(f.Plan, input, method)).Implementation.DeclaringType.Definition.Assembly);
        Assert.Equal(input, Success(f.Resolver.ResolveLibraryField(f.Plan, input, field)).Implementation.DeclaringType.Definition.Assembly);
    }

    [Fact]
    public void Work_budgets_and_plan_ownership_are_enforced()
    {
        using var f = new Fixture(Shape); var caller = f.Caller();
        Error(f.Resolver.ResolveLibraryMethod(f.Plan, caller.Assembly, caller.Method, new(1)), DiagnosticIds.TranslationLimit);
        Error(f.Resolver.ResolveLibraryField(f.Plan, caller.Assembly, caller.Field, new(1)), DiagnosticIds.TranslationLimit);
        using var other = f.Files.Open();
        Assert.Throws<ArgumentException>(() => other.ResolveLibraryMethod(f.Plan, caller.Assembly, caller.Method));
        f.Resolver.Dispose();
        Assert.Throws<ObjectDisposedException>(() => f.Resolver.ResolveLibraryField(f.Plan, caller.Assembly, caller.Field));
    }

    [Theory]
    [InlineData(42, true)]
    [InlineData(43, false)]
    [InlineData(null, false)]
    public void Literal_values_must_exist_and_match(int? targetValue, bool succeeds)
    {
        static void Shape(ResolutionAssemblyBuilder b, int? value)
        {
            b.Type("Example", "Box`1", arity: 1);
            var field = b.FieldWithAttributes("Answer", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal, 6, 8);
            if (value.HasValue) b.Constant(field, value.Value);
        }
        using var f = new Fixture(b => Shape(b, 42), b => Shape(b, targetValue));
        var result = f.Resolver.ResolveLibraryField(f.Plan, f.Contract, MetadataTokens.FieldDefinitionHandle(1));
        if (succeeds) Assert.Equal(f.Target, Success(result).Implementation.DeclaringType.Definition.Assembly);
        else Error(result, DiagnosticIds.LibraryBindingInvalid);
    }

    [Fact]
    public void Named_types_inside_declarations_are_projected_consistently()
    {
        static void Shape(ResolutionAssemblyBuilder b)
        {
            b.Type("Example", "Box`1", arity: 1);
            // The owner itself, represented as a named type in both declarations.
            b.Method("Self", [0, 1, 0x12, 8, 0x12, 8]);
            b.Field("Self", 6, 0x12, 8);
        }
        using var f = new Fixture(Shape);
        var method = Success(f.Resolver.ResolveLibraryMethod(f.Plan, f.Contract, MetadataTokens.MethodDefinitionHandle(1)));
        var field = Success(f.Resolver.ResolveLibraryField(f.Plan, f.Contract, MetadataTokens.FieldDefinitionHandle(1)));
        var expected = new TypeSignature.Named(Success(f.Resolver.ResolveType(f.Target, "Example", "Box`1")), SignatureTypeKind.Class);
        Assert.Equal(expected, method.Implementation.Signature.ReturnType);
        Assert.Equal(expected, Assert.Single(method.Implementation.Signature.Parameters));
        Assert.Equal(expected, field.Implementation.Signature);
        Assert.NotEqual(method.Contract.Signature.ReturnType, method.Implementation.Signature.ReturnType);
    }

    [Fact]
    public void Open_generic_definitions_in_method_constraints_are_rejected_even_when_both_images_match()
    {
        using var f = new Fixture(b =>
        {
            var owner = b.Type("Example", "Box`1", arity: 1);
            b.Method("M", [0x10, 1, 0, 1], arity: 1);
            b.GenericConstraint(MetadataTokens.GenericParameterHandle(1), owner);
        });
        // MethodDef row 1 sorts before TypeDef row 2 in the GenericParam table.
        Error(f.Resolver.ResolveLibraryMethod(f.Plan, f.Contract, MetadataTokens.MethodDefinitionHandle(1)), DiagnosticIds.LibraryBindingInvalid);
    }
}
