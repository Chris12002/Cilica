using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Cilica.Metadata;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ObjectArrayTranslationTests
{
    private static ObjectIrProgram Authored(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ArrayProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ArrayProbe).GetMethod(name)!.MetadataToken);
        var result = ObjectTranslator.Translate(resolver, image, method, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        ObjectIrValidator.Validate(result.Program!); return result.Program!;
    }

    [Theory]
    [InlineData(2, 0, 7)]
    [InlineData(5, 4, int.MaxValue)]
    [InlineData(2, -1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 0, 0)]
    public void Allocation_aliasing_calls_mutation_and_bounds_match_dotnet(int length, int index, int value)
    {
        var program = Authored(nameof(ArrayProbe.Integers));
        var expected = Record.Exception(() => ArrayProbe.Integers(length, index, value));
        var evaluator = new ObjectTranslationConformanceTests.Evaluator(program);
        if (expected is not null) Assert.Equal(expected.GetType(), Record.Exception(() => evaluator.Run(length, index, value))?.GetType());
        else Assert.Equal(ArrayProbe.Integers(length, index, value), evaluator.Run(length, index, value));
        Assert.Single(program.Flow.Types.Arrays);
        Assert.Contains(ObjectRootAssignment.Assign(program).SelectMany(r => r.Safepoints), p => p.Kind == ObjectSafepointKind.Allocation);
        Assert.Throws<NotSupportedException>(() => ObjectCppEmitter.EmitProgram(program));
    }

    [Theory]
    [InlineData(nameof(ArrayProbe.Zeroes))]
    [InlineData(nameof(ArrayProbe.NullLength))]
    [InlineData(nameof(ArrayProbe.NullLoad))]
    [InlineData(nameof(ArrayProbe.NullStore))]
    public void Defaults_and_null_faults_match_dotnet(string name)
    {
        var evaluator = new ObjectTranslationConformanceTests.Evaluator(Authored(name));
        var oracle = typeof(ArrayProbe).GetMethod(name)!;
        object[] args = name == nameof(ArrayProbe.NullLength) ? [] : [2];
        var expected = Record.Exception(() => oracle.Invoke(null, args));
        int[] values = args.Cast<int>().ToArray();
        if (expected is TargetInvocationException fault) Assert.Equal(fault.InnerException!.GetType(), Record.Exception(() => evaluator.Run(values))?.GetType());
        else Assert.Equal((int)oracle.Invoke(null, args)!, evaluator.Run(values));
    }

    [Fact]
    public void Reference_fields_identity_and_default_null_translate_and_match_dotnet()
    {
        var program = Authored(nameof(ArrayProbe.References));
        Assert.Contains(program.Flow.Types.Types.SelectMany(t => t.Fields), f => f.Storage is ObjectStorage.Reference { Type.ArrayElement: not null });
        Assert.Equal(ArrayProbe.References(), new ObjectTranslationConformanceTests.Evaluator(program).Run());
    }

    [Fact]
    public void Array_parameters_and_returns_agree_across_interface_dispatch()
    {
        var program = Authored(nameof(ArrayProbe.Dispatch));
        var contract = Assert.Single(program.InterfaceDispatch!.Declarations);
        var array = Assert.Single(program.Flow.Types.Arrays);
        Assert.Equal(IrType.Reference(array), contract.ReturnType);
        Assert.Equal(contract.ReturnType, contract.Parameters[1]);
        Assert.Contains(program.Flow.Methods, m => m.ReturnType == contract.ReturnType);
    }

    [Fact]
    public void Reference_vectors_include_nominal_elements_and_covariant_views()
    {
        var program = Authored(nameof(ArrayProbe.Covariance));
        Assert.Equal(2, program.Flow.Types.Arrays.Length);
        Assert.Single(program.Flow.Types.Interfaces);
    }

    [Fact]
    public void Array_signatures_are_preserved_by_class_virtual_overrides()
    {
        var program = Authored(nameof(ArrayProbe.Virtual));
        Assert.Single(program.Flow.Types.Arrays);
        Assert.Equal(2, Assert.Single(program.Dispatch).Targets.Length);
    }

    [Theory]
    [InlineData("same", false)]
    [InlineData("null", false)]
    [InlineData("different", false)]
    [InlineData("different", true)]
    public void Array_stack_joins_preserve_exact_types_or_require_an_explicit_local(string kind, bool typedLocal)
    {
        var b = new ResolutionAssemblyBuilder(new("ArrayJoin" + kind + typedLocal, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); var child = b.Type("Example", "Child", baseType: obj);
        b.Type("Example", "Driver", baseType: obj);
        var entry = b.Method("Run", [0, 1, 8, 8], locals: typedLocal ? [7, 1, 0x1d, 0x1c] : null, emit: il =>
        {
            var alternate = il.DefineLabel(); var join = il.DefineLabel();
            il.LoadArgument(0); il.Branch(ILOpCode.Brtrue_s, alternate);
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(child);
            if (typedLocal) il.StoreLocal(0);
            il.Branch(ILOpCode.Br_s, join); il.MarkLabel(alternate);
            // The taken edge is visited first; force a transient null-only view.
            if (kind == "null") il.OpCode(ILOpCode.Ldnull);
            else { il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(kind == "different" ? obj : child); }
            if (typedLocal) il.StoreLocal(0);
            il.MarkLabel(join);
            if (typedLocal) il.LoadLocal(0);
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ldelem_ref); il.OpCode(ILOpCode.Pop);
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
        });
        var result = ObjectControlFlowTests.Translate(new(b, entry));
        if (kind == "different" && !typedLocal)
            Assert.Contains("explicitly typed array local", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        else
        {
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal(kind == "different" ? 2 : 1, result.Program!.Flow.Types.Arrays.Length);
        }
    }

    [Theory]
    [InlineData("jagged", false)]
    [InlineData("jagged", true)]
    [InlineData("multidimensional", false)]
    [InlineData("multidimensional", true)]
    [InlineData("primitive", false)]
    [InlineData("primitive", true)]
    public void Unsupported_vector_storage_is_rejected_in_fields_and_locals(string shape, bool field)
    {
        byte[] storage = shape switch { "jagged" => [0x1d, 0x1d, 8], "multidimensional" => [0x14, 8, 1, 0, 0], _ => [0x1d, 10] };
        var b = new ResolutionAssemblyBuilder(new("ArrayStorage" + shape + field, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); var holder = b.Type("Example", "Holder", baseType: obj);
        if (field) b.Field("Values", [6, .. storage]);
        var entry = b.Method("Run", [0, 0, 8], locals: field ? [7, 1, 0x12, 12] : [7, 1, .. storage],
            emit: il => { il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret); });
        var result = ObjectControlFlowTests.Translate(new(b, entry));
        Assert.Equal(DiagnosticIds.ExecutionSignatureUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Standalone_interface_preparation_discovers_array_signatures()
    {
        var b = new ResolutionAssemblyBuilder(new("ArrayContract", new Version(1, 0, 0, 0)));
        b.Type("System", "Object");
        var contract = b.Type("Example", "IArray", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = b.Method("Echo", [0x20, 1, 0x1d, 8, 0x1d, 8],
            attributes: MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("contract.dll", b.Build())).Value!;
        var type = resolver.ResolveType(image, contract).Value!;
        var method = resolver.ResolveDeclaredMethod(image, declaration).Value!;
        var result = ObjectInterfacePlanBuilder.Build(resolver, [type], [method], image);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Single(result.Plan!.Types.Arrays);
    }

    [Fact]
    public void Allocation_element_mapping_uses_selected_runtime_identity_not_the_type_name()
    {
        var identity = new AssemblyIdentity("ArrayCore", new Version(1, 0, 0, 0));
        var core = new ResolutionAssemblyBuilder(identity);
        var obj = core.Type("System", "Object"); core.Type("System", "Int32", baseType: obj);
        var b = new ResolutionAssemblyBuilder(new("OtherArrayElements", new Version(1, 0, 0, 0)));
        var importedObject = b.TypeReference("System", "Object", b.Reference(identity));
        var unrelated = b.Type("System", "Int32", baseType: importedObject);
        b.Type("Example", "Driver", baseType: importedObject);
        var entry = b.Method("Run", [0, 0, 8], emit: il =>
        {
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Newarr); il.Token(unrelated);
            il.LoadConstantI4(0); il.OpCode(ILOpCode.Ldelem_ref); il.OpCode(ILOpCode.Pop);
            il.LoadConstantI4(1); il.OpCode(ILOpCode.Ret);
        });
        using var files = new ResolverFiles();
        var corePath = files.Write("ArrayCore.dll", core.Build());
        var inputPath = files.Write("input.dll", b.Build());
        using var resolver = files.Open("refs");
        var image = resolver.OpenInput(inputPath).Value!; var runtime = resolver.OpenInput(corePath).Value!;
        var result = ObjectTranslator.Translate(resolver, image, entry, runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var array = Assert.Single(result.Program!.Flow.Types.Arrays);
        Assert.Equal(ObjectArrayElementKind.Reference, array.ArrayElement!.Kind);
        Assert.Equal(image.ContentHash, array.ArrayElement.Type!.ContentHash);
        Assert.Equal(runtime.ContentHash, array.ContentHash);
    }

    [Theory]
    [InlineData("opcode")]
    [InlineData("length")]
    [InlineData("jagged")]
    [InlineData("wide")]
    [InlineData("index")]
    [InlineData("value")]
    [InlineData("native_length")]
    [InlineData("split_length")]
    public void Unsupported_shapes_and_invalid_array_operands_are_located_diagnostics(string kind)
    {
        var b = new ResolutionAssemblyBuilder(new("BadArray" + kind, new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object"); b.Type("Example", "Driver", baseType: obj);
        var element = b.Specification(kind == "jagged" ? [0x1d, 8] : kind == "wide" ? [10] : [8]);
        var entry = b.Method("Run", [0, 0, 8], emit: il =>
        {
            if (kind == "length") il.OpCode(ILOpCode.Ldnull); else il.LoadConstantI4(1);
            il.OpCode(ILOpCode.Newarr); il.Token(element);
            if (kind is "native_length" or "split_length")
            {
                il.OpCode(ILOpCode.Ldlen);
                if (kind == "split_length") { var next = il.DefineLabel(); il.Branch(ILOpCode.Br_s, next); il.MarkLabel(next); il.OpCode(ILOpCode.Conv_i4); }
            }
            else
            {
                if (kind == "index") il.OpCode(ILOpCode.Ldnull); else il.LoadConstantI4(0);
                if (kind == "value") { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Stelem_i4); il.LoadConstantI4(0); }
                else il.OpCode(kind == "opcode" ? ILOpCode.Ldelem_ref : ILOpCode.Ldelem_i4);
            }
            il.OpCode(ILOpCode.Ret);
        });
        var result = ObjectControlFlowTests.Translate(new(b, entry));
        Assert.False(result.Success); var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("IL_", diagnostic.Location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(127, true)]
    [InlineData(128, false)]
    public void Fields_and_vectors_share_the_type_limit(int count, bool success)
    {
        var b = new ResolutionAssemblyBuilder(new("ArrayTypeBudget", new Version(1, 0, 0, 0)));
        var obj = b.Type("System", "Object");
        var elements = Enumerable.Range(0, count).Select(i => b.Type("Example", "Element" + i, baseType: obj)).ToArray();
        var holder = b.Type("Example", "Holder", baseType: obj);
        foreach (var element in elements)
        {
            var blob = new BlobBuilder(); blob.WriteByte(6); blob.WriteByte(0x1d); blob.WriteByte(0x12);
            blob.WriteCompressedInteger(MetadataTokens.GetRowNumber(element) << 2);
            b.Field("Items" + element.GetHashCode(), blob.ToArray());
        }
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var image = resolver.OpenInput(files.Write("arrays.dll", b.Build())).Value!;
        var root = resolver.ResolveType(image, holder).Value!;
        var result = ObjectModelBuilder.Build(resolver, root, image);
        Assert.Equal(success, result.Success);
        if (success)
        {
            Assert.Equal(count, result.Model!.Arrays.Length);
            Assert.Equal(ObjectModelLimits.MaximumTypes, result.Model.Types.Length + result.Model.Arrays.Length);
        }
        else Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
    }
}
