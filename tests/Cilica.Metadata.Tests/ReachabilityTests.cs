using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ReachabilityTests
{
    private static ResolutionAssemblyBuilder Builder(string name = "Graph") => new(new AssemblyIdentity(name, new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void Operand(InstructionEncoder il, ILOpCode code, EntityHandle token)
    {
        il.OpCode(code);
        il.Token(token);
    }
    private static ResolvedAssembly Open(ResolutionAssemblyBuilder builder, ResolverFiles files, AssemblyResolver resolver) =>
        Assert.IsType<ResolvedAssembly>(resolver.OpenInput(files.Write("graph.dll", builder.Build(), "input")).Value);
    private static MethodDefinitionHandle[] Methods(ReachabilityReport report) => report.Nodes
        .Select(n => n.Entity).OfType<ReachableEntity.Method>().Select(m => m.Definition.Handle).OrderBy(h => MetadataTokens.GetRowNumber(h)).ToArray();
    private static void AssertReasons(ReachabilityReport report)
    {
        foreach (var node in report.Nodes)
        {
            var visited = new HashSet<int>();
            var current = node;
            while (!report.Roots.Contains(current.Id))
            {
                Assert.True(visited.Add(current.Id), "Inclusion reasons must not cycle.");
                Assert.NotNull(current.FirstReason);
                var reason = report.Edges[current.FirstReason.Value];
                Assert.Equal(current.Id, reason.To);
                current = report.Nodes[reason.From];
            }
        }
    }

    [Fact]
    public void Direct_graph_has_exact_methods_initializers_and_paths_but_excludes_disconnected_methods()
    {
        var builder = Builder();
        var moduleInitializer = builder.Method(".cctor", [0, 0, 1], emit: Return);
        builder.Type("", "Entry");
        var leafToken = MetadataTokens.MethodDefinitionHandle(3);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.Call(leafToken); il.OpCode(ILOpCode.Ret); });
        var leaf = builder.Method("Leaf", [0, 0, 1], emit: Return);
        var unused = builder.Method("Unused", [0, 0, 1], emit: Return);
        var initializer = builder.Method(".cctor", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { moduleInitializer, root, leaf, initializer }, Methods(report));
        Assert.DoesNotContain(unused, Methods(report));
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.Call && e.ILOffset == 0);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.TypeInitializer);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ModuleInitializer);
        AssertReasons(report);
    }

    [Fact]
    public void Cyclic_calls_terminate_and_root_order_does_not_change_the_graph()
    {
        var builder = Builder();
        builder.Type("", "C");
        var a = builder.Method("A", [0, 0, 1], emit: il => { il.Call(MetadataTokens.MethodDefinitionHandle(2)); il.OpCode(ILOpCode.Ret); });
        var b = builder.Method("B", [0, 0, 1], emit: il => { il.Call(MetadataTokens.MethodDefinitionHandle(1)); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        byte[] bytes = builder.Build();
        var assembly = resolver.OpenInput(files.Write("graph.dll", bytes, "input")).Value!;
        var first = resolver.AnalyzeReachability(assembly, [a, b]);
        var second = resolver.AnalyzeReachability(assembly, [b, a, b]);
        Assert.Empty(first.Issues);
        Assert.Equal(first.Nodes, second.Nodes);
        Assert.Equal(first.Edges, second.Edges);
        Assert.Equal(first.Roots, second.Roots);
        AssertReasons(first);
        var copy = resolver.OpenInput(files.Write("copy.dll", bytes, "another-path")).Value!;
        var copied = resolver.AnalyzeReachability(copy, [b, a]);
        Assert.Equal(first.Nodes, copied.Nodes);
        Assert.Equal(first.Edges, copied.Edges);
    }

    [Fact]
    public void Constructors_fields_locals_and_type_operands_add_their_dependencies()
    {
        var builder = Builder();
        builder.Type("", "Entry");
        var field = builder.FieldWithAttributes("State", FieldAttributes.Static | FieldAttributes.Public, 6, 8);
        var ctor = MetadataTokens.MethodDefinitionHandle(2);
        var function = MetadataTokens.MethodDefinitionHandle(3);
        var box = MetadataTokens.TypeDefinitionHandle(3);
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            Operand(il, ILOpCode.Newobj, ctor); il.OpCode(ILOpCode.Pop);
            Operand(il, ILOpCode.Ldsfld, field); il.OpCode(ILOpCode.Pop);
            Operand(il, ILOpCode.Ldtoken, box); il.OpCode(ILOpCode.Pop);
            Operand(il, ILOpCode.Ldftn, function); il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ret);
        }, locals: [7, 1, 0x45, 0x12, 12]);
        builder.Type("", "Box");
        builder.Method(".ctor", [0x20, 0, 1], attributes: MethodAttributes.Public, emit: Return);
        builder.Method("Function", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { root, ctor, function }, Methods(report));
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.Constructor);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.FieldAccess);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.MetadataToken);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.Local);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.MethodAddress);
        AssertReasons(report);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generic_call_context_reaches_the_right_type_and_method_instances(bool swap)
    {
        var builder = Builder();
        builder.Type("", "C`1", arity: 1);
        // C<T>.Relay<U>() calls C<T>.Sink<U>() or C<U>.Sink<T>().
        var owner = builder.Specification(0x15, 0x12, 8, 1, swap ? (byte)0x1e : (byte)0x13, 0);
        var sinkReference = builder.Member(owner, "Sink", 0x10, 1, 0, 1);
        var sinkInstance = builder.Instantiate(sinkReference, 0x0a, 1, swap ? (byte)0x13 : (byte)0x1e, 0);
        var field = builder.Field("Value", 6, 0x13, 0);
        builder.Method("Relay", [0x10, 1, 0, 1], arity: 1, emit: il => { il.Call(sinkInstance); il.OpCode(ILOpCode.Ret); });
        var sink = builder.Method("Sink", [0x10, 1, 0, 1], arity: 1, emit: il =>
        {
            // ldtoken follows the definition in the constructed declaring context.
            Operand(il, ILOpCode.Ldtoken, field); il.OpCode(ILOpCode.Pop); il.OpCode(ILOpCode.Ret);
        });
        builder.Type("", "Entry");
        var closedOwner = builder.Specification(0x15, 0x12, 8, 1, 8);
        var relay = builder.Member(closedOwner, "Relay", 0x10, 1, 0, 1);
        var relayInstance = builder.Instantiate(relay, 0x0a, 1, 0x0e);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.Call(relayInstance); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Empty(report.Issues);
        var target = Assert.Single(report.Nodes.Select(n => n.Entity).OfType<ReachableEntity.Method>(), m => m.Definition.Handle == sink).Definition;
        Assert.Equal(new TypeSignature.Primitive(swap ? PrimitiveTypeCode.String : PrimitiveTypeCode.Int32), Assert.Single(target.DeclaringType.Arguments));
        Assert.Equal(new TypeSignature.Primitive(swap ? PrimitiveTypeCode.Int32 : PrimitiveTypeCode.String), Assert.Single(target.MethodArguments));
        var resolvedField = Assert.Single(report.Nodes.Select(n => n.Entity).OfType<ReachableEntity.Field>(),
            f => f.Definition.DeclaringType == target.DeclaringType).Definition;
        Assert.Equal(new TypeSignature.Primitive(swap ? PrimitiveTypeCode.String : PrimitiveTypeCode.Int32), resolvedField.Signature);
        AssertReasons(report);
    }

    [Fact]
    public void Generic_recursion_stops_at_the_instance_limit_with_a_path()
    {
        var builder = Builder();
        var owner = builder.Type("", "C");
        var recurse = builder.Member(owner, "Grow", 0x10, 1, 0, 1);
        var growing = builder.Instantiate(recurse, 0x0a, 1, 0x1d, 0x1e, 0);
        builder.Method("Grow", [0x10, 1, 0, 1], arity: 1, emit: il => { il.Call(growing); il.OpCode(ILOpCode.Ret); });
        var start = builder.Instantiate(recurse, 0x0a, 1, 8);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.Call(start); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root], new(MaximumMethodInstances: 3));
        Assert.Equal(4, Methods(report).Length);
        var issue = Assert.Single(report.Issues);
        Assert.Equal(DiagnosticIds.ReachabilityLimit, issue.Diagnostic.Id);
        Assert.NotNull(issue.Node);
        Assert.Equal(0, issue.ILOffset);
        AssertReasons(report);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Graph_limits_preserve_reasons_for_all_returned_nodes(bool nodes)
    {
        var builder = Builder();
        builder.Type("", "C");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.Call(MetadataTokens.MethodDefinitionHandle(2)); il.OpCode(ILOpCode.Ret); });
        builder.Method("Leaf", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root], nodes ? new(MaximumNodes: 2) : new(MaximumEdges: 1));
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
        AssertReasons(report);
        Assert.All(report.Edges, e => { Assert.InRange(e.From, 0, report.Nodes.Length - 1); Assert.InRange(e.To, 0, report.Nodes.Length - 1); });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reference_and_runtime_methods_leave_visible_gaps(bool reference)
    {
        var builder = Builder();
        builder.Type("", "C");
        var root = builder.Method("Root", [0, 0, 1], emit: reference ? Return : null);
        if (reference) builder.MarkReferenceAssembly();
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.True(report.HasUnresolvedDependencies);
        Assert.Equal(DiagnosticIds.ReachabilityIncomplete, Assert.Single(report.Issues).Diagnostic.Id);
        AssertReasons(report);
    }

    [Fact]
    public void Missing_call_dependency_does_not_hide_other_reachable_calls()
    {
        var builder = Builder();
        builder.Type("", "C");
        var missing = builder.Reference(new AssemblyIdentity("Missing", new Version(1, 0, 0, 0)));
        var absentType = builder.TypeReference("", "Absent", missing);
        var absentMethod = builder.Member(absentType, "M", 0, 0, 1);
        var leaf = MetadataTokens.MethodDefinitionHandle(2);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.Call(absentMethod); il.Call(leaf); il.OpCode(ILOpCode.Ret); });
        builder.Method("Leaf", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Contains(leaf, Methods(report));
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.AssemblyNotFound && i.ILOffset == 0 && i.Node == report.Roots[0]);
        AssertReasons(report);
    }

    [Fact]
    public void Ordinary_virtual_calls_expand_known_concrete_receivers()
    {
        var builder = Builder();
        builder.Type("", "C");
        var target = builder.Method("Virtual", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, emit: Return);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.OpCode(ILOpCode.Ldnull); Operand(il, ILOpCode.Callvirt, target); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Contains(target, Methods(report));
        Assert.Empty(report.Issues);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch && e.ILOffset == 1);
    }

    [Fact]
    public void Explicit_type_roots_and_public_surface_roots_have_different_documented_scopes()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var publicMethod = builder.Method("Public", [0, 0, 1], emit: Return);
        var privateMethod = builder.Method("Private", [0, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Static, emit: Return);
        builder.Type("", "Hidden", attributes: TypeAttributes.NotPublic);
        var hiddenMethod = builder.Method("HiddenPublic", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        var explicitType = resolver.AnalyzeReachability(assembly, [type]);
        Assert.Empty(explicitType.Issues);
        Assert.Equal(new[] { publicMethod, privateMethod }, Methods(explicitType));
        var surface = resolver.AnalyzePublicSurface(assembly);
        Assert.Empty(surface.Issues);
        Assert.Equal(new[] { publicMethod }, Methods(surface));
        Assert.DoesNotContain(hiddenMethod, Methods(surface));
        AssertReasons(surface);
    }

    [Fact]
    public void Invalid_root_and_malformed_instruction_tokens_are_diagnostics()
    {
        var builder = Builder();
        builder.Type("", "C");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.OpCode(ILOpCode.Call); il.CodeBuilder.WriteInt32(unchecked((int)0x86000001)); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = Open(builder, files, resolver);
        var report = resolver.AnalyzeReachability(assembly, [root, MetadataTokens.MethodDefinitionHandle(9999)]);
        Assert.Contains(report.Issues, i => i.Node is null);
        Assert.Contains(report.Issues, i => i.ILOffset == 0);
        AssertReasons(report);
        Assert.True(resolver.AnalyzeReachability(assembly, []).HasUnresolvedDependencies);
        Assert.True(resolver.AnalyzeReachability(assembly, [default]).HasUnresolvedDependencies);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Body_byte_limits_leave_an_explicit_gap(bool total)
    {
        var builder = Builder();
        builder.Type("", "C");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.OpCode(ILOpCode.Nop); il.OpCode(ILOpCode.Ret); });
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root],
            total ? new(MaximumTotalBodyBytes: 1) : new(MaximumBodyBytes: 1));
        Assert.Equal(DiagnosticIds.ReachabilityLimit, Assert.Single(report.Issues).Diagnostic.Id);
        AssertReasons(report);
    }

    [Fact]
    public void Catch_type_and_interface_declarations_are_dependencies()
    {
        var builder = Builder();
        var entry = builder.Type("", "C");
        var catchType = MetadataTokens.TypeDefinitionHandle(3);
        var contract = MetadataTokens.TypeDefinitionHandle(4);
        builder.Implements(entry, contract);
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            var start = il.DefineLabel(); var handler = il.DefineLabel(); var end = il.DefineLabel();
            il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Throw);
            il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.Branch(ILOpCode.Leave_s, end);
            il.MarkLabel(end); il.OpCode(ILOpCode.Ret);
            il.ControlFlowBuilder!.AddCatchRegion(start, handler, handler, end, catchType);
        });
        builder.Type("", "Catch");
        builder.Type("", "IContract", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Empty(report.Issues);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.CatchType && e.ILOffset == 2);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.Interface);
        AssertReasons(report);
    }

    [Fact]
    public void Authored_CSharp_calls_match_the_expected_dotnet_fixture_path()
    {
        Assert.Equal(7, ReachabilityProbe.Run(3));
        var root = MetadataTokens.EntityHandle(typeof(ReachabilityProbe).GetMethod(nameof(ReachabilityProbe.Run))!.MetadataToken);
        using var resolver = AssemblyResolver.Create([]).Value!;
        var assembly = resolver.OpenInput(typeof(ReachabilityProbe).Assembly.Location).Value!;
        var report = resolver.AnalyzeReachability(assembly, [root]);
        var expected = typeof(ReachabilityProbe).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name is "Run" or "Leaf").Select(m => MetadataTokens.MethodDefinitionHandle(m.MetadataToken & 0x00ffffff)).OrderBy(h => MetadataTokens.GetRowNumber(h));
        Assert.Equal(expected, Methods(report));
        // The deliberately empty reference catalogue cannot resolve System.Object's
        // assembly. Its missing base-type dependency must remain visible.
        Assert.NotEmpty(report.Issues);
        Assert.All(report.Issues, i => Assert.Equal(DiagnosticIds.AssemblyNotFound, i.Diagnostic.Id));
        AssertReasons(report);
    }

    [Fact]
    public void Invalid_stack_and_local_signatures_cannot_look_like_complete_graphs()
    {
        var builder = Builder();
        builder.Type("", "C");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { il.OpCode(ILOpCode.Pop); il.OpCode(ILOpCode.Ret); }, locals: [7, 1]);
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), [root]);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.MalformedEntity);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.InconsistentStack);
        AssertReasons(report);
    }

    [Fact]
    public void Seeded_body_corruption_stays_inside_the_graph_diagnostic_boundary()
    {
        var builder = Builder();
        builder.Type("", "C");
        var roots = new List<EntityHandle>();
        var random = new Random(2003);
        for (int i = 0; i < Fuzz.Iterations; i++)
        {
            byte[] bytes = new byte[random.Next(1, 48)];
            random.NextBytes(bytes);
            roots.Add(builder.Method("M" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), [0, 0, 1], emit: il => il.CodeBuilder.WriteBytes(bytes)));
        }
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var report = resolver.AnalyzeReachability(Open(builder, files, resolver), roots);
        Assert.NotEmpty(report.Issues);
        Assert.All(report.Issues, i => Assert.Equal(DiagnosticSeverity.Error, i.Diagnostic.Severity));
        AssertReasons(report);
    }
}

internal static class ReachabilityProbe
{
    public static int Run(int value) => Leaf(value) + 1;
    private static int Leaf(int value) => value * 2;
    private static int Unused() => 99;
}
