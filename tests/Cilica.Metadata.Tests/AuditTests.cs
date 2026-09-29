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

public sealed class AuditTests
{
    private static ResolutionAssemblyBuilder Builder(string name = "Audit") => new(new AssemblyIdentity(name, new Version(1, 0, 0, 0)));
    private static void Return(InstructionEncoder il) => il.OpCode(ILOpCode.Ret);

    private static void Filter(InstructionEncoder il, bool general)
    {
        var start = il.DefineLabel(); var filter = il.DefineLabel(); var handler = il.DefineLabel(); var end = il.DefineLabel();
        il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Throw);
        il.MarkLabel(filter); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1);
        if (general) { il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); }
        il.OpCode(ILOpCode.Endfilter);
        il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.Branch(ILOpCode.Leave_s, end);
        il.MarkLabel(end); il.OpCode(ILOpCode.Ret);
        il.ControlFlowBuilder!.AddFilterRegion(start, filter, handler, end, filter);
    }

    [Fact]
    public void Only_reachable_filters_count_and_each_has_a_chain_back_to_the_root()
    {
        var builder = Builder(); builder.Type("Example", "Entry");
        var entry = builder.Method("Entry", [0, 0, 1], emit: il => { il.Call(MetadataTokens.MethodDefinitionHandle(2)); Return(il); });
        var reachable = builder.Method("TypeTest", [0, 0, 1], emit: il => Filter(il, false));
        var unused = builder.Method("General", [0, 0, 1], emit: il => Filter(il, true));
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!;
        var report = resolver.Audit(assembly, [entry], []);
        Assert.Empty(report.Issues);
        Assert.Equal("not-observed-in-bounded-graph", report.GeneralFilterStatus);
        var feature = Assert.Single(report.Features);
        Assert.Equal("filter-type-test", feature.Feature);
        Assert.Equal(2, feature.ILOffset);
        var leaf = report.Nodes[feature.Node];
        Assert.Equal("Example.Entry::TypeTest", leaf.Name);
        var edge = report.Edges[leaf.FirstReason!.Value];
        Assert.Equal(DependencyKind.Call, edge.Kind);
        Assert.Contains(edge.From, report.Roots);
        Assert.DoesNotContain(report.Nodes, n => n.Name.EndsWith("::General", StringComparison.Ordinal));
        var general = resolver.Audit(assembly, [unused, reachable], []);
        Assert.Empty(general.Issues);
        Assert.Equal("observed", general.GeneralFilterStatus);
        Assert.Equal(2, general.Features.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reference_stubs_and_missing_bodies_cannot_clear_filter_status(bool reference)
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Run", [0, 0, 1], emit: reference ? il => Filter(il, true) : null);
        if (reference) builder.MarkReferenceAssembly();
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!;
        var report = resolver.Audit(assembly, [root], []);
        Assert.Equal("incomplete", report.Status);
        Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Empty(report.Features);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityIncomplete && i.Node == report.Roots[0]);
        Assert.Equal(reference, Assert.Single(report.Assemblies).IsReferenceAssembly);
    }

    [Fact]
    public void External_member_evidence_preserves_facade_and_implementation_hashes()
    {
        var library = Builder("Library"); library.Type("Example", "Library");
        library.Method("Filter", [0, 0, 1], emit: il => Filter(il, true));
        var facade = Builder("Facade");
        facade.Forward("Example", "Library", facade.Reference(new("Library", new Version(1, 0, 0, 0))));
        var input = Builder("Input"); input.Type("", "Entry");
        var reference = input.TypeReference("Example", "Library", input.Reference(new("Facade", new Version(1, 0, 0, 0))));
        var member = input.Member(reference, "Filter", 0, 0, 1);
        var root = input.Method("Run", [0, 0, 1], emit: il => { il.Call(member); Return(il); });
        using var files = new ResolverFiles();
        files.Write("library.dll", library.Build()); files.Write("facade.dll", facade.Build());
        using var resolver = files.Open("refs");
        var report = resolver.Audit(resolver.OpenInput(files.Write("input.dll", input.Build(), "input")).Value!, [root], []);
        Assert.Empty(report.Issues);
        Assert.Equal("observed", report.GeneralFilterStatus);
        Assert.Equal(3, report.Assemblies.Length);
        Assert.All(report.Assemblies, a => Assert.Equal(64, a.ContentHash.Length));
        var external = Assert.Single(report.Nodes.Where(n => n.IsExternalMember));
        Assert.Equal("Example.Library::Filter", external.Name);
        Assert.StartsWith("Library,", report.Assemblies[external.Assembly].Identity, StringComparison.Ordinal);
        Assert.Equal(external.Id, Assert.Single(report.Features).Node);
    }

    [Fact]
    public void Feature_signals_cover_signatures_instructions_and_named_infrastructure()
    {
        var builder = Builder(); builder.Type("Example", "Entry");
        var root = builder.Method("Run", [0, 0, 1], emit: il => { il.LoadConstantR8(1.5); il.OpCode(ILOpCode.Pop); Return(il); },
            locals: [7, 1, 0x0f, 8]); // int32* local
        builder.Field("Decimal", 6, 0x11, 12); // TypeDef 3
        builder.Type("System", "Decimal");
        builder.Type("System.Threading.Tasks", "Task");
        builder.Type("System.Runtime.CompilerServices", "TaskAwaiter");
        builder.Type("System.Reflection", "MethodInfo");
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!, [root],
            ["System.Threading.Tasks.Task", "System.Runtime.CompilerServices.TaskAwaiter", "System.Reflection.MethodInfo"]);
        Assert.Empty(report.Issues);
        Assert.Equal(new[] { "async", "decimal", "floating-point", "reflection", "threading", "unsafe" },
            report.Features.Select(f => f.Feature).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(report.Features, f => f.Feature == "floating-point" && f.ILOffset == 0);
    }

    [Fact]
    public void Reflection_related_calls_leave_an_explicit_dynamic_target_gap()
    {
        var builder = Builder(); builder.Type("System.Reflection", "MethodInfo");
        var root = builder.Method("Invoke", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!, [root], []);
        Assert.Contains(report.Features, f => f.Feature == "reflection");
        Assert.Contains(report.Issues, i => i.Diagnostic.Message.Contains("dynamic-target", StringComparison.Ordinal));
        Assert.Equal("unresolved", report.GeneralFilterStatus);
    }

    [Fact]
    public void Reports_survive_disposal_and_are_identical_across_paths_cultures_and_root_order()
    {
        var builder = Builder(); builder.Type("Example", "Entry");
        var a = builder.Method("A", [0, 0, 1], emit: il => Filter(il, true));
        var b = builder.Method("B", [0, 0, 1], emit: Return);
        byte[] bytes = builder.Build();
        using var files = new ResolverFiles();
        AuditReport Run(string directory, EntityHandle[] roots)
        {
            using var resolver = files.Open();
            return resolver.Audit(resolver.OpenInput(files.Write("audit.dll", bytes, directory)).Value!, roots, []);
        }
        var first = Run("first", [a, b]); var second = Run("second", [b, a, b]);
        string json = AuditReportWriter.Json(first), text = AuditReportWriter.Text(first);
        Golden.Verify("audit.json", json);
        Golden.Verify("audit.txt", text);
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(json, AuditReportWriter.Json(second));
            Assert.Equal(text, AuditReportWriter.Text(second));
        }
        finally { CultureInfo.CurrentCulture = saved; }
        Assert.DoesNotContain(files.Root, json, StringComparison.Ordinal);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("cilica.audit", parsed.RootElement.GetProperty("format").GetString());
        Assert.Equal("observed", parsed.RootElement.GetProperty("generalFilterStatus").GetString());
    }

    [Fact]
    public void Type_selection_missing_roots_and_limits_remain_explicit()
    {
        var builder = Builder(); var type = builder.Type("Example", "Entry");
        builder.Method("Private", [0, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Static, emit: Return);
        builder.Method("Public", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var assembly = resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!;
        Assert.Contains(resolver.Audit(assembly, [], ["Example.Entry"]).Nodes, n => n.Name.EndsWith("::Private", StringComparison.Ordinal));
        Assert.DoesNotContain(resolver.Audit(assembly, [], [], publicSurface: true).Nodes, n => n.Name.EndsWith("::Private", StringComparison.Ordinal));
        Assert.Contains(resolver.Audit(assembly, [], ["Missing"]).Issues, i => i.Diagnostic.Id == DiagnosticIds.TypeNotFound);
        var limited = resolver.Audit(assembly, [type], [], limits: new(MaximumNodes: 1));
        Assert.Equal("unresolved", limited.GeneralFilterStatus);
        Assert.Contains(limited.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("audit", "a.dll")]
    [InlineData("audit", "a.dll", "--root", "0x0a000001")]
    [InlineData("audit", "a.dll", "--root", "0x06000000")]
    [InlineData("audit", "a.dll", "--root", "nope")]
    [InlineData("audit", "a.dll", "--root-type")]
    [InlineData("audit", "a.dll", "--reference", "--public-surface")]
    [InlineData("audit", "a.dll", "--public-surface", "--root-type", "C")]
    public void Invalid_audit_arguments_are_usage_errors(params string[] args) => Assert.Equal(2, Run(args).Code);

    [Fact]
    public void Command_writes_success_or_incomplete_reports_and_output_files()
    {
        var builder = Builder(); builder.Type("Example", "Entry"); builder.Method("Run", [0, 0, 1], emit: Return);
        using var files = new ResolverFiles(); string path = files.Write("audit.dll", builder.Build());
        var valid = Run("audit", path, "--root-type", "Example.Entry", "--format", "json");
        Assert.Equal(0, valid.Code); Assert.Empty(valid.Error);
        using var parsed = JsonDocument.Parse(valid.Output);
        Assert.Equal("bounded-graph-analyzed", parsed.RootElement.GetProperty("status").GetString());
        string output = Path.Combine(files.Root, "report.json");
        var written = Run("audit", path, "--root-type", "Example.Entry", "--format", "json", "--output", output);
        Assert.Equal(0, written.Code); Assert.Empty(written.Output); Assert.Equal(valid.Output, File.ReadAllText(output));
        Assert.Equal(1, Run("audit", path, "--root", "06000100").Code);
        var missing = Run("audit", path, "--public-surface", "--reference", Path.Combine(files.Root, "missing"), "--format", "json");
        Assert.Equal(1, missing.Code); Assert.Empty(missing.Error);
        Assert.Contains("CIL3001", missing.Output, StringComparison.Ordinal);
        Assert.Contains("unresolved", missing.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_virtual_declarations_are_signalled_even_when_open_roots_are_incomplete()
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("M", [0x30, 1, 0, 1], arity: 1,
            attributes: MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, emit: Return);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!, [root], []);
        Assert.Contains(report.Features, f => f.Feature == "generic-virtual" && f.Node == report.Roots[0]);
        Assert.Equal("unresolved", report.GeneralFilterStatus);
    }

    [Fact]
    public void Observed_general_filters_remain_visible_alongside_missing_dependencies()
    {
        var builder = Builder(); builder.Type("", "Entry");
        var root = builder.Method("Filter", [0, 0, 1], emit: il => Filter(il, true));
        var missing = builder.Method("Missing", [0, 0, 1]);
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var report = resolver.Audit(resolver.OpenInput(files.Write("audit.dll", builder.Build())).Value!, [missing, root], []);
        Assert.Equal("incomplete", report.Status);
        Assert.Equal("observed", report.GeneralFilterStatus);
    }

    [Fact]
    public void Command_requires_explicit_reference_directories_even_for_adjacent_assemblies()
    {
        var library = Builder("Library"); library.Type("", "Library"); library.Method("M", [0, 0, 1], emit: Return);
        var input = Builder(); input.Type("", "Entry");
        var type = input.TypeReference("", "Library", input.Reference(new("Library", new Version(1, 0, 0, 0))));
        var target = input.Member(type, "M", 0, 0, 1);
        input.Method("Run", [0, 0, 1], emit: il => { il.Call(target); Return(il); });
        using var files = new ResolverFiles(); files.Write("library.dll", library.Build());
        string path = files.Write("audit.dll", input.Build());
        var missing = Run("audit", path, "--root", "06000001");
        Assert.Equal(1, missing.Code); Assert.Contains("CIL3003", missing.Output, StringComparison.Ordinal);
        Assert.Equal(0, Run("audit", path, "--root", "06000001", "--reference", files.DirectoryPath("refs")).Code);
    }

    [Fact]
    public void Seeded_audit_corruption_never_becomes_an_internal_command_error()
    {
        var builder = Builder(); builder.Type("Example", "Entry");
        builder.Method("Run", [0, 0, 1], emit: il => Filter(il, true));
        byte[] original = builder.Build();
        var random = new Random(1729);
        using var files = new ResolverFiles();
        for (int iteration = 0; iteration < Fuzz.Iterations; iteration++)
        {
            byte[] bytes = (byte[])original.Clone();
            for (int i = 0; i < 3; i++) bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            string path = files.Write("corrupt.dll", bytes);
            var result = Run("audit", path, "--root-type", "Example.Entry", "--format", "json");
            Assert.True(result.Code is 0 or 1, result.Error);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal("cilica.audit", json.RootElement.GetProperty("format").GetString());
        }
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        int code = CilicaCommand.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
