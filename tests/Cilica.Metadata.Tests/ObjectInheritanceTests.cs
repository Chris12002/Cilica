using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectInheritanceTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ObjectTypeId Object = new(Hash, 0x02000002), Base = new(Hash, 0x02000003),
        Left = new(Hash, 0x02000004), Deep = new(Hash, 0x02000005), Right = new(new string('b', 64), 0x02000004);

    private class BaseOracle { }
    private class LeftOracle : BaseOracle { }
    private sealed class DeepOracle : LeftOracle { }
    private sealed class RightOracle : BaseOracle { }

    private static ObjectTypeLayout Layout(ObjectTypeId id, ObjectTypeId? parent) =>
        new(new("fixture", id.ContentHash, id.Token, "SameName"), parent, TypeAttributes.Public, []);

    private static ObjectTypeCatalog Catalog() => new([Deep, Right], Object,
        [Layout(Object, null), Layout(Base, Object), Layout(Left, Base), Layout(Deep, Left), Layout(Right, Base)]);

    private static IrStatement.Define Define(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);

    private static IrMethod Method(ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements, int returned) =>
        new(new("fixture", Hash, 0x06000001, "test"), parameters, result, [new(0, 0, [], statements, new IrReturn(1, returned))], []);

    private static ReferenceIrProgram Program(IrMethod method) => new(IrMethodId.From(method.Source), [method], Catalog());

    private static ObjectModelResult Build(ResolutionAssemblyBuilder builder, TypeDefinitionHandle root)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("hierarchy.dll", builder.Build())).Value!;
        return ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, root).Value!, image);
    }

    private static byte[] Reference(EntityHandle type)
    {
        var blob = new BlobBuilder(); blob.WriteByte(0x06); blob.WriteByte(0x12);
        blob.WriteCompressedInteger(MetadataTokens.GetRowNumber(type) * 4 + (type.Kind == HandleKind.TypeReference ? 1 : 0));
        return blob.ToArray();
    }

    [Fact]
    public void Base_closure_keeps_hidden_fields_and_declared_reference_slots_distinct()
    {
        var builder = new ResolutionAssemblyBuilder(new("Hierarchy", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        var parent = builder.Type("Example", "Parent", baseType: rootObject);
        var childHandle = MetadataTokens.TypeDefinitionHandle(4);
        builder.Field("Value", 0x06, 0x08); builder.Field("Link", Reference(childHandle));
        var child = builder.Type("Example", "Child", baseType: parent);
        builder.Field("Value", 0x06, 0x08); builder.Field("Link", Reference(parent));
        var leaf = builder.Type("Example", "Leaf", baseType: child, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        var result = Build(builder, leaf); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var model = result.Model!; ObjectModelValidator.Validate(model);
        Assert.Equal(4, model.Types.Length); Assert.Equal(4, model.Types.Sum(t => t.Fields.Length));
        var parentLayout = model.Types.Single(t => t.Source.Token == MetadataTokens.GetToken(parent));
        var childLayout = model.Types.Single(t => t.Source.Token == MetadataTokens.GetToken(child));
        Assert.Equal(ObjectTypeId.From(parentLayout.Source), childLayout.BaseType);
        Assert.Equal(new[] { "Value", "Link" }, childLayout.Fields.Select(f => f.Name));
        Assert.Equal(new[] { 1 }, parentLayout.ReferenceSlots); Assert.Equal(new[] { 1 }, childLayout.ReferenceSlots);
        Assert.All(parentLayout.Fields, f => Assert.Equal(ObjectTypeId.From(parentLayout.Source), f.Id.Owner));
        Assert.All(childLayout.Fields, f => Assert.Equal(ObjectTypeId.From(childLayout.Source), f.Id.Owner));
        Assert.Empty(model.Types.Single(t => t.Source.Token == MetadataTokens.GetToken(leaf)).Fields);
        Assert.NotEqual(parentLayout.Fields[0].Id, childLayout.Fields[0].Id);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("cycle")]
    [InlineData("sealed")]
    public void Invalid_metadata_bases_return_diagnostics_without_partial_models(string kind)
    {
        var builder = new ResolutionAssemblyBuilder(new("InvalidHierarchy", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        var parent = builder.Type("Example", "Parent", baseType: kind switch
        {
            "self" => MetadataTokens.TypeDefinitionHandle(3), "cycle" => MetadataTokens.TypeDefinitionHandle(4), _ => rootObject,
        }, attributes: TypeAttributes.Public | (kind == "sealed" ? TypeAttributes.Sealed : 0));
        var child = builder.Type("Example", "Child", baseType: parent);
        var result = Build(builder, child); Assert.Null(result.Model);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
        Assert.Contains(kind == "sealed" ? "sealed" : "cycle", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("layout inclusion", diagnostic.Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Base_chains_obey_the_exact_type_limit_without_recursive_traversal(bool excess)
    {
        var builder = new ResolutionAssemblyBuilder(new("DeepHierarchy", new Version(1, 0, 0, 0)));
        var parent = builder.Type("System", "Object");
        for (int i = 1; i < ObjectModelLimits.MaximumTypes + (excess ? 1 : 0); i++)
            parent = builder.Type("Example", "Type" + i.ToString(CultureInfo.InvariantCulture), baseType: parent);
        var result = Build(builder, parent); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        else
        {
            var model = result.Model!; Assert.Equal(ObjectModelLimits.MaximumTypes, model.Types.Length);
            var method = Method([IrType.Reference(model.Entry)], IrType.Reference(model.ObjectBase),
                [Define(0, IrType.Reference(model.Entry), new IrExpression.Parameter(0))], 0);
            ReferenceIrValidator.Validate(new(IrMethodId.From(method.Source), [method], new([model.Entry], model.ObjectBase, model.Types)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inherited_declarations_share_the_field_budget_without_flattening(bool excess)
    {
        var builder = new ResolutionAssemblyBuilder(new("HierarchyFields", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object"); var parent = builder.Type("Example", "Parent", baseType: rootObject);
        for (int i = 0; i < 2048; i++) builder.Field("Base" + i.ToString(CultureInfo.InvariantCulture), 0x06, 0x08);
        var child = builder.Type("Example", "Child", baseType: parent);
        for (int i = 0; i < 2048 + (excess ? 1 : 0); i++) builder.Field("Child" + i.ToString(CultureInfo.InvariantCulture), 0x06, 0x08);
        var result = Build(builder, child); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        else Assert.Equal(4096, result.Model!.Types.Sum(t => t.Fields.Length));
    }

    [Fact]
    public void Unsupported_ancestor_shape_reports_its_base_inclusion_path()
    {
        var builder = new ResolutionAssemblyBuilder(new("AbstractAncestor", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        var parent = builder.Type("Example", "Parent", baseType: rootObject, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var child = builder.Type("Example", "Child", baseType: parent);
        var result = Build(builder, child); Assert.Null(result.Model);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("Parent", diagnostic.Location, StringComparison.Ordinal);
        Assert.Contains("via base class", diagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Valid_deep_graphs_do_not_repeatedly_allocate_diagnostic_names()
    {
        string name = new('x', 1024);
        var ids = Enumerable.Range(2, ObjectModelLimits.MaximumTypes).Select(row => new ObjectTypeId(Hash, 0x02000000 | row)).ToArray();
        var catalog = new ObjectTypeCatalog([ids[^1]], ids[0],
            [.. ids.Select((id, index) => Layout(id, index == 0 ? null : ids[index - 1]) with
                { Source = new("fixture", Hash, id.Token, name) })]);
        ObjectModelValidator.Validate(Catalog()); // Warm ordinary validation paths outside the measurement.
        long before = GC.GetAllocatedBytesForCurrentThread();
        ObjectModelValidator.Validate(catalog);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // Generous resource guard, not a timing benchmark. Eager diagnostic
        // concatenation per ancestor step allocates over 60 MiB for this graph.
        Assert.InRange(allocated, 0, 16 * 1024 * 1024);
    }

    [Fact]
    public void Bases_from_another_image_preserve_identity_and_sealed_leaf_shape()
    {
        var core = new ResolutionAssemblyBuilder(new("CoreHierarchy", new Version(1, 0, 0, 0)));
        var rootObject = core.Type("System", "Object"); core.Type("Example", "SameName", baseType: rootObject);
        var app = new ResolutionAssemblyBuilder(new("AppHierarchy", new Version(1, 0, 0, 0)));
        var parent = app.TypeReference("Example", "SameName", app.Reference(new("CoreHierarchy", new Version(1, 0, 0, 0))));
        var child = app.Type("Example", "SameName", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        using var files = new ResolverFiles(); string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs"); var image = resolver.OpenInput(appPath).Value!;
        var result = ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, child).Value!, resolver.OpenInput(corePath).Value!);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var derived = result.Model!.Types.Single(t => ObjectTypeId.From(t.Source) == result.Model.Entry);
        Assert.NotEqual(derived.Source.ContentHash, derived.BaseType!.ContentHash);
        Assert.Equal(derived.Source.Name, result.Model.Types.Single(t => ObjectTypeId.From(t.Source) == derived.BaseType).Source.Name);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("self")]
    [InlineData("cycle")]
    [InlineData("sealed")]
    [InlineData("null")]
    [InlineData("noncanonical")]
    public void Forged_base_graphs_fail_before_type_flow_or_emission(string kind)
    {
        var catalog = Catalog(); var parent = catalog.Types.Single(t => ObjectTypeId.From(t.Source) == Base);
        parent = kind == "sealed" ? parent with { Attributes = parent.Attributes | TypeAttributes.Sealed }
            : parent with { BaseType = kind switch { "missing" => new(Hash, 0x02000009), "self" => Base, "cycle" => Deep,
                "null" => null, _ => Object with { ContentHash = Hash.ToUpperInvariant() } } };
        catalog = catalog with { Types = catalog.Types.SetItem(1, parent) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
        var method = Method([IrType.Reference(Deep), IrType.Reference(Right)], IrType.Reference(Object),
            [Define(0, IrType.Reference(Deep), new IrExpression.Parameter(0))], 0);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method) with { Types = catalog }));
    }

    [Fact]
    public void Return_assignability_matches_dotnet_for_all_pairs_and_ignores_display_names()
    {
        (ObjectTypeId Id, Type Runtime)[] types = [(Object, typeof(object)), (Base, typeof(BaseOracle)),
            (Left, typeof(LeftOracle)), (Deep, typeof(DeepOracle)), (Right, typeof(RightOracle))];
        foreach (var source in types) foreach (var target in types)
        {
            var method = Method([IrType.Reference(source.Id), IrType.Reference(Deep), IrType.Reference(Right)], IrType.Reference(target.Id),
                [Define(0, IrType.Reference(source.Id), new IrExpression.Parameter(0))], 0);
            if (target.Runtime.IsAssignableFrom(source.Runtime)) ReferenceIrValidator.Validate(Program(method));
            else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
        }
    }

    [Fact]
    public void Ancestors_flow_through_locals_calls_and_declared_block_joins_without_retagging()
    {
        var deep = IrType.Reference(Deep); var right = IrType.Reference(Right); var parent = IrType.Reference(Base);
        var callee = Method([parent], parent, [Define(0, parent, new IrExpression.Parameter(0))], 0)
            with { Source = new("fixture", Hash, 0x06000002, "callee") };
        var entry = Method([deep, right, IrType.Int32], parent,
            [Define(0, deep, new IrExpression.Parameter(0)), new IrStatement.StoreLocal(0, 0, 0),
             Define(1, parent, new IrExpression.Local(0)), Define(2, parent, new IrExpression.Call(IrMethodId.From(callee.Source), [0]), true)], 2)
            with { Locals = [parent] };
        ReferenceIrValidator.Validate(new(IrMethodId.From(entry.Source), [entry, callee], Catalog()));
        var bad = entry with { Blocks = [entry.Blocks[0] with { Statements = entry.Blocks[0].Statements.SetItem(0,
            Define(0, parent, new IrExpression.Parameter(0))) }] };
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(IrMethodId.From(bad.Source), [bad, callee], Catalog())));
        var joined = entry with { Locals = [], Blocks = [
            new(0, 0, [], [Define(0, deep, new IrExpression.Parameter(0)), Define(1, right, new IrExpression.Parameter(1)),
                Define(2, IrType.Int32, new IrExpression.Parameter(2))], new IrBranch(1, Int32Comparison.Nonzero, 2, null, new(1, [0]), new(1, [1]))),
            new(1, 2, [parent], [new IrStatement.Define(new(0, parent, 2, new IrExpression.StackInput(0)))], new IrReturn(3, 0))] };
        ReferenceIrValidator.Validate(Program(joined));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(joined with { ReturnType = deep })));
    }

    [Fact]
    public void Base_field_closure_is_reachable_but_extra_unrelated_catalog_roots_are_rejected()
    {
        var catalog = Catalog();
        var parent = catalog.Types[1] with { Fields = [new(new(Base, 0x04000001), "Link", FieldAttributes.Public, new ObjectStorage.Reference(Right))] };
        catalog = catalog with { Roots = [Deep], Types = catalog.Types.SetItem(1, parent) };
        var method = Method([IrType.Reference(Deep)], IrType.Reference(Object), [Define(0, IrType.Reference(Deep), new IrExpression.Parameter(0))], 0);
        ReferenceIrValidator.Validate(Program(method) with { Types = catalog });
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method))); // Right is an unrelated root without the field.
    }

    [Fact]
    public void Native_object_emission_accepts_validated_inherited_catalogs()
    {
        var method = Method([], IrType.Int32, [Define(0, IrType.Int32, new IrExpression.Constant(1))], 0)
            with { Locals = [IrType.Reference(Deep), IrType.Reference(Right)] };
        var program = new ObjectIrProgram(Program(method), [new(IrMethodId.From(method.Source), Deep, ObjectMethodKind.Static,
            MethodAttributes.Public | MethodAttributes.Static, true)]);
        ObjectIrValidator.Validate(program);
        Assert.Contains("derives_from", ObjectCppEmitter.EmitProgram(program).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Cil_translation_includes_inherited_reference_locals()
    {
        var builder = new ResolutionAssemblyBuilder(new("InheritedLocal", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object"); var parent = builder.Type("Example", "Parent", baseType: rootObject);
        var child = builder.Type("Example", "Child", baseType: parent);
        var local = new BlobBuilder(); local.WriteByte(7); local.WriteByte(1); local.WriteByte(0x12);
        local.WriteCompressedInteger(MetadataTokens.GetRowNumber(child) * 4);
        var root = builder.Method("Run", [0, 0, 8], locals: local.ToArray(), emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, root, image);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ObjectIrValidator.Validate(result.Program!);
    }
}
