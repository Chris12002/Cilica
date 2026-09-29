using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Output;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class DispatchSchedulingTests
{
    private const MethodAttributes Virtual = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot;
    private static ResolutionAssemblyBuilder Builder() => new(new("Scheduling", new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void Token(InstructionEncoder il, ILOpCode opcode, EntityHandle handle) { il.OpCode(opcode); il.Token(handle); }
    private static void Mention(InstructionEncoder il, EntityHandle handle) { Token(il, ILOpCode.Ldtoken, handle); il.OpCode(ILOpCode.Pop); }
    private static void Call(InstructionEncoder il, EntityHandle handle) { il.OpCode(ILOpCode.Ldnull); Token(il, ILOpCode.Callvirt, handle); }
    private static void Float(InstructionEncoder il) { il.LoadConstantR8(1.5); il.OpCode(ILOpCode.Pop); Return(il); }

    [Fact]
    public void Dispatch_body_is_scanned_before_an_unrelated_ordinary_chain_exhausts_the_node_budget()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.Abstract);
        var receiver = builder.Type("", "Receiver"); builder.Implements(receiver, contract);
        var body = builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Float);
        builder.Type("", "Pressure");
        var chain = MetadataTokens.MethodDefinitionHandle(3);
        for (int index = 0; index < 40; index++)
        {
            int nextRow = index + 4;
            builder.Method("Step" + index, [0, 0, 1], emit: il =>
            {
                if (nextRow < 43) il.Call(MetadataTokens.MethodDefinitionHandle(nextRow));
                Return(il);
            });
        }
        builder.Type("", "Entry");
        var root = builder.Method("Run", [0, 0, 1], emit: il => { Mention(il, receiver); Call(il, declaration); il.Call(chain); Return(il); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!;
        var report = resolver.Audit(assembly, [root], [], limits: new(MaximumNodes: 24));
        Assert.Equal("incomplete", report.Status);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit && i.Diagnostic.Message.Contains("node limit", StringComparison.Ordinal));
        var target = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.Equal("0x06000002", report.Nodes[target.To].Token);
        Assert.Equal(0x06000002, MetadataTokens.GetToken(body));
        Assert.Contains(report.Features, f => f.Node == target.To && f.Feature == "floating-point");
        Assert.Equal(AuditReportWriter.Json(report), AuditReportWriter.Json(resolver.Audit(assembly, [root, root], [], limits: new(MaximumNodes: 24))));
        AssertReasons(report);
    }

    [Fact]
    public void Ordinary_body_is_scanned_while_many_dispatch_pairs_remain()
    {
        var builder = Builder();
        var owner = builder.Type("", "Base");
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        var receivers = Enumerable.Range(0, 20).Select(i => builder.Type("", "Derived" + i, baseType: owner)).ToArray();
        builder.Type("", "Entry");
        var leaf = builder.Method("Leaf", [0, 0, 1], emit: Float);
        var root = builder.Method("Run", [0, 0, 1], emit: il =>
        {
            il.Call(leaf); Call(il, declaration);
            foreach (var receiver in receivers) Mention(il, receiver);
            Return(il);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, [root], [], limits: new(MaximumDispatchPairs: 3));
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit && i.Diagnostic.Message.Contains("pair limit", StringComparison.Ordinal));
        Assert.Contains(report.Features, f => report.Nodes[f.Node].Name == "Entry::Leaf" && f.Feature == "floating-point");
        AssertReasons(report);
    }

    [Fact]
    public void A_later_exact_site_gets_a_turn_before_the_first_site_consumes_all_pairs()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var declaration = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.Abstract);
        var receiver = builder.Type("", "Receiver", attributes: TypeAttributes.Public | TypeAttributes.Sealed); builder.Implements(receiver, contract);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: Return);
        var unrelated = Enumerable.Range(0, 20).Select(i => builder.Type("", "Unrelated" + i)).ToArray();
        builder.Type("", "Entry");
        // ref Receiver: TypeDef row 3, TypeDefOrRef coded index 12.
        var root = builder.Method("Run", [0, 1, 1, 0x10, 0x12, 12], emit: il =>
        {
            foreach (var type in unrelated) Mention(il, type);
            Call(il, declaration);
            il.LoadArgument(0); Token(il, ILOpCode.Constrained, receiver); Token(il, ILOpCode.Callvirt, declaration); Return(il);
        });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, [root], [], limits: new(MaximumDispatchPairs: 2));
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
        var edge = Assert.Single(report.Edges, e => e.Kind == DependencyKind.ConservativeDispatch);
        Assert.NotNull(edge.ConstraintType); Assert.Equal(edge.ConstraintType, edge.ReceiverType);
        AssertReasons(report);
    }

    [Fact]
    public void A_site_discovered_in_a_target_body_considers_old_receivers_once()
    {
        var builder = Builder();
        var contract = builder.Type("", "I", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var first = builder.Method("M", [0x20, 0, 1], attributes: Virtual | MethodAttributes.Abstract);
        var second = builder.Method("N", [0x20, 0, 1], attributes: Virtual | MethodAttributes.Abstract);
        var receiver = builder.Type("", "Receiver"); builder.Implements(receiver, contract);
        builder.Method("M", [0x20, 0, 1], attributes: Virtual, emit: il => { Call(il, second); Return(il); });
        builder.Method("N", [0x20, 0, 1], attributes: Virtual, emit: Float);
        builder.Type("", "Entry");
        var root = builder.Method("Run", [0, 0, 1], emit: il => { Mention(il, receiver); Call(il, first); Return(il); });
        using var files = new ResolverFiles(); using var resolver = files.Open();
        // Three included types and two sites: exactly six distinct pairs. The
        // second site exists only after the first implementation body is scanned.
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", builder.Build())).Value!, [root], [], limits: new(MaximumDispatchPairs: 6));
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { "Receiver::M", "Receiver::N" }, report.Edges.Where(e => e.Kind == DependencyKind.ConservativeDispatch).Select(e => report.Nodes[e.To].Name));
        Assert.Contains(report.Features, f => f.Feature == "floating-point" && report.Nodes[f.Node].Name == "Receiver::N");
        AssertReasons(report);
    }

    private static void AssertReasons(AuditReport report)
    {
        foreach (var node in report.Nodes)
        {
            var current = node;
            var seen = new System.Collections.Generic.HashSet<int>();
            while (!report.Roots.Contains(current.Id))
            {
                Assert.True(seen.Add(current.Id));
                var edge = report.Edges[current.FirstReason!.Value];
                Assert.Equal(current.Id, edge.To);
                current = report.Nodes[edge.From];
            }
        }
    }
}
