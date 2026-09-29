using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DispatchTests
{
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    private const MethodAttributes Declaration = Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract;
    private const TypeAttributes Interface = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
    private static ResolutionAssemblyBuilder Builder() => new(new AssemblyIdentity("Dispatch", new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void Token(InstructionEncoder il, ILOpCode opcode, EntityHandle token) { il.OpCode(opcode); il.Token(token); }
    private static void Mention(InstructionEncoder il, EntityHandle token) { Token(il, ILOpCode.Ldtoken, token); il.OpCode(ILOpCode.Pop); }
    private static void Call(InstructionEncoder il, EntityHandle method) { il.OpCode(ILOpCode.Ldnull); Token(il, ILOpCode.Callvirt, method); }
    private static ReachabilityReport Analyze(ResolutionAssemblyBuilder builder, EntityHandle root, ReachabilityLimits? limits = null)
    {
        using var files = new ResolverFiles();
        using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("dispatch.dll", builder.Build())).Value!;
        var report = resolver.AnalyzeReachability(assembly, [root], limits);
        foreach (ReachabilityNode node in report.Nodes)
        {
            var current = node;
            var seen = new System.Collections.Generic.HashSet<int>();
            while (!report.Roots.Contains(current.Id))
            {
                Assert.True(seen.Add(current.Id));
                Assert.NotNull(current.FirstReason);
                var reason = report.Edges[current.FirstReason.Value];
                Assert.Equal(current.Id, reason.To);
                current = report.Nodes[reason.From];
            }
        }
        return report;
    }
    private static MethodDefinitionHandle[] Targets(ReachabilityReport report) => report.Edges
        .Where(e => e.Kind == DependencyKind.ConservativeDispatch)
        .Select(e => ((ReachableEntity.Method)report.Nodes[e.To].Entity).Definition.Handle).Distinct().ToArray();

    [Fact]
    public void Interface_dispatch_reaches_a_fixed_point_when_a_target_body_discovers_another_receiver()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Declaration);
        var first = builder.Type("", "First");
        builder.Implements(first, contract);
        var late = MetadataTokens.TypeDefinitionHandle(4);
        var firstBody = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot,
            emit: il => { Mention(il, late); il.OpCode(ILOpCode.Ret); });
        builder.Type("", "Late");
        builder.Implements(late, contract);
        var lateBody = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot,
            emit: il => { il.Call(MetadataTokens.MethodDefinitionHandle(4)); il.OpCode(ILOpCode.Ret); });
        var leaf = builder.Method("Leaf", [0, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Static, emit: Return);
        var unrelated = builder.Type("", "Unrelated");
        var excluded = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            Call(il, declaration); Mention(il, first); Mention(il, unrelated); il.OpCode(ILOpCode.Ret);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { firstBody, lateBody }, Targets(report));
        Assert.Contains(report.Nodes, n => n.Entity is ReachableEntity.Method m && m.Definition.Handle == leaf);
        Assert.DoesNotContain(report.Nodes, n => n.Entity is ReachableEntity.Method m && m.Definition.Handle == excluded);
        Assert.Contains(report.Nodes, n => n.Entity is ReachableEntity.Method m && m.Definition.Handle == declaration);
        Assert.All(report.Edges.Where(e => e.Kind == DependencyKind.ConservativeDispatch), e =>
        {
            Assert.NotNull(e.ReceiverType);
            Assert.IsType<ReachableEntity.Type>(report.Nodes[e.ReceiverType.Value].Entity);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_interface_mapping_follows_a_differently_named_private_body(bool receiverFirst)
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Declaration);
        var implementation = builder.Type("", "Implementation");
        builder.Implements(implementation, contract);
        var body = builder.Method("I.M", [0x20, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, emit: Return);
        builder.MethodImplementation(implementation, body, declaration);
        var excluded = builder.Method("M", [0x20, 0, 1], attributes: MethodAttributes.Private, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            if (receiverFirst) Mention(il, implementation);
            Call(il, declaration);
            if (!receiverFirst) Mention(il, implementation);
            il.OpCode(ILOpCode.Ret);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { body }, Targets(report));
        Assert.DoesNotContain(report.Nodes, n => n.Entity is ReachableEntity.Method m && m.Definition.Handle == excluded);
    }

    [Fact]
    public void Newslot_hides_only_the_new_slot_and_does_not_override_the_original()
    {
        var builder = Builder();
        var parent = builder.Type("", "Base");
        var original = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var middle = builder.Type("", "Middle", baseType: parent);
        var overridden = builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        var hiding = builder.Type("", "Hiding", baseType: middle);
        var newSlot = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var last = builder.Type("", "Last", baseType: hiding);
        var newOverride = builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, original); Mention(il, last); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues);
        Assert.Contains(original, Targets(report));
        Assert.Contains(overridden, Targets(report));
        Assert.DoesNotContain(newSlot, Targets(report));
        Assert.DoesNotContain(newOverride, Targets(report));
    }

    [Fact]
    public void Generic_interface_and_method_arguments_are_preserved_in_explicit_bodies()
    {
        var builder = Builder();
        var contract = builder.Type("", "I`1", arity: 1, attributes: Interface);
        var declaration = builder.Method("M", [0x30, 1, 0, 1], arity: 1, attributes: Declaration);
        var implementation = builder.Type("", "Implementation`1", arity: 1);
        var openInterface = builder.Specification(0x15, 0x12, 8, 1, 0x13, 0);
        builder.Implements(implementation, openInterface);
        var mappedDeclaration = builder.Member(openInterface, "M", 0x30, 1, 0, 1);
        var body = builder.Method("I.M", [0x30, 1, 0, 1], arity: 1,
            attributes: MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, emit: Return);
        builder.MethodImplementation(implementation, body, mappedDeclaration);
        builder.Type("", "Entry");
        var closedInterface = builder.Specification(0x15, 0x12, 8, 1, 8);
        var call = builder.Instantiate(builder.Member(closedInterface, "M", 0x30, 1, 0, 1), 0x0a, 1, 0x0e);
        var receiver = builder.Specification(0x15, 0x12, 12, 1, 8);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, call); Mention(il, receiver); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        // The string method argument now records its implicit receiver gap unless
        // a runtime library is selected; declaration/instance binding still succeeds.
        Assert.Contains("Array/string receiver synthesis", Assert.Single(report.Issues).Diagnostic.Message, StringComparison.Ordinal);
        var target = Assert.Single(report.Nodes.Select(n => n.Entity).OfType<ReachableEntity.Method>(), m => m.Definition.Handle == body).Definition;
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), Assert.Single(target.DeclaringType.Arguments));
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.String), Assert.Single(target.MethodArguments));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_and_default_interface_implementations_remain_explicit_gaps(bool defaultBody)
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: defaultBody ? Virtual | MethodAttributes.NewSlot : Declaration,
            emit: defaultBody ? Return : null);
        var receiver = builder.Type("", "C");
        builder.Implements(receiver, contract);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, declaration); Mention(il, receiver); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.True(report.HasUnresolvedDependencies);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ResolutionUnsupported);
        Assert.Empty(Targets(report));
    }

    [Fact]
    public void Dispatch_pair_budget_stops_with_an_explicit_issue()
    {
        var builder = Builder();
        var type = builder.Type("", "C");
        var method = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, method); Mention(il, type); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root, new(MaximumDispatchPairs: 1));
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit && i.ILOffset == 1);
    }

    [Fact]
    public void Declaration_only_abstract_method_does_not_hide_a_later_direct_call_gap()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Declaration);
        var receiver = builder.Type("", "C");
        builder.Implements(receiver, contract);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); il.Call(declaration); il.OpCode(ILOpCode.Ret);
        });
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, declaration); Mention(il, receiver); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.Contains(report.Issues, i => i.Node is int id && report.Nodes[id].Entity is ReachableEntity.Method m
            && m.Definition.Handle == declaration && i.Diagnostic.Id == DiagnosticIds.ReachabilityIncomplete);
    }

    [Fact]
    public void Explicit_implementation_in_a_base_class_is_inherited_by_a_late_receiver()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Declaration);
        var parent = builder.Type("", "Base", attributes: TypeAttributes.Public | TypeAttributes.Abstract);
        builder.Implements(parent, contract);
        var body = builder.Method("Renamed", [0x20, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, emit: Return);
        builder.MethodImplementation(parent, body, declaration);
        var child = builder.Type("", "Child", baseType: parent);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, declaration); Mention(il, child); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { body }, Targets(report));
    }

    [Fact]
    public void Generic_virtual_overrides_compare_open_method_parameters_before_substitution()
    {
        var builder = Builder();
        var parent = builder.Type("", "Base");
        var declaration = builder.Method("M", [0x30, 1, 1, 1, 0x1e, 0], arity: 1, attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var child = builder.Type("", "Child", baseType: parent);
        var matching = builder.Method("M", [0x30, 1, 1, 1, 0x1e, 0], arity: 1, attributes: Virtual, emit: Return);
        var different = builder.Method("M", [0x30, 1, 1, 1, 8], arity: 1, attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var instantiated = builder.Instantiate(declaration, 0x0a, 1, 8);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); il.LoadConstantI4(1); Token(il, ILOpCode.Callvirt, instantiated);
            Mention(il, child); il.OpCode(ILOpCode.Ret);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues);
        Assert.Contains(matching, Targets(report));
        Assert.DoesNotContain(different, Targets(report));
    }

    [Fact]
    public void Class_MethodImpl_remapping_is_reported_instead_of_guessed()
    {
        var builder = Builder();
        var parent = builder.Type("", "Base");
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var child = builder.Type("", "Child", baseType: parent);
        var body = builder.Method("Renamed", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.MethodImplementation(child, body, declaration);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, declaration); Mention(il, child); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ResolutionUnsupported && i.ILOffset == 1);
    }

    [Fact]
    public void Array_receivers_do_not_silently_disappear_from_dispatch_analysis()
    {
        var builder = Builder();
        builder.Type("", "C");
        var method = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, method); il.OpCode(ILOpCode.Ret); }, locals: [7, 1, 0x1d, 8]);
        var report = Analyze(builder, root);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityIncomplete
            && i.Diagnostic.Message.Contains("receiver synthesis", StringComparison.Ordinal));
    }

    [Fact]
    public void Authored_CSharp_dispatch_targets_agree_with_dotnet_execution()
    {
        Assert.Equal(50, DispatchProbe.Run());
        string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var files = new ResolverFiles();
        // These two identities are explicit test inputs, not resolver probing. No input
        // assembly is loaded or executed; only this authored fixture runs on .NET.
        foreach (string file in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll" })
            files.Write(file, File.ReadAllBytes(Path.Combine(runtimeDirectory, file)));
        using var resolver = files.Open("refs");
        var assembly = resolver.OpenInput(typeof(DispatchProbe).Assembly.Location).Value!;
        var root = MetadataTokens.EntityHandle(typeof(DispatchProbe).GetMethod(nameof(DispatchProbe.Run))!.MetadataToken);
        var report = resolver.AnalyzeReachability(assembly, [root]);
        Assert.Empty(report.Issues);
        var expected = typeof(DispatchDerived).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Read" || m.Name.EndsWith(".ReadInterface", StringComparison.Ordinal))
            .Select(m => MetadataTokens.MethodDefinitionHandle(m.MetadataToken & 0x00ffffff)).ToArray();
        Assert.Equal(2, expected.Length);
        foreach (var method in expected) Assert.Contains(method, Targets(report));
    }

    [Fact]
    public void Variant_interface_conversion_remains_a_visible_gap()
    {
        var builder = Builder();
        builder.Type("", "I`1", arity: 1, attributes: Interface, genericAttributes: GenericParameterAttributes.Covariant);
        builder.Method("M", [0x20, 0, 1], attributes: Declaration);
        var receiver = builder.Type("", "C");
        var source = builder.Specification(0x15, 0x12, 8, 1, 0x0e);
        var target = builder.Specification(0x15, 0x12, 8, 1, 0x1c);
        builder.Implements(receiver, source);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var call = builder.Member(target, "M", 0x20, 0, 1);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, call); Mention(il, receiver); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ResolutionUnsupported
            && i.Diagnostic.Message.Contains("Variant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Interface_ancestry_handles_inheritance_and_diagnoses_cycles(bool cycle)
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: Interface);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Declaration);
        if (cycle) builder.Implements(contract, contract);
        var child = builder.Type("", "IChild", attributes: Interface);
        builder.Implements(child, contract);
        var receiver = builder.Type("", "C");
        builder.Implements(receiver, child);
        var implementation = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, declaration); Mention(il, receiver); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        if (cycle) Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.MalformedEntity);
        else
        {
            Assert.Empty(report.Issues);
            Assert.Equal(new[] { implementation }, Targets(report));
        }
    }

    [Fact]
    public void An_override_of_a_final_slot_is_diagnosed()
    {
        var builder = Builder();
        var parent = builder.Type("", "Base");
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot | MethodAttributes.Final, emit: Return);
        var child = builder.Type("", "Child", baseType: parent);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il => { Call(il, declaration); Mention(il, child); il.OpCode(ILOpCode.Ret); });
        var report = Analyze(builder, root);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.MalformedEntity
            && i.Diagnostic.Message.Contains("final", StringComparison.Ordinal));
    }
}

internal interface IDispatchProbe { int ReadInterface(); }
internal class DispatchBase { public virtual int Read() => 10; }
internal sealed class DispatchDerived : DispatchBase, IDispatchProbe
{
    public override int Read() => 20;
    int IDispatchProbe.ReadInterface() => 30;
}
internal static class DispatchProbe
{
    public static int Run()
    {
        DispatchBase instance = new DispatchDerived();
        return instance.Read() + ((IDispatchProbe)instance).ReadInterface();
    }
}
