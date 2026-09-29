using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Output;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class ConstrainedDispatchTests
{
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual;
    private static ResolutionAssemblyBuilder Builder() => new(new("Constrained", new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void Token(InstructionEncoder il, ILOpCode code, EntityHandle token) { il.OpCode(code); il.Token(token); }
    private static void Mention(InstructionEncoder il, EntityHandle type) { Token(il, ILOpCode.Ldtoken, type); il.OpCode(ILOpCode.Pop); }
    private static MethodDefinitionHandle Contract(ResolutionAssemblyBuilder builder, out TypeDefinitionHandle type)
    {
        type = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        return builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract);
    }
    private static ReachabilityReport Analyze(ResolutionAssemblyBuilder builder, EntityHandle root, ReachabilityLimits? limits = null)
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        return resolver.AnalyzeReachability(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, [root], limits);
    }
    private static MethodDefinitionHandle[] Targets(ReachabilityReport report) => report.Edges
        .Where(e => e.Kind == DependencyKind.ConservativeDispatch)
        .Select(e => ((ReachableEntity.Method)report.Nodes[e.To].Entity).Definition.Handle).Distinct().ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closed_generic_constraint_selects_only_its_receiver_and_preserves_prefix_evidence(bool explicitBody)
    {
        var builder = Builder(); var declaration = Contract(builder, out var contract);
        var receiver = builder.Type("", "Receiver", attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        builder.Implements(receiver, contract);
        var body = builder.Method(explicitBody ? "Renamed" : "M", [0x20, 0, 1], attributes: explicitBody
            ? MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot
            : Virtual | MethodAttributes.NewSlot, emit: Return);
        if (explicitBody) builder.MethodImplementation(receiver, body, declaration);
        var other = builder.Type("", "Other"); builder.Implements(other, contract);
        var excluded = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry"); var variable = builder.Specification(0x1e, 0);
        var generic = builder.Method("Invoke", [0x10, 1, 1, 1, 0x10, 0x1e, 0], arity: 1, emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, variable); Token(il, ILOpCode.Callvirt, declaration); Return(il);
        });
        var closed = builder.Instantiate(generic, 0x0a, 1, 0x12, 12);
        var root = builder.Method("Root", [0, 0, 1], locals: [7, 2, 0x12, 12, 0x1d, 8], emit: il =>
        {
            il.LoadLocalAddress(0); il.Call(closed); Mention(il, other); Return(il);
        });
        var report = Analyze(builder, root, new(MaximumDispatchPairs: 1));
        Assert.Empty(report.Issues); // Unrelated array local cannot broaden a sealed constrained receiver.
        Assert.Equal(new[] { body }, Targets(report));
        Assert.DoesNotContain(report.Nodes, n => n.Entity is ReachableEntity.Method m && m.Definition.Handle == excluded);
        var edge = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.Equal(edge.ReceiverType, edge.ConstraintType); Assert.NotNull(edge.ConstraintType);
        Assert.Equal(receiver, Assert.IsType<ReachableEntity.Type>(report.Nodes[edge.ConstraintType.Value].Entity).Definition.Definition.Handle);
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConstrainedType && e.To == edge.ConstraintType && e.ILOffset == 1);
        Assert.Equal(7, edge.ILOffset);
    }

    [Fact]
    public void Reference_constraint_keeps_late_derived_overrides_but_excludes_unrelated_implementations()
    {
        var builder = Builder(); var declaration = Contract(builder, out var contract);
        var parent = builder.Type("", "Base"); builder.Implements(parent, contract);
        var child = MetadataTokens.TypeDefinitionHandle(4);
        var baseBody = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot,
            emit: il => { Mention(il, child); Return(il); });
        builder.Type("", "Child", baseType: parent);
        var overrideBody = builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        var unrelated = builder.Type("", "Other"); builder.Implements(unrelated, contract);
        var excluded = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 1, 1, 0x10, 0x12, 12], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, parent); Token(il, ILOpCode.Callvirt, declaration);
            Mention(il, unrelated); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues); Assert.Contains(baseBody, Targets(report)); Assert.Contains(overrideBody, Targets(report));
        Assert.DoesNotContain(excluded, Targets(report));
        Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch && e.ReceiverType != e.ConstraintType);
    }

    [Fact]
    public void Interface_constraint_uses_included_implementors_without_inventing_a_concrete_receiver()
    {
        var builder = Builder(); var declaration = Contract(builder, out var contract);
        var receiver = builder.Type("", "Receiver"); builder.Implements(receiver, contract);
        var body = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 1, 1, 0x10, 0x12, 8], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, contract); Token(il, ILOpCode.Callvirt, declaration);
            Mention(il, receiver); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues); Assert.Equal(new[] { body }, Targets(report));
        var edge = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.NotEqual(edge.ReceiverType, edge.ConstraintType);
    }

    [Fact]
    public void Constructed_constraint_preserves_receiver_and_generic_method_arguments()
    {
        var builder = Builder();
        var contract = builder.Type("", "I`1", arity: 1, attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        builder.Method("M", [0x30, 1, 0, 1], arity: 1, attributes: Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot);
        var receiver = builder.Type("", "Receiver`1", arity: 1, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        var openInterface = builder.Specification(0x15, 0x12, 8, 1, 0x13, 0);
        builder.Implements(receiver, openInterface);
        var body = builder.Method("M", [0x30, 1, 0, 1], arity: 1, attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var closedReceiver = builder.Specification(0x15, 0x12, 12, 1, 8);
        var closedInterface = builder.Specification(0x15, 0x12, 8, 1, 8);
        var call = builder.Instantiate(builder.Member(closedInterface, "M", 0x30, 1, 0, 1), 0x0a, 1, 0x0e);
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); Token(il, ILOpCode.Constrained, closedReceiver); Token(il, ILOpCode.Callvirt, call); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues);
        var target = Assert.Single(report.Nodes.Select(n => n.Entity).OfType<ReachableEntity.Method>(), n => n.Definition.Handle == body).Definition;
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.Int32), Assert.Single(target.DeclaringType.Arguments));
        Assert.Equal(new TypeSignature.Primitive(PrimitiveTypeCode.String), Assert.Single(target.MethodArguments));
    }

    [Fact]
    public void Nonvirtual_operand_is_not_replaced_by_a_same_named_virtual_method()
    {
        var builder = Builder(); var parent = builder.Type("", "Base");
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: MethodAttributes.Public, emit: Return);
        var child = builder.Type("", "Child", baseType: parent);
        var hidden = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 1, 1, 0x10, 0x12, 12], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, child); Token(il, ILOpCode.Callvirt, declaration); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues); Assert.Equal(new[] { declaration }, Targets(report)); Assert.DoesNotContain(hidden, Targets(report));
    }

    [Theory]
    [InlineData(new byte[] { 8 })] // Primitive needs explicit runtime mapping.
    [InlineData(new byte[] { 0x1d, 8 })] // Array needs a synthesized receiver.
    [InlineData(new byte[] { 0x1e, 0 })] // Open method variable.
    public void Unsupported_constraints_never_fall_back_to_unconstrained_dispatch(byte[] signature)
    {
        var builder = Builder(); var declaration = Contract(builder, out var contract);
        var receiver = builder.Type("", "Receiver"); builder.Implements(receiver, contract);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry"); var constraint = builder.Specification(signature);
        var root = builder.Method("Root", [0x10, 1, 0, 1], arity: 1, emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull); Token(il, ILOpCode.Constrained, constraint); Token(il, ILOpCode.Callvirt, declaration);
            Mention(il, receiver); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(Targets(report));
        Assert.Contains(report.Issues, i => i.ILOffset == 1 && i.Diagnostic.Message.Contains("closed named type", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ILOpCode.Call)]
    [InlineData(ILOpCode.Ldftn)]
    public void Static_interface_forms_remain_explicit_gaps(ILOpCode opcode)
    {
        var builder = Builder(); var contract = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = builder.Method("M", [0, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.Virtual | MethodAttributes.Abstract);
        var receiver = builder.Type("", "C"); builder.Implements(receiver, contract);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            Token(il, ILOpCode.Constrained, receiver); Token(il, opcode, declaration);
            if (opcode == ILOpCode.Ldftn) il.OpCode(ILOpCode.Pop);
            Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(Targets(report));
        Assert.Contains(report.Issues, i => i.ILOffset == 0 && i.Diagnostic.Message.Contains("static-interface", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("wrong-opcode")]
    [InlineData("bad-token")]
    [InlineData("static-callvirt")]
    public void Invalid_constrained_sequences_are_diagnosed(string shape)
    {
        var builder = Builder(); var receiver = builder.Type("", "C", attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        var target = builder.Method("M", shape == "static-callvirt" ? [0, 0, 1] : [0x20, 0, 1],
            attributes: shape == "static-callvirt" ? MethodAttributes.Public | MethodAttributes.Static : Virtual, emit: Return);
        var root = builder.Method("Root", [0, 0, 1], emit: il =>
        {
            il.OpCode(ILOpCode.Ldnull);
            Token(il, ILOpCode.Constrained, shape == "bad-token" ? MetadataTokens.TypeDefinitionHandle(99999) : receiver);
            if (shape == "duplicate") Token(il, ILOpCode.Constrained, receiver);
            if (shape == "wrong-opcode") il.OpCode(ILOpCode.Pop);
            else Token(il, ILOpCode.Callvirt, target);
            if (shape == "static-callvirt") il.OpCode(ILOpCode.Pop);
            Return(il);
        });
        var report = Analyze(builder, root);
        Assert.NotEmpty(report.Issues); Assert.Empty(Targets(report));
    }

    [Fact]
    public void Authored_CSharp_probe_preserves_value_aliasing_and_reference_override_targets()
    {
        Assert.Equal(33, ConstrainedProbe.Run()); // Two mutations of the same struct, plus the derived reference override.
        using var files = new ResolverFiles();
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (string file in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll" })
            files.Write(file, File.ReadAllBytes(Path.Combine(runtime, file)));
        using var resolver = files.Open("refs");
        var assembly = resolver.OpenInput(typeof(ConstrainedProbe).Assembly.Location).Value!;
        var root = MetadataTokens.EntityHandle(typeof(ConstrainedProbe).GetMethod(nameof(ConstrainedProbe.Run))!.MetadataToken);
        var report = resolver.AnalyzeReachability(assembly, [root]);
        Assert.Empty(report.Issues);
        foreach (var type in new[] { typeof(ConstrainedValue), typeof(ConstrainedBase), typeof(ConstrainedDerived) })
        {
            int token = type.GetMethod("Next")!.MetadataToken;
            Assert.Contains(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch && e.ConstraintType is not null
                && report.Nodes[e.To].Entity is ReachableEntity.Method m
                && m.Definition.DeclaringType.Definition.Assembly.ContentHash == assembly.ContentHash
                && MetadataTokens.GetToken(m.Definition.Handle) == token);
        }
    }

    [Fact]
    public void Unimplemented_default_interface_body_still_leaves_a_gap_for_a_constrained_receiver()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var receiver = builder.Type("", "C", attributes: TypeAttributes.Public | TypeAttributes.Sealed); builder.Implements(receiver, contract);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 1, 1, 0x10, 0x12, 12], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, receiver); Token(il, ILOpCode.Callvirt, declaration); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(Targets(report));
        Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains("default interface", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inherited_base_body_is_retained_with_constrained_receiver_evidence(bool overridden)
    {
        // Models the method-body closure for a value type inheriting a base virtual
        // method. Boxing choice and object layout are deliberately not lowered here.
        var builder = Builder(); var parent = builder.Type("", "Base");
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        var receiver = builder.Type("", "Value", baseType: parent, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        MethodDefinitionHandle replacement = default;
        if (overridden) replacement = builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 1, 1, 0x10, 0x11, 12], emit: il =>
        {
            il.LoadArgument(0); il.OpCode(ILOpCode.Tail); Token(il, ILOpCode.Constrained, receiver);
            Token(il, ILOpCode.Callvirt, declaration); Return(il);
        });
        var report = Analyze(builder, root);
        Assert.Empty(report.Issues); Assert.Contains(declaration, Targets(report));
        if (overridden) Assert.Contains(replacement, Targets(report));
        Assert.All(report.Edges.Where(e => e.Kind == DependencyKind.ConservativeDispatch), e => Assert.Equal(e.ConstraintType, e.ReceiverType));
    }

    [Fact]
    public void Constrained_receiver_evidence_survives_audit_serialization()
    {
        var builder = Builder(); var declaration = Contract(builder, out var contract);
        var receiver = builder.Type("", "C", attributes: TypeAttributes.Public | TypeAttributes.Sealed); builder.Implements(receiver, contract);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.NewSlot, emit: Return);
        builder.Type("", "Entry");
        var root = builder.Method("Root", [0, 1, 1, 0x10, 0x12, 12], emit: il =>
        {
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, receiver); Token(il, ILOpCode.Callvirt, declaration); Return(il);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, [root], []);
        Assert.Empty(report.Issues);
        Assert.Contains("constraintType", AuditReportWriter.Json(report), StringComparison.Ordinal);
        Assert.Contains("constraint=", AuditReportWriter.Text(report), StringComparison.Ordinal);
        var constrained = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConstrainedType);
        Assert.Contains(constrained.From, report.Roots);
    }
}

internal interface IConstrainedProbe { int Next(); }
internal struct ConstrainedValue : IConstrainedProbe
{
    private int _value;
    public int Next() => ++_value;
}
internal class ConstrainedBase : IConstrainedProbe { public virtual int Next() => 20; }
internal sealed class ConstrainedDerived : ConstrainedBase { public override int Next() => 30; }
internal static class ConstrainedProbe
{
    private static int Apply<T>(ref T value) where T : IConstrainedProbe => value.Next();
    public static int Run()
    {
        var value = new ConstrainedValue();
        ConstrainedBase reference = new ConstrainedDerived();
        return Apply(ref value) + Apply(ref value) + Apply(ref reference);
    }
}
