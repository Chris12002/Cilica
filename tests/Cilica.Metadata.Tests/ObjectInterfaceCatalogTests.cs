using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectInterfaceCatalogTests
{
    private static readonly string Hash = new('a', 64);
    private const TypeAttributes ContractFlags = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
    private static readonly ObjectTypeId Object = Id(2), Read = Id(3), Left = Id(4), Right = Id(5), Diamond = Id(6),
        Parent = Id(7), Child = Id(8), Other = new(new string('b', 64), 0x02000003);
    private interface IRead { }
    private interface ILeft : IRead { }
    private interface IRight : IRead { }
    private interface IDiamond : ILeft, IRight { }
    private class ParentOracle : IDiamond { }
    private sealed class ChildOracle : ParentOracle { }
    private sealed class OtherOracle { }
    private static ObjectTypeId Id(int row) => new(Hash, 0x02000000 | row);
    private static TypeSource Source(ObjectTypeId id) => new("fixture", id.ContentHash, id.Token, "SameName");
    private static ObjectTypeLayout Layout(ObjectTypeId id, ObjectTypeId? parent, params ObjectTypeId[] contracts) =>
        new(Source(id), parent, TypeAttributes.Public, []) { Interfaces = [.. contracts] };
    private static ObjectInterface Contract(ObjectTypeId id, params ObjectTypeId[] parents) => new(Source(id), ContractFlags, [.. parents]);
    private static ObjectTypeCatalog Catalog() => new([Child, Other], Object,
        [Layout(Object, null), Layout(Parent, Object, Diamond), Layout(Child, Parent), Layout(Other, Object)])
        { Interfaces = [Contract(Read), Contract(Left, Read), Contract(Right, Read), Contract(Diamond, Left, Right)] };
    private static readonly ObjectTypeId[] AllIds = [Object, Read, Left, Right, Diamond, Parent, Child, Other];

    private static IrStatement.Define Define(int id, IrType type, IrExpression expression, bool fault = false) =>
        new(new(id, type, 0, expression), fault ? IrExceptionTarget.MethodExit : null);
    private static IrMethod Method(ImmutableArray<IrType> parameters, IrType result, ImmutableArray<IrStatement> statements, int returned) =>
        new(new("fixture", Hash, 0x06000001, "test"), parameters, result, [new(0, 0, [], statements, new IrReturn(1, returned))], []);
    private static ReferenceIrProgram Program(IrMethod method, ObjectTypeCatalog? catalog = null) =>
        new(IrMethodId.From(method.Source), [method], catalog ?? Catalog());
    private static ObjectModelResult Build(ResolutionAssemblyBuilder builder, TypeDefinitionHandle root) => Build(builder.Build(), root);
    private static ObjectModelResult Build(byte[] bytes, TypeDefinitionHandle root)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", bytes)).Value!;
        return ObjectModelBuilder.Build(resolver, resolver.ResolveType(image, root).Value!, image);
    }

    [Fact]
    public void Every_assignment_pair_matches_dotnet_including_diamonds_and_inherited_contracts()
    {
        Type[] runtime = [typeof(object), typeof(IRead), typeof(ILeft), typeof(IRight), typeof(IDiamond),
            typeof(ParentOracle), typeof(ChildOracle), typeof(OtherOracle)];
        for (int source = 0; source < AllIds.Length; source++) for (int target = 0; target < AllIds.Length; target++)
        {
            var method = Method([IrType.Reference(AllIds[source]), .. AllIds.Select(IrType.Reference)], IrType.Reference(AllIds[target]),
                [Define(0, IrType.Reference(AllIds[source]), new IrExpression.Parameter(0))], 0);
            if (runtime[target].IsAssignableFrom(runtime[source])) ReferenceIrValidator.Validate(Program(method));
            else Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method)));
        }
    }

    [Fact]
    public void Contracts_flow_through_locals_call_arguments_returns_and_declared_joins()
    {
        var child = IrType.Reference(Child); var read = IrType.Reference(Read); var diamond = IrType.Reference(Diamond);
        var callee = Method([read], read, [Define(0, read, new IrExpression.Parameter(0))], 0)
            with { Source = new("fixture", Hash, 0x06000002, "callee") };
        var entry = Method([child, IrType.Reference(Other)], read,
            [Define(0, child, new IrExpression.Parameter(0)), new IrStatement.StoreLocal(0, 0, 0),
             Define(1, diamond, new IrExpression.Local(0)), Define(2, read, new IrExpression.Call(IrMethodId.From(callee.Source), [1]), true)], 2)
            with { Locals = [diamond] };
        ReferenceIrValidator.Validate(new(IrMethodId.From(entry.Source), [entry, callee], Catalog()));
        var retagged = entry with { Blocks = [entry.Blocks[0] with { Statements = entry.Blocks[0].Statements.SetItem(0,
            Define(0, diamond, new IrExpression.Parameter(0))) }] };
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(new(IrMethodId.From(entry.Source), [retagged, callee], Catalog())));
        var joined = entry with { Locals = [], Blocks = [
            new(0, 0, [], [Define(0, child, new IrExpression.Parameter(0)), Define(1, IrType.NullReference, new IrExpression.Null())], new IrJump(1, new(1, [0, 1]))),
            new(1, 2, [read, diamond], [new IrStatement.Define(new(0, read, 2, new IrExpression.StackInput(0)))], new IrReturn(3, 0))] };
        ReferenceIrValidator.Validate(Program(joined));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(joined with { ReturnType = child })));
        var nullLocal = Method([child, IrType.Reference(Other)], read,
            [Define(0, IrType.NullReference, new IrExpression.Null()), new IrStatement.StoreLocal(0, 0, 0), Define(1, read, new IrExpression.Local(0))], 1)
            with { Locals = [read] };
        ReferenceIrValidator.Validate(Program(nullLocal));
    }

    [Fact]
    public void Metadata_catalog_separates_contracts_from_layouts_and_survives_reader_disposal()
    {
        var builder = new ResolutionAssemblyBuilder(new("Contracts", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        var read = builder.Type("Example", "Read", attributes: ContractFlags);
        var left = builder.Type("Example", "Left", attributes: ContractFlags); builder.Implements(left, read);
        var right = builder.Type("Example", "Right", attributes: ContractFlags); builder.Implements(right, read);
        var diamond = builder.Type("Example", "Diamond", attributes: ContractFlags); builder.Implements(diamond, left); builder.Implements(diamond, right);
        var parent = builder.Type("Example", "Parent", baseType: rootObject); builder.Implements(parent, diamond);
        // FIELD CLASS Read. Interface references retain the same logical reference storage kind.
        builder.Field("Link", 0x06, 0x12, (byte)(MetadataTokens.GetRowNumber(read) * 4));
        var child = builder.Type("Example", "Child", baseType: parent);
        byte[] bytes = builder.Build();
        var result = Build(bytes, child); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var model = result.Model!; ObjectModelValidator.Validate(model);
        Assert.Equal(3, model.Types.Length); Assert.Equal(4, model.Interfaces.Length);
        var parentLayout = model.Types.Single(t => t.Source.Token == MetadataTokens.GetToken(parent));
        Assert.Equal([0], parentLayout.ReferenceSlots);
        Assert.Equal(model.Interfaces.Single(i => i.Source.Token == MetadataTokens.GetToken(diamond)).Source.Token, Assert.Single(parentLayout.Interfaces).Token);
        Assert.Equal(MetadataTokens.GetToken(read), Assert.IsType<ObjectStorage.Reference>(Assert.Single(parentLayout.Fields).Storage).Type.Token);
        var contracts = JsonSerializer.Deserialize<ImmutableArray<ObjectInterface>>(JsonSerializer.Serialize(model.Interfaces));
        ObjectModelValidator.Validate(model with { Interfaces = contracts });
        var method = Method([IrType.Reference(model.Entry)], IrType.Reference(ObjectTypeId.From(model.Interfaces[0].Source)),
            [Define(0, IrType.Reference(model.Entry), new IrExpression.Parameter(0))], 0);
        ReferenceIrValidator.Validate(Program(method, new([model.Entry], model.ObjectBase, model.Types) { Interfaces = model.Interfaces }));
        var interfaceOnly = Build(bytes, diamond); Assert.True(interfaceOnly.Success);
        Assert.Single(interfaceOnly.Model!.Types); Assert.Equal(4, interfaceOnly.Model.Interfaces.Length);
    }

    [Fact]
    public void Interface_field_closure_is_used_but_unused_catalog_contracts_are_rejected()
    {
        var catalog = Catalog() with { Roots = [Other], Types = [Layout(Object, null), Layout(Other, Object) with
            { Fields = [new(new(Other, 0x04000001), "Link", FieldAttributes.Public, new ObjectStorage.Reference(Diamond))] }] };
        var method = Method([IrType.Reference(Other)], IrType.Reference(Object), [Define(0, IrType.Reference(Other), new IrExpression.Parameter(0))], 0);
        ReferenceIrValidator.Validate(Program(method, catalog));
        var unused = catalog with { Roots = [Other, Diamond], Types = [Layout(Object, null), Layout(Other, Object)] };
        ObjectModelValidator.Validate(unused);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method, unused)));
    }

    [Fact]
    public void Same_named_equal_token_contracts_from_different_images_do_not_merge()
    {
        var core = new ResolutionAssemblyBuilder(new("ContractCore", new Version(1, 0, 0, 0)));
        core.Type("System", "Object"); var coreContract = core.Type("Example", "Read", attributes: ContractFlags);
        var app = new ResolutionAssemblyBuilder(new("ContractApp", new Version(1, 0, 0, 0)));
        app.Type("Example", "Unused"); var appContract = app.Type("Example", "Read", attributes: ContractFlags);
        var scope = app.Reference(new("ContractCore", new Version(1, 0, 0, 0)));
        var objectRef = app.TypeReference("System", "Object", scope); var contractRef = app.TypeReference("Example", "Read", scope);
        var root = app.Type("Example", "Root", baseType: objectRef); app.Implements(root, contractRef);
        using var files = new ResolverFiles(); string corePath = files.Write("core.dll", core.Build()), appPath = files.Write("app.dll", app.Build());
        using var resolver = files.Open("refs"); var coreImage = resolver.OpenInput(corePath).Value!; var appImage = resolver.OpenInput(appPath).Value!;
        var result = ObjectModelBuilder.BuildCatalog(resolver, [resolver.ResolveType(appImage, root).Value!, resolver.ResolveType(appImage, appContract).Value!], coreImage);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); var catalog = result.Catalog!;
        var contracts = catalog.Interfaces; Assert.Equal(2, contracts.Length);
        Assert.Equal(contracts[0].Source.Token, contracts[1].Source.Token); Assert.Equal(contracts[0].Source.Name, contracts[1].Source.Name);
        Assert.NotEqual(contracts[0].Source.ContentHash, contracts[1].Source.ContentHash);
        var rootId = new ObjectTypeId(appImage.ContentHash, MetadataTokens.GetToken(root));
        var coreId = new ObjectTypeId(coreImage.ContentHash, MetadataTokens.GetToken(coreContract));
        var appId = new ObjectTypeId(appImage.ContentHash, MetadataTokens.GetToken(appContract));
        var method = Method([IrType.Reference(rootId), IrType.Reference(appId)], IrType.Reference(coreId),
            [Define(0, IrType.Reference(rootId), new IrExpression.Parameter(0))], 0);
        ReferenceIrValidator.Validate(Program(method, catalog));
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method with { ReturnType = IrType.Reference(appId) }, catalog)));
    }

    [Theory]
    [InlineData("default_catalog")]
    [InlineData("default_edges")]
    [InlineData("default_parents")]
    [InlineData("duplicate_identity")]
    [InlineData("class_identity")]
    [InlineData("duplicate_edge")]
    [InlineData("duplicate_parent")]
    [InlineData("missing_edge")]
    [InlineData("class_edge")]
    [InlineData("interface_base")]
    [InlineData("cycle")]
    [InlineData("self")]
    [InlineData("private")]
    [InlineData("sealed")]
    [InlineData("not_abstract")]
    [InlineData("null_source")]
    [InlineData("noncanonical")]
    public void Forged_contract_graphs_fail_before_reference_flow_and_native_emission(string kind)
    {
        var catalog = Catalog(); var contract = catalog.Interfaces[0]; var parent = catalog.Types[1];
        switch (kind)
        {
            case "default_catalog": catalog = catalog with { Interfaces = default }; break;
            case "duplicate_identity": catalog = catalog with { Interfaces = catalog.Interfaces.Add(contract) }; break;
            case "class_identity": contract = contract with { Source = Source(Parent) }; break;
            case "default_edges": parent = parent with { Interfaces = default }; break;
            case "duplicate_edge": parent = parent with { Interfaces = [Diamond, Diamond] }; break;
            case "missing_edge": parent = parent with { Interfaces = [Id(99)] }; break;
            case "class_edge": parent = parent with { Interfaces = [Child] }; break;
            case "interface_base": parent = parent with { BaseType = Read }; break;
            case "default_parents": contract = contract with { Parents = default }; break;
            case "duplicate_parent": contract = contract with { Parents = [Left, Left] }; break;
            case "cycle": contract = contract with { Parents = [Diamond] }; break;
            case "self": contract = contract with { Parents = [Read] }; break;
            case "private": contract = contract with { Attributes = TypeAttributes.Interface | TypeAttributes.Abstract }; break;
            case "sealed": contract = contract with { Attributes = ContractFlags | TypeAttributes.Sealed }; break;
            case "not_abstract": contract = contract with { Attributes = TypeAttributes.Public | TypeAttributes.Interface }; break;
            case "null_source": contract = contract with { Source = null! }; break;
            case "noncanonical": contract = contract with { Parents = [Left with { ContentHash = Hash.ToUpperInvariant() }] }; break;
        }
        if (kind is not "default_catalog" and not "duplicate_identity") catalog = catalog with { Interfaces = catalog.Interfaces.SetItem(0, contract) };
        catalog = catalog with { Types = catalog.Types.SetItem(1, parent) };
        Assert.Throws<ArgumentException>(() => ObjectModelValidator.Validate(catalog));
        var method = Method([.. AllIds.Select(IrType.Reference)], IrType.Int32, [Define(0, IrType.Int32, new IrExpression.Constant(1))], 0);
        Assert.Throws<ArgumentException>(() => ReferenceIrValidator.Validate(Program(method, catalog)));
    }

    [Theory]
    [InlineData("cycle")]
    [InlineData("duplicate")]
    [InlineData("class_edge")]
    [InlineData("base")]
    [InlineData("field")]
    [InlineData("generic")]
    [InlineData("private")]
    [InlineData("initializer")]
    public void Unsupported_metadata_contracts_return_no_partial_model(string kind)
    {
        var builder = new ResolutionAssemblyBuilder(new("InvalidContracts", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object");
        var read = builder.Type("Example", "Read", baseType: kind == "base" ? rootObject : default,
            attributes: kind == "private" ? TypeAttributes.Interface | TypeAttributes.Abstract : ContractFlags, arity: kind == "generic" ? 1 : 0);
        if (kind == "cycle") builder.Implements(read, read);
        if (kind == "field") builder.Field("Value", 0x06, 0x08);
        if (kind == "initializer") builder.Method(".cctor", [0, 0, 1]);
        var root = builder.Type("Example", "Root", baseType: rootObject); builder.Implements(root, kind == "class_edge" ? rootObject : read);
        if (kind == "duplicate") builder.Implements(root, read);
        var result = Build(builder, root); Assert.Null(result.Model);
        var diagnostic = Assert.Single(result.Diagnostics); Assert.Equal(DiagnosticIds.ExecutionUnsupported, diagnostic.Id);
        Assert.Contains("layout inclusion", diagnostic.Location, StringComparison.Ordinal);
        if (kind is "base" or "field" or "generic" or "private" or "initializer") Assert.Contains("via interface", diagnostic.Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Combined_class_and_interface_closure_obeys_exact_type_limit_iteratively(bool excess)
    {
        var builder = new ResolutionAssemblyBuilder(new("DeepContracts", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object"); TypeDefinitionHandle previous = default;
        for (int i = 0; i < ObjectModelLimits.MaximumTypes - 2 + (excess ? 1 : 0); i++)
        {
            var next = builder.Type("Example", "I" + i, attributes: ContractFlags);
            if (!previous.IsNil) builder.Implements(next, previous);
            previous = next;
        }
        var root = builder.Type("Example", "Root", baseType: rootObject); builder.Implements(root, previous);
        var result = Build(builder, root); Assert.Equal(!excess, result.Success);
        if (excess) Assert.Equal(DiagnosticIds.TranslationLimit, Assert.Single(result.Diagnostics).Id);
        else
        {
            var model = result.Model!; Assert.Equal(256, model.Types.Length + model.Interfaces.Length);
            var method = Method([IrType.Reference(model.Entry)], IrType.Reference(ObjectTypeId.From(model.Interfaces[0].Source)),
                [Define(0, IrType.Reference(model.Entry), new IrExpression.Parameter(0))], 0);
            ReferenceIrValidator.Validate(Program(method, new([model.Entry], model.ObjectBase, model.Types) { Interfaces = model.Interfaces }));
        }
    }

    [Fact]
    public void Native_consumers_reject_valid_contract_catalogs_before_producing_source_or_root_plans()
    {
        var method = Method([], IrType.Int32, [Define(0, IrType.Int32, new IrExpression.Constant(1))], 0)
            with { Locals = [.. AllIds.Select(IrType.Reference)] };
        ReferenceIrValidator.Validate(Program(method));
        var program = new ObjectIrProgram(Program(method), [new(IrMethodId.From(method.Source), Child, ObjectMethodKind.Static,
            MethodAttributes.Public | MethodAttributes.Static, true)]);
        Assert.Throws<ArgumentException>(() => ObjectIrValidator.Validate(program));
        Assert.Throws<ArgumentException>(() => ObjectRootAssignment.Assign(program));
        Assert.Throws<ArgumentException>(() => ObjectCppEmitter.EmitProgram(program));
    }

    [Fact]
    public void Cil_translation_with_an_included_interface_class_remains_explicitly_gated()
    {
        var builder = new ResolutionAssemblyBuilder(new("ContractBody", new Version(1, 0, 0, 0)));
        var rootObject = builder.Type("System", "Object"); var contract = builder.Type("Example", "Read", attributes: ContractFlags);
        var root = builder.Type("Example", "Root", baseType: rootObject); builder.Implements(root, contract);
        byte[] locals = [7, 1, 0x12, (byte)(MetadataTokens.GetRowNumber(root) * 4)];
        var entry = builder.Method("Run", [0, 0, 8], locals: locals, emit: il => { il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, image); Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.ExecutionUnsupported && d.Message.Contains("Interface catalogs", StringComparison.Ordinal));
    }
}
