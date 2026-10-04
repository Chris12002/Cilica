using System;
using System.Globalization;
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

public sealed class LibraryContractTests
{
    private static AssemblyIdentity Identity(string name) => new(name, new Version(1, 0, 0, 0));
    private static ResolutionAssemblyBuilder Builder(string name) => new(Identity(name));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);
    private static void GeneralFilter(InstructionEncoder il)
    {
        var start = il.DefineLabel(); var filter = il.DefineLabel(); var handler = il.DefineLabel(); var end = il.DefineLabel();
        il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Throw);
        il.MarkLabel(filter); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); il.LoadConstantI4(1);
        il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Endfilter);
        il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.Branch(ILOpCode.Leave_s, end);
        il.MarkLabel(end); Return(il);
        il.ControlFlowBuilder!.AddFilterRegion(start, filter, handler, end, filter);
    }

    private static (byte[] Bytes, MethodDefinitionHandle Root) Caller(string library = "Library")
    {
        var input = Builder("Input"); input.Type("", "Entry");
        var reference = input.TypeReference("", "Api", input.Reference(Identity(library)));
        var member = input.Member(reference, "Use", 0, 0, 1);
        var root = input.Method("Run", [0, 0, 1], emit: il => { il.Call(member); Return(il); });
        return (input.Build(), root);
    }

    [Fact]
    public void Boundary_retains_declarations_and_reasons_but_defers_bodies_layout_and_initializers()
    {
        var library = Builder("Library");
        library.Method(".cctor", [0, 0, 1], emit: GeneralFilter); // module initializer
        library.Type("", "Api");
        library.Field("ImplementationDetail", 6, 0x12, 12); // TypeDef 3
        library.Method(".cctor", [0, 0, 1], emit: GeneralFilter);
        library.Method("Use", [0, 0, 1], emit: GeneralFilter);
        library.Type("", "HiddenLayout");
        var caller = Caller();
        using var files = new ResolverFiles();
        string libraryPath = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var input = resolver.OpenInput(files.Write("input.dll", caller.Bytes, "input")).Value!;
        var contract = resolver.OpenInput(libraryPath).Value!;
        var ordinary = resolver.Audit(input, [caller.Root], []);
        Assert.Equal("observed", ordinary.GeneralFilterStatus);
        Assert.Contains(ordinary.Nodes, n => n.Name == "HiddenLayout");
        var inventory = resolver.Audit(input, [caller.Root], [], contractAssemblies: [contract]);
        Assert.Empty(inventory.Issues);
        Assert.Equal("contract-inventory", inventory.Status);
        Assert.Equal("unresolved", inventory.GeneralFilterStatus);
        Assert.Equal(AuditReport.ContractProfile, inventory.AnalysisProfile);
        Assert.Empty(inventory.Features);
        Assert.DoesNotContain(inventory.Nodes, n => n.Name.Contains(".cctor", StringComparison.Ordinal) || n.Name == "HiddenLayout");
        Assert.Equal(new[] { "Api", "Api::Use" }, inventory.DeferredNodes.Select(id => inventory.Nodes[id].Name).Order(StringComparer.Ordinal));
        var method = Assert.Single(inventory.Nodes, n => n.IsExternalMember);
        var edge = inventory.Edges[method.FirstReason!.Value];
        Assert.Equal(DependencyKind.Call, edge.Kind);
        Assert.Contains(edge.From, inventory.Roots);
        Assert.True(resolver.AnalyzeReachability(input, [caller.Root], contractAssemblies: [contract]).HasUnresolvedDependencies);
        Assert.Equal(AuditReportWriter.Json(ordinary), AuditReportWriter.Json(resolver.Audit(input, [caller.Root], [], contractAssemblies: [])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_boundaries_allow_reference_or_bodyless_declarations_without_claiming_implementation(bool reference)
    {
        var library = Builder("Library"); library.Type("", "Api");
        library.Method("Use", [0, 0, 1]);
        if (reference) library.MarkReferenceAssembly();
        var caller = Caller();
        using var files = new ResolverFiles();
        string path = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var input = resolver.OpenInput(files.Write("input.dll", caller.Bytes, "input")).Value!;
        var contract = resolver.OpenInput(path).Value!;
        Assert.Equal("incomplete", resolver.Audit(input, [caller.Root], []).Status);
        var report = resolver.Audit(input, [caller.Root], [], contractAssemblies: [contract]);
        Assert.Empty(report.Issues);
        Assert.Equal("contract-inventory", report.Status);
        Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Equal(reference, report.Assemblies[Assert.Single(report.ContractAssemblies)].IsReferenceAssembly);
    }

    [Fact]
    public void Same_identity_with_different_content_does_not_defer_the_bound_image()
    {
        var library = Builder("Library"); library.Type("", "Api"); library.Method("Use", [0, 0, 1], emit: GeneralFilter);
        var other = Builder("Library"); other.Type("", "Api"); other.Method("Use", [0, 0, 1], emit: Return);
        var caller = Caller();
        using var files = new ResolverFiles(); files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var input = resolver.OpenInput(files.Write("input.dll", caller.Bytes, "input")).Value!;
        var selected = resolver.OpenInput(files.Write("other.dll", other.Build(), "other")).Value!;
        var report = resolver.Audit(input, [caller.Root], [], contractAssemblies: [selected]);
        Assert.Empty(report.Issues);
        Assert.Empty(report.DeferredNodes);
        Assert.Equal("observed", report.GeneralFilterStatus);
        Assert.Equal(3, report.Assemblies.Length);
        Assert.NotEqual(Assert.Single(report.ContractAssemblies), Assert.Single(report.Nodes, n => n.IsExternalMember).Assembly);
    }

    [Fact]
    public void Closed_method_instances_and_their_signature_types_remain_distinct()
    {
        var library = Builder("Library"); library.Type("", "Api");
        library.Method("Identity", [0x10, 1, 1, 0x1e, 0, 0x1e, 0], arity: 1);
        var input = Builder("Input"); input.Type("", "Payload"); input.Type("", "Entry");
        var api = input.TypeReference("", "Api", input.Reference(Identity("Library")));
        var method = input.Member(api, "Identity", 0x10, 1, 1, 0x1e, 0, 0x1e, 0);
        var integer = input.Instantiate(method, 0x0a, 1, 8);
        var payload = input.Instantiate(method, 0x0a, 1, 0x12, 8);
        var root = input.Method("Run", [0, 0, 1], emit: il =>
        {
            il.LoadConstantI4(3); il.Call(integer); il.OpCode(ILOpCode.Pop);
            il.OpCode(ILOpCode.Ldnull); il.Call(payload); il.OpCode(ILOpCode.Pop); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [],
            contractAssemblies: [resolver.OpenInput(path).Value!]);
        Assert.Empty(report.Issues);
        Assert.Equal(2, report.Nodes.Count(n => n.Name == "Api::Identity"));
        Assert.Equal(2, report.Nodes.Where(n => n.Name == "Api::Identity").Select(n => n.Instantiation).Distinct().Count());
        var type = Assert.Single(report.Nodes, n => n.Name == "Payload");
        Assert.DoesNotContain(type.Id, report.DeferredNodes);
        Assert.Contains(report.Edges, e => e.To == type.Id && e.Kind == DependencyKind.Signature);
    }

    [Fact]
    public void Missing_signature_dependencies_are_not_hidden_by_a_boundary()
    {
        var library = Builder("Library"); library.Type("", "Api");
        library.Method("Use", [0, 0, 1], emit: Return);
        var missing = library.TypeReference("", "MissingBase", library.Reference(Identity("Missing")));
        // An application signature reaches a library type whose base cannot be resolved.
        var input = Builder("Input"); input.Type("", "Entry");
        library.Type("", "Child", baseType: missing);
        input.TypeReference("", "Child", input.Reference(Identity("Library")));
        var root = input.Method("Run", [0, 1, 1, 0x12, 5], emit: Return);
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [],
            contractAssemblies: [resolver.OpenInput(path).Value!]);
        Assert.Equal("incomplete", report.Status);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.AssemblyNotFound);
        Assert.Contains(report.Nodes, n => n.Name == "Child");
    }

    [Fact]
    public void Interface_dispatch_into_application_code_is_still_expanded()
    {
        var library = Builder("Library");
        library.Type("", "IAction", attributes: TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        library.Method("Act", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot);
        var input = Builder("Input");
        var contract = input.TypeReference("", "IAction", input.Reference(Identity("Library")));
        var declaration = input.Member(contract, "Act", 0x20, 0, 1);
        var receiver = input.Type("", "Action"); input.Implements(receiver, contract);
        input.Method("Act", [0x20, 0, 1], attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, emit: GeneralFilter);
        input.Type("", "Entry");
        var root = input.Method("Run", [0, 1, 1, 0x12, 8], emit: il =>
        {
            il.LoadArgument(0); il.OpCode(ILOpCode.Callvirt); il.Token(declaration); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [],
            contractAssemblies: [resolver.OpenInput(path).Value!]);
        Assert.Empty(report.Issues);
        Assert.Equal("observed", report.GeneralFilterStatus);
        var body = Assert.Single(report.Nodes, n => n.Name == "Action::Act");
        Assert.DoesNotContain(body.Id, report.DeferredNodes);
        Assert.Contains(report.Edges, e => e.To == body.Id && e.Kind == DependencyKind.ConservativeDispatch);
        Assert.Equal(body.Id, Assert.Single(report.Features).Node);
    }

    [Fact]
    public void A_boundary_does_not_add_implicit_reference_resolution_authority()
    {
        var library = Builder("Library"); library.Type("", "Api"); library.Method("Use", [0, 0, 1], emit: Return);
        var caller = Caller();
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build());
        string inputPath = files.Write("input.dll", caller.Bytes, "input");
        var absent = Run("audit", inputPath, "--root", "06000001", "--contract-only", path, "--format", "json");
        Assert.Equal(1, absent.Code); Assert.Empty(absent.Error);
        using var parsed = JsonDocument.Parse(absent.Output);
        Assert.Equal("incomplete", parsed.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, parsed.RootElement.GetProperty("deferredNodes").GetArrayLength());
        var present = Run("audit", inputPath, "--public-surface", "--contract-only", path,
            "--reference", files.DirectoryPath("refs"), "--format", "json");
        Assert.Equal(0, present.Code); Assert.Empty(present.Error);
        using var complete = JsonDocument.Parse(present.Output);
        Assert.Equal("contract-inventory", complete.RootElement.GetProperty("status").GetString());
        Assert.Equal("unresolved", complete.RootElement.GetProperty("generalFilterStatus").GetString());
    }

    [Fact]
    public void Boundaries_must_belong_to_the_resolver_and_cannot_be_the_input()
    {
        var input = Builder("Input"); input.Type("", "Entry"); var root = input.Method("Run", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); string path = files.Write("input.dll", input.Build());
        using var resolver = files.Open(); using var other = files.Open();
        var assembly = resolver.OpenInput(path).Value!; var foreign = other.OpenInput(path).Value!;
        Assert.Throws<ArgumentException>(() => resolver.Audit(assembly, [root], [], contractAssemblies: [assembly]));
        Assert.Throws<ArgumentException>(() => resolver.Audit(assembly, [root], [], contractAssemblies: [foreign]));
        Assert.Equal(1, Run("audit", path, "--public-surface", "--contract-only", path).Code);
        Assert.Equal(1, Run("audit", path, "--public-surface", "--contract-only", Path.Combine(files.Root, "missing.dll")).Code);
        Assert.Equal(2, Run("audit", path, "--public-surface", "--contract-only").Code);
    }

    [Fact]
    public void Direct_field_access_is_recorded_without_enumerating_other_library_fields()
    {
        var library = Builder("Library"); library.Type("", "Api");
        library.FieldWithAttributes("Value", FieldAttributes.Public | FieldAttributes.Static, 6, 0x12, 12);
        library.Field("Unused", 6, 8);
        library.Type("", "Payload");
        var input = Builder("Input"); input.Type("", "Entry");
        var api = input.TypeReference("", "Api", input.Reference(Identity("Library")));
        input.TypeReference("", "Payload", input.Reference(Identity("Library")));
        var field = input.Member(api, "Value", 6, 0x12, 9); // TypeRef 2
        var root = input.Method("Run", [0, 0, 1], emit: il =>
        {
            il.OpCode(ILOpCode.Ldsfld); il.Token(field); il.OpCode(ILOpCode.Pop); Return(il);
        });
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], [],
            contractAssemblies: [resolver.OpenInput(path).Value!]);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { "Api", "Api::Value", "Payload" }, report.DeferredNodes.Select(id => report.Nodes[id].Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(report.Nodes, n => n.Name == "Api::Unused");
        var member = Assert.Single(report.Nodes, n => n.IsExternalMember);
        Assert.Equal("field", member.Kind);
        Assert.Contains(report.Edges, e => e.To == member.Id && e.ILOffset == 0);
    }

    [Fact]
    public void Unreached_boundary_still_identifies_the_requested_inventory_scope()
    {
        var library = Builder("Library"); library.Type("", "Unused");
        var input = Builder("Input"); input.Type("", "Entry"); var root = input.Method("Run", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build())).Value!, [root], [],
            contractAssemblies: [resolver.OpenInput(files.Write("library.dll", library.Build())).Value!]);
        Assert.Empty(report.Issues); Assert.Empty(report.DeferredNodes);
        Assert.Single(report.ContractAssemblies);
        Assert.Equal("contract-inventory", report.Status);
        Assert.Equal("unresolved", report.GeneralFilterStatus);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(10, 1)]
    [InlineData(10, 2)]
    public void Limits_preserve_partial_evidence_without_dangling_deferred_nodes(int nodes, int edges)
    {
        var library = Builder("Library"); library.Type("", "Api"); library.Method("Use", [0, 0, 1], emit: Return);
        var caller = Caller();
        using var files = new ResolverFiles(); string path = files.Write("library.dll", library.Build());
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", caller.Bytes, "input")).Value!, [caller.Root], [],
            limits: new(MaximumNodes: nodes, MaximumEdges: edges), contractAssemblies: [resolver.OpenInput(path).Value!]);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
        Assert.Equal("incomplete", report.Status);
        Assert.All(report.DeferredNodes, id => Assert.Contains(report.Nodes[id].Assembly, report.ContractAssemblies));
        Assert.All(report.Nodes.Where(n => n.FirstReason.HasValue), n => Assert.Equal(n.Id, report.Edges[n.FirstReason!.Value].To));
    }

    [Fact]
    public void Reports_survive_disposal_and_duplicate_path_selection_and_culture_changes()
    {
        var library = Builder("Library"); library.Type("", "Api"); library.Method("Use", [0, 0, 1], emit: Return);
        byte[] bytes = library.Build(); var caller = Caller();
        using var files = new ResolverFiles(); string path = files.Write("library.dll", bytes);
        AuditReport Analyze(bool duplicate)
        {
            using var resolver = files.Open("refs");
            var selected = resolver.OpenInput(path).Value!;
            var copy = resolver.OpenInput(files.Write("copy.dll", bytes, "copies")).Value!;
            var input = resolver.OpenInput(files.Write("input.dll", caller.Bytes, duplicate ? "first" : "second")).Value!;
            return resolver.Audit(input, [caller.Root], [], contractAssemblies: duplicate ? [copy, selected, copy] : [selected]);
        }
        var report = Analyze(true); string json = AuditReportWriter.Json(report), text = AuditReportWriter.Text(report);
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var second = Analyze(false);
            Assert.Equal(json, AuditReportWriter.Json(second));
            Assert.Equal(text, AuditReportWriter.Text(second));
        }
        finally { CultureInfo.CurrentCulture = saved; }
        Assert.DoesNotContain(files.Root, json, StringComparison.Ordinal);
        Assert.DoesNotContain(files.Root, text, StringComparison.Ordinal);
        Assert.Contains("implementation-deferred", text, StringComparison.Ordinal);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(AuditReport.ContractProfile, parsed.RootElement.GetProperty("profile").GetString());
        Assert.Equal(1, parsed.RootElement.GetProperty("contractAssemblies").GetArrayLength());
        Assert.Equal(2, parsed.RootElement.GetProperty("deferredNodes").GetArrayLength());
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        int code = CilicaCommand.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
