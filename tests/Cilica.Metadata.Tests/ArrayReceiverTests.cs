using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.IO;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Output;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ArrayReceiverTests
{
    private static readonly AssemblyIdentity Library = new("Arrays", new Version(1, 0, 0, 0));
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    private const TypeAttributes Interface = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
    private static ResolutionAssemblyBuilder Builder(string name = "Input") => new(new(name, new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void Token(InstructionEncoder il, ILOpCode code, EntityHandle handle) { il.OpCode(code); il.Token(handle); }
    private static void Call(InstructionEncoder il, EntityHandle handle) { il.OpCode(ILOpCode.Ldnull); Token(il, ILOpCode.Callvirt, handle); }

    private static byte[] LibraryBytes(bool reference = false)
    {
        var builder = new ResolutionAssemblyBuilder(Library);
        var parent = builder.Type("System", "Object");
        builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("N", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract);
        var array = builder.Type("System", "Array", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        builder.Implements(array, contract);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        var body = builder.Method("I.N", [0x20, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, emit: Return);
        builder.MethodImplementation(array, body, declaration);
        builder.Type("System", "Int32", attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        if (reference) builder.MarkReferenceAssembly();
        return builder.Build();
    }

    [Theory]
    [InlineData("parameter")]
    [InlineData("return")]
    [InlineData("field")]
    [InlineData("local")]
    [InlineData("newarr")]
    public void Array_sources_follow_inherited_methods_with_distinct_array_receiver_evidence(string source)
    {
        var input = Builder(); input.Type("", "Entry");
        var scope = input.Reference(Library);
        var parent = input.TypeReference("System", "Object", scope);
        var target = input.Member(parent, "M", 0x20, 0, 1);
        var element = input.TypeReference("System", "Int32", scope);
        var field = source == "field" ? input.FieldWithAttributes("Data", FieldAttributes.Static | FieldAttributes.Public, 6, 0x1d, 8) : default;
        var root = input.Method("Run", source == "parameter" ? [0, 1, 1, 0x1d, 8] : source == "return" ? [0, 0, 0x1d, 8] : [0, 0, 1],
            locals: source == "local" ? [7, 1, 0x1d, 8] : null, emit: il =>
            {
                if (source == "newarr") { il.LoadConstantI4(1); Token(il, ILOpCode.Newarr, element); il.OpCode(ILOpCode.Pop); }
                if (source == "field") { Token(il, ILOpCode.Ldsfld, field); il.OpCode(ILOpCode.Pop); }
                Call(il, target);
                if (source == "return") il.OpCode(ILOpCode.Ldnull);
                Return(il);
            });
        using var files = new ResolverFiles(); string libraryPath = files.Write("library.dll", LibraryBytes());
        using var resolver = files.Open("refs"); var library = resolver.OpenInput(libraryPath).Value!;
        var assembly = resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!;
        var graph = resolver.AnalyzeReachability(assembly, [root], runtimeLibrary: library);
        Assert.Empty(graph.Issues);
        var receiver = Assert.Single(graph.Nodes, n => n.Entity is ReachableEntity.ArrayReceiver);
        Assert.IsType<TypeSignature.Vector>(Assert.IsType<ReachableEntity.ArrayReceiver>(receiver.Entity).Shape);
        var edges = graph.Edges.Where(e => e.Kind == DependencyKind.ConservativeDispatch && e.ReceiverType == receiver.Id).ToArray();
        Assert.Equal(new[] { 0x06000001, 0x06000003 }, edges.Select(e => MetadataTokens.GetToken(Assert.IsType<ReachableEntity.Method>(graph.Nodes[e.To].Entity).Definition.Handle)));
        Assert.Contains(graph.Edges, e => e.From == receiver.Id && e.Kind == DependencyKind.BaseType);
        var report = resolver.Audit(assembly, [root], [], runtimeLibrary: library);
        var node = Assert.Single(report.Nodes, n => n.Kind == "array-receiver");
        Assert.False(node.IsExternalMember); Assert.Contains("array=", node.Instantiation, StringComparison.Ordinal);
        resolver.Dispose();
        Assert.Contains("array-receiver", AuditReportWriter.Json(report), StringComparison.Ordinal);
        Assert.Contains("array-receiver", AuditReportWriter.Text(report), StringComparison.Ordinal);
        Assert.DoesNotContain(files.Root, AuditReportWriter.Json(report), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Base_declared_interface_bodies_are_followed_but_reference_bodies_remain_gaps(bool reference)
    {
        var input = Builder(); input.Type("", "Entry");
        var target = input.Member(input.TypeReference("", "I", input.Reference(Library)), "N", 0x20, 0, 1);
        var root = input.Method("Run", [0, 1, 1, 0x1d, 8], emit: il => { Call(il, target); Return(il); });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", LibraryBytes(reference));
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [], runtimeLibrary: resolver.OpenInput(path).Value!);
        var edge = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.Equal("array-receiver", report.Nodes[edge.ReceiverType!.Value].Kind);
        Assert.Equal("System.Array::I.N", report.Nodes[edge.To].Name);
        if (reference) Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains("Reference-assembly", StringComparison.Ordinal));
        else Assert.Empty(report.Issues);
    }

    [Fact]
    public void Array_shapes_preserve_rank_bounds_element_identity_and_jagged_nesting()
    {
        var input = Builder(); input.Type("", "Entry");
        // Locals: int[], int[,], int[2..4], int[][], and a repeated int[].
        var root = input.Method("Run", [0, 0, 1], locals: [7, 5, 0x1d, 8, 0x14, 8, 2, 0, 0, 0x14, 8, 1, 1, 3, 1, 4, 0x1d, 0x1d, 8, 0x1d, 8], emit: Return);
        using var files = new ResolverFiles(); string path = files.Write("library.dll", LibraryBytes()); using var resolver = files.Open("refs");
        var report = resolver.AnalyzeReachability(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Empty(report.Issues);
        var arrays = report.Nodes.Select(n => n.Entity).OfType<ReachableEntity.ArrayReceiver>().ToArray();
        Assert.Equal(4, arrays.Length);
        Assert.Contains(arrays, a => a.Shape is TypeSignature.Vector { Element: TypeSignature.Vector });
        Assert.Contains(arrays, a => a.Shape is TypeSignature.Array { Rank: 2 });
        var bounded = Assert.Single(arrays, a => a.Shape is TypeSignature.Array { Rank: 1 });
        var shape = Assert.IsType<TypeSignature.Array>(bounded.Shape);
        Assert.Equal(3, Assert.Single(shape.Sizes)); Assert.Equal(2, Assert.Single(shape.LowerBounds));
    }

    [Fact]
    public void Runtime_supplied_generic_interfaces_remain_located_gaps_even_if_another_receiver_supplies_a_body()
    {
        var library = new ResolutionAssemblyBuilder(Library);
        var contract = library.Type("", "I`1", attributes: Interface, arity: 1);
        library.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract);
        library.Type("System", "Array", attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var named = library.Type("", "Named");
        var closed = library.Specification(0x15, 0x12, 8, 1, 8); library.Implements(named, closed);
        library.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var input = Builder(); input.Type("", "Entry");
        var scope = input.Reference(Library); var reference = input.TypeReference("", "I`1", scope);
        Assert.Equal(1, MetadataTokens.GetRowNumber(reference));
        var owner = input.Specification(0x15, 0x12, 5, 1, 8);
        var target = input.Member(owner, "M", 0x20, 0, 1);
        var namedReference = input.TypeReference("", "Named", scope);
        var root = input.Method("Run", [0, 1, 1, 0x1d, 8], emit: il => { Token(il, ILOpCode.Ldtoken, namedReference); il.OpCode(ILOpCode.Pop); Call(il, target); Return(il); });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build()); using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.Contains(report.Issues, i => i.ILOffset is not null && i.Diagnostic.Message.Contains("explicit runtime helper contract", StringComparison.Ordinal));
        Assert.Equal("unresolved", report.GeneralFilterStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_selection_or_missing_array_base_preserves_uncertainty(bool select)
    {
        var library = new ResolutionAssemblyBuilder(Library); library.Type("System", "Object");
        library.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var input = Builder(); input.Type("", "Entry");
        var target = input.Member(input.TypeReference("System", "Object", input.Reference(Library)), "M", 0x20, 0, 1);
        var root = input.Method("Run", [0, 1, 1, 0x1d, 8], emit: il => { Call(il, target); Return(il); });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build()); using var resolver = files.Open("refs");
        var graph = resolver.AnalyzeReachability(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], runtimeLibrary: select ? resolver.OpenInput(path).Value! : null);
        Assert.DoesNotContain(graph.Nodes, n => n.Entity is ReachableEntity.ArrayReceiver);
        Assert.Contains(graph.Issues, i => i.Diagnostic.Message.Contains("receiver synthesis", StringComparison.Ordinal));
        if (select) Assert.Contains(graph.Issues, i => i.Diagnostic.Id == DiagnosticIds.TypeNotFound);
    }

    [Theory]
    [InlineData(TypeAttributes.Public, 0)]
    [InlineData(TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface, 0)]
    [InlineData(TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, 0)]
    [InlineData(TypeAttributes.Public | TypeAttributes.Abstract, 1)]
    public void Invalid_selected_array_base_shapes_are_rejected(TypeAttributes attributes, int arity)
    {
        var library = Builder(); library.Type("System", "Array", attributes: attributes, arity: arity);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var result = resolver.ResolveRuntimeArrayBase(resolver.OpenInput(files.Write("library.dll", library.Build())).Value!);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Selected_facade_forwards_array_base_and_excludes_namesakes_and_conflicting_content()
    {
        var implementation = Builder("Implementation"); var array = implementation.Type("System", "Array", attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var facade = Builder("Facade"); facade.Forward("System", "Array", facade.Reference(new("Implementation", new Version(1, 0, 0, 0))));
        var decoy = Builder("Decoy"); decoy.Type("System", "Array", attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        using var files = new ResolverFiles(); files.Write("implementation.dll", implementation.Build()); files.Write("decoy.dll", decoy.Build());
        string path = files.Write("facade.dll", facade.Build(), "profile");
        using var resolver = files.Open("refs"); var selected = resolver.OpenInput(path).Value!;
        var mapped = resolver.ResolveRuntimeArrayBase(selected).Value!;
        Assert.Equal("Implementation", mapped.Assembly.Identity.Name); Assert.Equal(array, mapped.Handle);
        using var other = files.Open("refs"); Assert.Throws<ArgumentException>(() => other.ResolveRuntimeArrayBase(selected));
        resolver.Dispose(); Assert.Throws<ObjectDisposedException>(() => resolver.ResolveRuntimeArrayBase(selected));
        var conflict = Builder("Facade"); conflict.Type("", "Different"); files.Write("conflict.dll", conflict.Build());
        using var conflicting = files.Open("refs");
        Assert.Equal(DiagnosticIds.AssemblyConflict, Assert.Single(conflicting.ResolveRuntimeArrayBase(conflicting.OpenInput(path).Value!).Diagnostics).Id);
    }

    [Fact]
    public void Open_array_shapes_keep_the_open_environment_gap()
    {
        var input = Builder(); input.Type("", "Entry");
        var root = input.Method("Run", [0x10, 1, 1, 1, 0x1d, 0x1e, 0], arity: 1, emit: Return);
        using var files = new ResolverFiles(); string path = files.Write("library.dll", LibraryBytes()); using var resolver = files.Open("refs");
        var report = resolver.AnalyzeReachability(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.DoesNotContain(report.Nodes, n => n.Entity is ReachableEntity.ArrayReceiver);
        Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains("Open generic", StringComparison.Ordinal));
    }

    [Fact]
    public void Array_intrinsic_member_owners_remain_explicitly_unsupported()
    {
        var input = Builder(); input.Type("", "Entry");
        var owner = input.Specification(0x14, 8, 2, 0, 0);
        var constructor = input.Member(owner, ".ctor", 0x20, 2, 1, 8, 8);
        var root = input.Method("Run", [0, 0, 1], emit: il =>
        {
            il.LoadConstantI4(1); il.LoadConstantI4(1); Token(il, ILOpCode.Newobj, constructor); il.OpCode(ILOpCode.Pop); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", LibraryBytes()); using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ResolutionUnsupported && i.Diagnostic.Message.Contains("intrinsic member owners", StringComparison.Ordinal));
        Assert.Equal("unresolved", report.GeneralFilterStatus);
    }

    [Fact]
    public void Array_discovery_stops_at_the_edge_budget_without_leaving_unexplained_nodes()
    {
        var input = Builder(); input.Type("", "Entry");
        var root = input.Method("Run", [0, 1, 1, 0x1d, 8], emit: Return);
        using var files = new ResolverFiles(); string path = files.Write("library.dll", LibraryBytes()); using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [],
            limits: new(MaximumEdges: 1), runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
        Assert.DoesNotContain(report.Nodes, n => n.Kind == "array-receiver");
        Assert.All(report.Nodes.Where(n => !report.Roots.Contains(n.Id)), n => Assert.Equal(n.Id, report.Edges[n.FirstReason!.Value].To));
    }

    [Fact]
    public void Unrelated_named_constraints_exclude_array_generic_interface_gaps()
    {
        var library = new ResolutionAssemblyBuilder(Library);
        library.Type("", "I`1", arity: 1, attributes: Interface);
        library.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract);
        library.Type("System", "Array", attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var implementation = library.Type("", "C");
        var contract = library.Specification(0x15, 0x12, 8, 1, 8); library.Implements(implementation, contract);
        library.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var input = Builder(); input.Type("", "Entry");
        var scope = input.Reference(Library);
        input.TypeReference("", "I`1", scope);
        var constraint = input.TypeReference("", "C", scope);
        var owner = input.Specification(0x15, 0x12, 5, 1, 8);
        var target = input.Member(owner, "M", 0x20, 0, 1);
        // ref C: TypeRef row 2, coded index 9. An unrelated int[] local
        // must not introduce a possible array receiver for this class constraint.
        var root = input.Method("Run", [0, 1, 1, 0x10, 0x12, 9], locals: [7, 1, 0x1d, 8], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, constraint); Token(il, ILOpCode.Callvirt, target); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build()); using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Empty(report.Issues);
        Assert.Equal("type", report.Nodes[Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch).ReceiverType!.Value].Kind);
    }

    [Fact]
    public void A_generic_interface_constraint_on_a_class_operand_does_not_hide_array_uncertainty()
    {
        var library = new ResolutionAssemblyBuilder(Library);
        var parent = library.Type("System", "Object");
        library.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        library.Type("", "I`1", arity: 1, attributes: Interface);
        library.Type("System", "Array", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        var named = library.Type("", "C", baseType: parent);
        library.Implements(named, library.Specification(0x15, 0x12, 12, 1, 8));
        library.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        var input = Builder(); input.Type("", "Entry");
        var scope = input.Reference(Library); input.TypeReference("", "I`1", scope);
        var parentReference = input.TypeReference("System", "Object", scope);
        var namedReference = input.TypeReference("", "C", scope);
        var constraint = input.Specification(0x15, 0x12, 5, 1, 8);
        var target = input.Member(parentReference, "M", 0x20, 0, 1);
        var root = input.Method("Run", [0, 1, 1, 0x10, 0x15, 0x12, 5, 1, 8], locals: [7, 1, 0x1d, 8], emit: il =>
        {
            Token(il, ILOpCode.Ldtoken, namedReference); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, constraint); Token(il, ILOpCode.Callvirt, target); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build()); using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [], runtimeLibrary: resolver.OpenInput(path).Value!);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch && e.ConstraintType is not null);
        Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains("explicit runtime helper contract", StringComparison.Ordinal));
    }

    [Fact]
    public void Authored_array_probe_reaches_the_non_generic_enumerator_body()
    {
        IEnumerator expected = ArrayReceiverProbe.Enumerator([7, 9]); Assert.True(expected.MoveNext()); Assert.Equal(7, expected.Current);
        using var files = new ResolverFiles(); string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (string name in new[] { "System.Runtime.dll", "System.Collections.dll", "System.Private.CoreLib.dll" }) files.Write(name, File.ReadAllBytes(Path.Combine(runtime, name)));
        using var resolver = files.Open("refs"); var library = resolver.OpenInput(Path.Combine(files.DirectoryPath("refs"), "System.Private.CoreLib.dll")).Value!;
        var assembly = resolver.OpenInput(typeof(ArrayReceiverProbe).Assembly.Location).Value!;
        var root = MetadataTokens.EntityHandle(typeof(ArrayReceiverProbe).GetMethod(nameof(ArrayReceiverProbe.Enumerator))!.MetadataToken);
        var report = resolver.AnalyzeReachability(assembly, [root], new(MaximumNodes: 256), runtimeLibrary: library);
        int token = typeof(Array).GetMethod(nameof(Array.GetEnumerator))!.MetadataToken;
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch
            && report.Nodes[e.ReceiverType!.Value].Entity is ReachableEntity.ArrayReceiver
            && report.Nodes[e.To].Entity is ReachableEntity.Method m && MetadataTokens.GetToken(m.Definition.Handle) == token
            && m.Definition.DeclaringType.Definition.Assembly.ContentHash == library.ContentHash);
        Assert.Empty(report.Issues);
    }
}

internal static class ArrayReceiverProbe
{
    public static IEnumerator Enumerator(int[] values) => ((IEnumerable)values).GetEnumerator();
}
