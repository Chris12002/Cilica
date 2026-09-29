using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Cilica.Cli;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Output;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class RuntimeTypeMappingTests
{
    private static readonly AssemblyIdentity LibraryId = new("Runtime", new Version(1, 0, 0, 0));
    private static ResolutionAssemblyBuilder Builder(string name = "Input") => new(new(name, new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void Token(InstructionEncoder il, ILOpCode code, EntityHandle handle) { il.OpCode(code); il.Token(handle); }

    [Theory]
    [InlineData(PrimitiveTypeCode.Boolean, "Boolean")]
    [InlineData(PrimitiveTypeCode.Char, "Char")]
    [InlineData(PrimitiveTypeCode.SByte, "SByte")]
    [InlineData(PrimitiveTypeCode.Byte, "Byte")]
    [InlineData(PrimitiveTypeCode.Int16, "Int16")]
    [InlineData(PrimitiveTypeCode.UInt16, "UInt16")]
    [InlineData(PrimitiveTypeCode.Int32, "Int32")]
    [InlineData(PrimitiveTypeCode.UInt32, "UInt32")]
    [InlineData(PrimitiveTypeCode.Int64, "Int64")]
    [InlineData(PrimitiveTypeCode.UInt64, "UInt64")]
    [InlineData(PrimitiveTypeCode.Single, "Single")]
    [InlineData(PrimitiveTypeCode.Double, "Double")]
    [InlineData(PrimitiveTypeCode.IntPtr, "IntPtr")]
    [InlineData(PrimitiveTypeCode.UIntPtr, "UIntPtr")]
    [InlineData(PrimitiveTypeCode.Object, "Object")]
    [InlineData(PrimitiveTypeCode.String, "String")]
    public void Primitive_uses_only_the_selected_library_and_its_forwarders(PrimitiveTypeCode code, string name)
    {
        var implementation = Builder("Implementation"); var expected = implementation.Type("System", name);
        var facade = Builder("Facade"); facade.Forward("System", name, facade.Reference(new("Implementation", new Version(1, 0, 0, 0))));
        var decoy = Builder("Decoy"); decoy.Type("System", name);
        using var files = new ResolverFiles();
        files.Write("implementation.dll", implementation.Build()); files.Write("decoy.dll", decoy.Build());
        string path = files.Write("facade.dll", facade.Build(), "profile");
        using var resolver = files.Open("refs"); var library = resolver.OpenInput(path).Value!;
        var result = resolver.ResolveRuntimeType(library, code);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Equal("Implementation", result.Value!.Assembly.Identity.Name); Assert.Equal(expected, result.Value.Handle);
    }

    [Theory]
    [InlineData(PrimitiveTypeCode.Void)]
    [InlineData(PrimitiveTypeCode.TypedReference)]
    public void Non_receiver_primitives_remain_unsupported(PrimitiveTypeCode code)
    {
        var builder = Builder(); builder.Type("System", code.ToString());
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var result = resolver.ResolveRuntimeType(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, code);
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Missing_selected_type_does_not_fall_back_to_a_catalogue_namesake()
    {
        var selected = Builder("Selected"); selected.Type("", "Empty");
        var decoy = Builder("Decoy"); decoy.Type("System", "Int32");
        using var files = new ResolverFiles(); files.Write("decoy.dll", decoy.Build());
        using var resolver = files.Open("refs");
        var result = resolver.ResolveRuntimeType(resolver.OpenInput(files.Write("selected.dll", selected.Build(), "profile")).Value!, PrimitiveTypeCode.Int32);
        Assert.False(result.Success); Assert.Equal(DiagnosticIds.TypeNotFound, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Selected_path_cannot_override_different_bytes_under_a_catalogue_identity()
    {
        var selected = Builder("Runtime"); selected.Type("System", "Int32");
        var conflict = Builder("Runtime"); conflict.Type("", "Different");
        var input = Builder(); input.Type("", "Entry"); var root = input.Method("Run", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); files.Write("runtime.dll", conflict.Build());
        using var resolver = files.Open("refs");
        var library = resolver.OpenInput(files.Write("selected.dll", selected.Build(), "profile")).Value!;
        Assert.Equal(DiagnosticIds.AssemblyConflict, Assert.Single(resolver.ResolveRuntimeType(library, PrimitiveTypeCode.Int32).Diagnostics).Id);
        var report = resolver.AnalyzeReachability(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], runtimeLibrary: library);
        Assert.Equal(DiagnosticIds.AssemblyConflict, Assert.Single(report.Issues).Diagnostic.Id);
        Assert.Empty(report.RuntimeMappings);
    }

    [Theory]
    [InlineData(0, TypeAttributes.Public | TypeAttributes.Abstract)]
    [InlineData(0, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract)]
    [InlineData(1, TypeAttributes.Public)]
    public void Invalid_runtime_type_shapes_are_reported(int arity, TypeAttributes attributes)
    {
        var builder = Builder(); builder.Type("System", "Int32", arity: arity, attributes: attributes);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var result = resolver.ResolveRuntimeType(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, PrimitiveTypeCode.Int32);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Mapping_checks_session_ownership_and_disposal()
    {
        var builder = Builder(); builder.Type("System", "Int32");
        using var files = new ResolverFiles(); using var first = files.Open(); using var second = files.Open();
        var library = first.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        Assert.Throws<ArgumentException>(() => second.ResolveRuntimeType(library, PrimitiveTypeCode.Int32));
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.ResolveRuntimeType(library, PrimitiveTypeCode.Int32));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Primitive_constrained_call_requires_explicit_mapping_and_keeps_receiver_evidence(bool select)
    {
        var libraryBuilder = new ResolutionAssemblyBuilder(LibraryId);
        var contract = libraryBuilder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        libraryBuilder.Method("M", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract);
        var integer = libraryBuilder.Type("System", "Int32", attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        libraryBuilder.Implements(integer, contract);
        var body = libraryBuilder.Method("M", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, emit: Return);
        var input = Builder(); input.Type("", "Entry");
        var target = input.Member(input.TypeReference("", "I", input.Reference(LibraryId)), "M", 0x20, 0, 1);
        var constraint = input.Specification(8);
        var root = input.Method("Run", [0, 1, 1, 0x10, 8], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, constraint); Token(il, ILOpCode.Callvirt, target); Return(il);
        });
        using var files = new ResolverFiles(); string libraryPath = files.Write("runtime.dll", libraryBuilder.Build());
        using var resolver = files.Open("refs"); var library = resolver.OpenInput(libraryPath).Value!;
        var assembly = resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!;
        var report = resolver.Audit(assembly, [root], [], runtimeLibrary: select ? library : null);
        if (!select)
        {
            Assert.Null(report.RuntimeLibrary); Assert.Empty(report.RuntimeMappings);
            Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains("--runtime-library", StringComparison.Ordinal));
            Assert.DoesNotContain(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        }
        else
        {
            Assert.Empty(report.Issues); Assert.NotNull(report.RuntimeLibrary);
            var mapped = Assert.Single(report.RuntimeMappings); Assert.Equal("Int32", mapped.Primitive);
            Assert.Equal("0x02000003", mapped.Token);
            var edge = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
            Assert.Equal(edge.ConstraintType, edge.ReceiverType);
            Assert.Equal("0x" + MetadataTokens.GetToken(body).ToString("x8", System.Globalization.CultureInfo.InvariantCulture), report.Nodes[edge.To].Token);
        }
    }

    [Theory]
    [InlineData("literal")]
    [InlineData("parameter")]
    [InlineData("field")]
    [InlineData("local")]
    [InlineData("return")]
    public void Implicit_strings_join_named_dispatch_for_each_value_source(string source)
    {
        var (libraryBytes, body) = StringLibrary();
        var input = Builder(); input.Type("", "Entry");
        var target = input.Member(input.TypeReference("", "I", input.Reference(LibraryId)), "M", 0x20, 0, 1);
        var field = source == "field" ? input.FieldWithAttributes("Text", FieldAttributes.Public | FieldAttributes.Static, 6, 0x0e) : default;
        var text = input.String("hello");
        var root = input.Method("Run", source == "parameter" ? [0, 1, 1, 0x0e] : source == "return" ? [0, 0, 0x0e] : [0, 0, 1],
            locals: source == "local" ? [7, 1, 0x0e] : null, emit: il =>
            {
                switch (source)
                {
                    case "literal": il.LoadString(text); break;
                    case "parameter": il.LoadArgument(0); break;
                    case "field": Token(il, ILOpCode.Ldsfld, field); break;
                    case "local": il.LoadLocal(0); break;
                    default: il.OpCode(ILOpCode.Ldnull); break;
                }
                Token(il, ILOpCode.Callvirt, target);
                if (source == "return") il.OpCode(ILOpCode.Ldnull);
                Return(il);
            });
        using var files = new ResolverFiles(); string libraryPath = files.Write("runtime.dll", libraryBytes);
        using var resolver = files.Open("refs"); var library = resolver.OpenInput(libraryPath).Value!;
        var report = resolver.AnalyzeReachability(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], runtimeLibrary: library);
        Assert.Empty(report.Issues);
        var edge = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.Equal(body, Assert.IsType<ReachableEntity.Method>(report.Nodes[edge.To].Entity).Definition.Handle);
        Assert.Equal(PrimitiveTypeCode.String, Assert.Single(report.RuntimeMappings).Primitive);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.RuntimeType && e.To == edge.ReceiverType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void String_mapping_does_not_hide_array_or_reference_body_gaps(bool reference)
    {
        var (bytes, _) = StringLibrary(reference);
        var input = Builder(); input.Type("", "Entry");
        var target = input.Member(input.TypeReference("", "I", input.Reference(LibraryId)), "M", 0x20, 0, 1);
        var root = input.Method("Run", [0, 1, 1, 0x0e], locals: reference ? null : [7, 1, 0x1d, 8], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Callvirt, target); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("runtime.dll", bytes);
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains(reference ? "Reference-assembly" : "receiver synthesis", StringComparison.Ordinal));
    }

    [Fact]
    public void Cli_records_mapping_selection_in_detached_json_and_rejects_duplicate_options()
    {
        var (bytes, _) = StringLibrary(); var input = Builder(); input.Type("", "Entry");
        input.Method("Run", [0, 1, 1, 0x0e], emit: Return);
        using var files = new ResolverFiles(); string library = files.Write("runtime.dll", bytes);
        string path = files.Write("input.dll", input.Build(), "input");
        using var output = new StringWriter(); using var errors = new StringWriter();
        Assert.Equal(0, CilicaCommand.Run(["audit", path, "--root", "06000001", "--runtime-library", library, "--reference", files.DirectoryPath("refs"), "--format", "json"], output, errors));
        Assert.Empty(errors.ToString());
        using var json = JsonDocument.Parse(output.ToString()); var root = json.RootElement;
        int selected = root.GetProperty("runtimeLibrary").GetInt32();
        Assert.StartsWith("Runtime,", root.GetProperty("assemblies")[selected].GetProperty("identity").GetString(), StringComparison.Ordinal);
        Assert.Equal("String", Assert.Single(root.GetProperty("runtimeMappings").EnumerateArray()).GetProperty("primitive").GetString());
        Assert.DoesNotContain(files.Root, output.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, CilicaCommand.Run(["audit", path, "--public-surface", "--runtime-library", library, "--runtime-library", library], TextWriter.Null, TextWriter.Null));
        Assert.Equal(1, CilicaCommand.Run(["audit", path, "--public-surface", "--runtime-library", Path.Combine(files.Root, "missing.dll")], TextWriter.Null, TextWriter.Null));
    }

    [Theory]
    [InlineData(ILOpCode.Ldtoken)]
    [InlineData(ILOpCode.Sizeof)]
    public void Void_pointer_operands_do_not_request_a_nonexistent_runtime_receiver(ILOpCode opcode)
    {
        var builder = Builder(); builder.Type("", "Entry"); var pointer = builder.Specification(0x0f, 1);
        var root = builder.Method("Run", [0, 0, 1], emit: il => { Token(il, opcode, pointer); il.OpCode(ILOpCode.Pop); Return(il); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var report = resolver.AnalyzeReachability(assembly, [root], runtimeLibrary: assembly);
        Assert.Empty(report.Issues); Assert.Empty(report.RuntimeMappings);
    }

    [Fact]
    public void Authored_primitive_constraint_reaches_the_dotnet_implementation_before_the_limit()
    {
        Assert.Equal(-1, PrimitiveReceiverProbe.Run());
        using var files = new ResolverFiles(); string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (string name in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll" })
            files.Write(name, File.ReadAllBytes(Path.Combine(runtime, name)));
        using var resolver = files.Open("refs");
        var library = resolver.OpenInput(Path.Combine(files.DirectoryPath("refs"), "System.Private.CoreLib.dll")).Value!;
        var assembly = resolver.OpenInput(typeof(PrimitiveReceiverProbe).Assembly.Location).Value!;
        var root = MetadataTokens.EntityHandle(typeof(PrimitiveReceiverProbe).GetMethod(nameof(PrimitiveReceiverProbe.Run))!.MetadataToken);
        var report = resolver.AnalyzeReachability(assembly, [root], new(MaximumNodes: 128), runtimeLibrary: library);
        Assert.Contains(report.RuntimeMappings, m => m.Primitive == PrimitiveTypeCode.Int32 && MetadataTokens.GetToken(m.Definition.Handle) == typeof(int).MetadataToken);
        var declaration = Assert.Single(report.Edges, e => e.Kind == DependencyKind.DispatchDeclaration && e.ConstraintType is not null
            && report.Nodes[e.ConstraintType.Value].Entity is ReachableEntity.Type t
            && t.Definition.Definition.Assembly.ContentHash == library.ContentHash
            && MetadataTokens.GetToken(t.Definition.Definition.Handle) == typeof(int).MetadataToken);
        var method = Assert.IsType<ReachableEntity.Method>(report.Nodes[declaration.To].Entity).Definition;
        Assert.Equal(typeof(IComparable<int>).GetMethod(nameof(int.CompareTo))!.MetadataToken, MetadataTokens.GetToken(method.Handle));
        Assert.Equal(library.ContentHash, method.DeclaringType.Definition.Assembly.ContentHash);
        int expectedBody = typeof(int).GetMethod(nameof(int.CompareTo), [typeof(int)])!.MetadataToken;
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch
            && e.ConstraintType == declaration.ConstraintType
            && report.Nodes[e.To].Entity is ReachableEntity.Method target
            && MetadataTokens.GetToken(target.Definition.Handle) == expectedBody
            && target.Definition.DeclaringType.Definition.Assembly.ContentHash == library.ContentHash);
        // Scheduling now reaches the concrete primitive target, while the broader
        // real-library closure remains explicitly bounded and incomplete.
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
    }

    [Fact]
    public void Runtime_mapping_keeps_budget_exhaustion_visible_for_a_broader_library_closure()
    {
        Assert.Equal(42, PrimitiveReceiverProbe.HashRun());
        using var files = new ResolverFiles(); string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (string name in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll" })
            files.Write(name, File.ReadAllBytes(Path.Combine(runtime, name)));
        using var resolver = files.Open("refs");
        var library = resolver.OpenInput(Path.Combine(files.DirectoryPath("refs"), "System.Private.CoreLib.dll")).Value!;
        var root = MetadataTokens.EntityHandle(typeof(PrimitiveReceiverProbe).GetMethod(nameof(PrimitiveReceiverProbe.HashRun))!.MetadataToken);
        var report = resolver.Audit(resolver.OpenInput(typeof(PrimitiveReceiverProbe).Assembly.Location).Value!, [root], [],
            limits: new(MaximumNodes: 128), runtimeLibrary: library);
        Assert.Equal("incomplete", report.Status);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
        Assert.Contains(report.RuntimeMappings, m => m.Primitive == "Int32");
    }

    private static (byte[] Bytes, MethodDefinitionHandle Body) StringLibrary(bool reference = false)
    {
        var builder = new ResolutionAssemblyBuilder(LibraryId);
        var contract = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        builder.Method("M", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract);
        var text = builder.Type("System", "String", attributes: TypeAttributes.Public | TypeAttributes.Sealed); builder.Implements(text, contract);
        var body = builder.Method("M", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, emit: Return);
        if (reference) builder.MarkReferenceAssembly();
        return (builder.Build(), body);
    }
}

internal static class PrimitiveReceiverProbe
{
    private static int Compare<T>(ref T value, T other) where T : IComparable<T> => value.CompareTo(other);
    public static int Run() { int value = 42; return Compare(ref value, 43); }
    private static int Hash<T>(ref T value) => value!.GetHashCode();
    public static int HashRun() { int value = 42; return Hash(ref value); }
}
