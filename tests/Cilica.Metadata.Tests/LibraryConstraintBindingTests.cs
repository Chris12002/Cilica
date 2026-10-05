using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryConstraintBindingTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly ResolverFiles _files = new();
        internal readonly AssemblyResolver Resolver;
        internal readonly ResolvedAssembly Source, Target;
        internal readonly List<LibraryTypeMapping> Mappings = [];
        internal Fixture(bool method, string shape = "same")
        {
            byte[] Image(string name, bool source)
            {
                var b = new ResolutionAssemblyBuilder(new(name, new Version(1, 0, 0, 0)));
                if (source) b.MarkReferenceAssembly(); else b.Type("", "Padding");
                var left = b.Type("Example", "ILeft`1", arity: 1, attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
                var right = b.Type("Example", "IRight`1", arity: 1, attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface);
                b.Type("Example", "Box`1", arity: 1);
                b.Method("M", [0x10, 1, 0, 8], arity: 1, emit: source ? null : il => { il.LoadConstantI4(17); il.OpCode(ILOpCode.Ret); });
                // Method row 1 precedes all three generic TypeDefs in sorted GenericParam rows.
                var parameter = MetadataTokens.GenericParameterHandle(method ? 1 : 4);
                byte l = checked((byte)(MetadataTokens.GetRowNumber(left) << 2));
                byte r = checked((byte)(MetadataTokens.GetRowNumber(right) << 2));
                EntityHandle first = b.Specification(0x15, 0x12, l, 1, 0x13, 0);
                EntityHandle second = b.Specification(0x15, 0x12, r, 1, method ? (byte)0x1e : (byte)0x13, 0);
                if (!source)
                {
                    if (shape == "argument") first = b.Specification(0x15, 0x12, l, 1, 8);
                    if (shape == "void") first = b.Specification(0x15, 0x12, l, 1, 1);
                    if (shape == "variable") first = b.Specification(0x15, 0x12, l, 1, 0x13, 1);
                    if (shape == "method-variable") first = b.Specification(0x15, 0x12, l, 1, 0x1e, method ? (byte)1 : (byte)0);
                    if (shape == "arity") first = b.Specification(0x15, 0x12, l, 2, 8, 8);
                    if (shape == "pointer") first = b.Specification(0x0f, 8);
                    if (shape == "open") first = left;
                    if (shape == "duplicate") second = first;
                    if (shape == "reordered") (first, second) = (second, first);
                }
                if (shape == "duplicate-both") second = first;
                if (source || shape != "removed") b.GenericConstraint(parameter, first);
                b.GenericConstraint(parameter, second);
                if (!source && shape == "added") b.GenericConstraint(parameter, b.Specification(0x15, 0x12, l, 1, 10));
                return b.Build();
            }
            Resolver = _files.Open();
            Source = Resolver.OpenInput(_files.Write("source.dll", Image("Contract", true))).Value!;
            Target = Resolver.OpenInput(_files.Write("target.dll", Image("Implementation", false))).Value!;
            foreach (string name in new[] { "ILeft`1", "IRight`1", "Box`1" })
                if (shape != "unmapped" || name != "IRight`1")
                    Mappings.Add(new(Resolver.ResolveType(Source, "Example", name).Value!, Resolver.ResolveType(Target, "Example", name).Value!));
        }
        public void Dispose() { Resolver.Dispose(); _files.Dispose(); }
    }

    [Theory]
    [InlineData(false, "same")] [InlineData(true, "same")]
    [InlineData(false, "reordered")] [InlineData(true, "reordered")]
    public void Open_constraints_match_after_identity_projection_independently_of_row_order(bool method, string shape)
    {
        using var f = new Fixture(method, shape);
        var plan = f.Resolver.CreateLibraryBindingPlan(f.Mappings);
        Assert.True(plan.Success, string.Join("; ", plan.Diagnostics));
        var mapped = f.Resolver.ResolveLibraryMethod(plan.Value!, f.Source, MetadataTokens.MethodDefinitionHandle(1));
        Assert.True(mapped.Success, string.Join("; ", mapped.Diagnostics));
        Assert.Equal(f.Target, mapped.Value!.Implementation.DeclaringType.Definition.Assembly);
        using var scope = f.Resolver.CreateLibraryCompilationScope(plan.Value!);
        Assert.Equal(mapped.Value.Implementation, scope.ResolveDeclaredMethod(f.Source, MetadataTokens.MethodDefinitionHandle(1)).Value);
        Assert.Equal(f.Source, f.Resolver.ResolveDeclaredMethod(f.Source, MetadataTokens.MethodDefinitionHandle(1)).Value!.DeclaringType.Definition.Assembly);
    }

    [Theory]
    [InlineData(false, "removed")] [InlineData(true, "removed")]
    [InlineData(false, "argument")] [InlineData(true, "argument")]
    [InlineData(false, "variable")] [InlineData(true, "variable")]
    [InlineData(false, "method-variable")] [InlineData(true, "method-variable")]
    [InlineData(false, "duplicate")] [InlineData(true, "duplicate")]
    [InlineData(false, "arity")] [InlineData(true, "arity")]
    [InlineData(false, "pointer")] [InlineData(true, "pointer")]
    [InlineData(false, "open")] [InlineData(true, "open")]
    [InlineData(false, "unmapped")] [InlineData(true, "unmapped")]
    [InlineData(false, "added")] [InlineData(true, "added")]
    [InlineData(false, "void")] [InlineData(true, "void")]
    [InlineData(false, "duplicate-both")] [InlineData(true, "duplicate-both")]
    public void Changed_or_malformed_obligations_cannot_enter_a_mapping(bool method, string shape)
    {
        using var f = new Fixture(method, shape);
        var plan = f.Resolver.CreateLibraryBindingPlan(f.Mappings);
        string diagnostic = shape == "unmapped" ? DiagnosticIds.LibraryTypeUnmapped
            : method && shape is "arity" or "void" ? DiagnosticIds.InvalidSignature : DiagnosticIds.LibraryBindingInvalid;
        if (method)
        {
            Assert.True(plan.Success, string.Join("; ", plan.Diagnostics));
            var result = f.Resolver.ResolveLibraryMethod(plan.Value!, f.Source, MetadataTokens.MethodDefinitionHandle(1));
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == diagnostic);
        }
        else { Assert.False(plan.Success); Assert.Contains(plan.Diagnostics, d => d.Id == diagnostic); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Constraint_projection_consumes_the_existing_shared_budget(bool method)
    {
        using var f = new Fixture(method); var budget = new MemberResolutionBudget();
        var plan = f.Resolver.CreateLibraryBindingPlan(f.Mappings, budget); Assert.True(plan.Success);
        if (method)
        {
            budget = new();
            Assert.True(f.Resolver.ResolveLibraryMethod(plan.Value!, f.Source, MetadataTokens.MethodDefinitionHandle(1), budget).Success);
            var result = f.Resolver.ResolveLibraryMethod(plan.Value!, f.Source, MetadataTokens.MethodDefinitionHandle(1), new(65536 - budget.Remaining - 1));
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
        }
        else
        {
            var result = f.Resolver.CreateLibraryBindingPlan(f.Mappings, new(65536 - budget.Remaining - 1));
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
        }
    }
}
