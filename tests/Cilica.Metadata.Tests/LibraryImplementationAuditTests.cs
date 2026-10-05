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

public sealed class LibraryImplementationAuditTests
{
    private static AssemblyIdentity Id(string name) => new(name, new Version(1, 0, 0, 0));
    private static T Success<T>(ResolutionResult<T> result) where T : class
    { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Value!; }

    private sealed class Fixture : IDisposable
    {
        internal readonly ResolverFiles Files = new();
        internal readonly AssemblyResolver Source, Scope;
        internal readonly ResolvedAssembly Contract, Implementation, App;
        internal readonly MethodDefinitionHandle Entry;
        internal Fixture(bool missing = false, bool filter = false, bool missingMember = false)
        {
            var left = new ResolutionAssemblyBuilder(Id("Contract")); left.MarkReferenceAssembly();
            left.Type("Example", "Api"); left.FieldWithAttributes("Seed", FieldAttributes.Public | FieldAttributes.Static, 6, 8);
            left.Method("Increment", [0, 1, 8, 8]);
            var right = new ResolutionAssemblyBuilder(Id("Library")); right.Type("Example", "Api");
            right.FieldWithAttributes("Seed", FieldAttributes.Public | FieldAttributes.Static, 6, 8);
            if (!missingMember) right.Method("Increment", [0, 1, 8, 8], emit: il => {
                il.Call(MetadataTokens.MethodDefinitionHandle(2)); il.LoadArgument(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
            });
            right.Method("Helper", [0, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Static,
                emit: missing ? null : il => {
                    if (!filter) { il.OpCode(ILOpCode.Ret); return; }
                    var start = il.DefineLabel(); var predicate = il.DefineLabel(); var handler = il.DefineLabel(); var end = il.DefineLabel();
                    il.MarkLabel(start); il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Throw);
                    il.MarkLabel(predicate); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(1); il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Endfilter);
                    il.MarkLabel(handler); il.OpCode(ILOpCode.Pop); il.Branch(ILOpCode.Leave_s, end); il.MarkLabel(end); il.OpCode(ILOpCode.Ret);
                    il.ControlFlowBuilder!.AddFilterRegion(start, predicate, handler, end, predicate);
                });
            right.Method(".cctor", [0, 0, 1], attributes: MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                emit: il => il.OpCode(ILOpCode.Ret));
            var input = new ResolutionAssemblyBuilder(Id("App"));
            var api = input.TypeReference("Example", "Api", input.Reference(Id("Contract")));
            var seed = input.Member(api, "Seed", 6, 8); var call = input.Member(api, "Increment", 0, 1, 8, 8);
            input.Type("Example", "Program"); Entry = input.Method("Run", [0, 0, 8], emit: il => {
                il.OpCode(ILOpCode.Ldsfld); il.Token(seed); il.Call(call); il.Call(call); il.OpCode(ILOpCode.Ret);
            });
            string l = Files.Write("Contract.dll", left.Build()), r = Files.Write("Library.dll", right.Build());
            Source = Files.Open("refs"); Contract = Success(Source.OpenInput(l)); Implementation = Success(Source.OpenInput(r));
            App = Success(Source.OpenInput(Files.Write("App.dll", input.Build(), "input")));
            var plan = Success(Source.CreateLibraryBindingPlan([new(Success(Source.ResolveType(Contract, "Example", "Api")),
                Success(Source.ResolveType(Implementation, "Example", "Api")))]));
            Scope = Source.CreateLibraryCompilationScope(plan);
        }
        public void Dispose() { Scope.Dispose(); Source.Dispose(); Files.Dispose(); }
    }

    [Fact]
    public void Mapped_audit_expands_implementation_helpers_and_retains_both_member_declarations()
    {
        AuditReport report;
        using (var f = new Fixture())
        {
            report = f.Scope.AuditLibraryImplementation(f.App, [f.Entry]);
            Assert.False(report.HasErrors, string.Join("; ", report.Issues));
            Assert.Equal("mapped-implementation-graph-analyzed", report.Status);
            Assert.Equal("not-observed-in-bounded-graph", report.GeneralFilterStatus);
            Assert.Empty(report.ContractAssemblies); Assert.Empty(report.DeferredNodes);
            Assert.Null(report.ApprovedLibraryManifestSha256);
            var mapping = Assert.Single(report.LibraryTypes);
            Assert.Equal(f.Contract.ContentHash, report.Assemblies[mapping.ContractAssembly].ContentHash);
            Assert.Equal(f.Implementation.ContentHash, report.Assemblies[mapping.ImplementationAssembly].ContentHash);
            Assert.Contains(report.Nodes, n => n.Name == "Example.Api::Helper" && n.Assembly == mapping.ImplementationAssembly);
            Assert.Contains(report.Nodes, n => n.Name == "Example.Api::.cctor" && n.Assembly == mapping.ImplementationAssembly);
            Assert.Contains(report.Edges, e => e.Kind == DependencyKind.FieldLayout);
            Assert.Contains(report.Edges, e => e.Kind == DependencyKind.TypeInitializer);
            Assert.DoesNotContain(report.Nodes, n => n.Assembly == mapping.ContractAssembly);
            Assert.Equal(2, report.LibraryMembers.Length);
            Assert.Contains(report.LibraryMembers, m => m.Kind == "field");
            Assert.Contains(report.LibraryMembers, m => m.Kind == "method");
            Assert.All(report.LibraryMembers, m => {
                Assert.Equal(mapping.ContractAssembly, m.Contract.Assembly);
                Assert.Equal(mapping.ImplementationAssembly, m.Implementation.Assembly);
                Assert.Contains("Int32", m.Contract.Instantiation, StringComparison.Ordinal);
                Assert.Contains("Int32", m.Implementation.Instantiation, StringComparison.Ordinal);
            });
            Assert.Equal(AuditReportWriter.Json(report), AuditReportWriter.Json(f.Scope.AuditLibraryImplementation(f.App, [f.Entry, f.Entry])));
            Assert.Throws<InvalidOperationException>(() => f.Scope.Audit(f.App, [f.Entry], []));
            Assert.Throws<InvalidOperationException>(() => f.Source.AuditLibraryImplementation(f.App, [f.Entry]));
            Assert.Equal("contract-inventory", f.Source.Audit(f.App, [f.Entry], [], contractAssemblies: [f.Contract]).Status);
        }
        Assert.Contains("libraryMembers", AuditReportWriter.Json(report), StringComparison.Ordinal);
        Assert.Contains("library field", AuditReportWriter.Text(report), StringComparison.Ordinal);
        Assert.Contains("not supplied", AuditReportWriter.Text(report), StringComparison.Ordinal);
    }

    [Fact]
    public void Implementation_general_filter_is_observed_behind_a_reference_only_declaration()
    {
        using var f = new Fixture(filter: true);
        var report = f.Scope.AuditLibraryImplementation(f.App, [f.Entry]);
        Assert.False(report.HasErrors, string.Join("; ", report.Issues));
        Assert.Equal("observed", report.GeneralFilterStatus);
        var feature = Assert.Single(report.Features.Where(x => x.Feature == "filter-general"));
        Assert.Equal("Example.Api::Helper", report.Nodes[feature.Node].Name);
        Assert.NotNull(report.Nodes[feature.Node].FirstReason);
    }

    [Fact]
    public void Missing_implementation_body_and_budget_exhaustion_remain_unresolved()
    {
        using var missing = new Fixture(missing: true);
        var report = missing.Scope.AuditLibraryImplementation(missing.App, [missing.Entry]);
        Assert.True(report.HasErrors); Assert.Equal("incomplete", report.Status); Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityIncomplete);
        using var limited = new Fixture();
        report = limited.Scope.AuditLibraryImplementation(limited.App, [limited.Entry], new(MaximumEdges: 1));
        Assert.True(report.HasErrors); Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit);
    }

    [Fact]
    public void Missing_mapped_members_leave_actionable_issues_without_reference_fallback()
    {
        using var f = new Fixture(missingMember: true);
        var report = f.Scope.AuditLibraryImplementation(f.App, [f.Entry]);
        Assert.True(report.HasErrors); Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.MemberNotFound && i.ILOffset is not null);
        Assert.DoesNotContain(report.Nodes, n => report.Assemblies[n.Assembly].IsReferenceAssembly);
    }

    [Fact]
    public void Projection_evidence_has_a_bound_even_before_root_expansion()
    {
        using var f = new Fixture();
        var report = f.Scope.AuditLibraryImplementation(f.Contract,
            [MetadataTokens.MethodDefinitionHandle(1), MetadataTokens.FieldDefinitionHandle(1)], new(MaximumEdges: 1));
        Assert.Equal("unresolved", report.GeneralFilterStatus);
        Assert.Single(report.LibraryMembers);
        Assert.Contains(report.Issues, i => i.Diagnostic.Id == DiagnosticIds.ReachabilityLimit
            && i.Diagnostic.Message.Contains("projection evidence", StringComparison.Ordinal));
    }

    [Fact]
    public void Closed_generic_member_evidence_retains_owner_and_method_arguments()
    {
        using var files = new ResolverFiles();
        static byte[] Image(string name, bool reference)
        {
            var b = new ResolutionAssemblyBuilder(Id(name)); if (reference) b.MarkReferenceAssembly();
            b.Type("Example", "Box`1", arity: 1);
            b.Method("Map", [0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0], arity: 1,
                emit: reference ? null : il => { il.LoadArgument(1); il.OpCode(ILOpCode.Ret); });
            return b.Build();
        }
        string left = files.Write("Contract.dll", Image("Contract", true)), right = files.Write("Library.dll", Image("Library", false));
        using var source = files.Open("refs");
        var reference = Success(source.OpenInput(left)); var implementation = Success(source.OpenInput(right));
        var plan = Success(source.CreateLibraryBindingPlan([new(Success(source.ResolveType(reference, "Example", "Box`1")),
            Success(source.ResolveType(implementation, "Example", "Box`1")))]));
        using var scope = source.CreateLibraryCompilationScope(plan);
        var input = new ResolutionAssemblyBuilder(Id("App")); input.TypeReference("Example", "Box`1", input.Reference(Id("Contract")));
        var closed = input.Specification(0x15, 0x12, 5, 1, 8);
        var member = input.Member(closed, "Map", 0x10, 1, 2, 0x1e, 0, 0x13, 0, 0x1e, 0);
        var call = input.Instantiate(member, 0x0a, 1, 0x0a); // Box<Int32>.Map<Int64>
        input.Type("Example", "Program");
        var root = input.Method("Run", [0, 0, 0x0a], emit: il => {
            il.LoadConstantI4(7); il.LoadConstantI8(9); il.Call(call); il.OpCode(ILOpCode.Ret);
        });
        var assembly = Success(source.OpenInput(files.Write("App.dll", input.Build(), "input")));
        var report = scope.AuditLibraryImplementation(assembly, [root]);
        Assert.False(report.HasErrors, string.Join("; ", report.Issues));
        var use = Assert.Single(report.LibraryMembers);
        Assert.Contains("<Int32><Int64>", use.Contract.Instantiation, StringComparison.Ordinal);
        Assert.Contains("Int64(Int32,Int64)", use.Implementation.Instantiation, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_method_roots_select_the_implementation_and_source_analysis_stays_strict()
    {
        using var f = new Fixture();
        var root = MetadataTokens.MethodDefinitionHandle(1);
        var report = f.Scope.AuditLibraryImplementation(f.Contract, [root]);
        Assert.False(report.HasErrors, string.Join("; ", report.Issues));
        var node = report.Nodes[Assert.Single(report.Roots)];
        Assert.Equal(f.Implementation.ContentHash, report.Assemblies[node.Assembly].ContentHash);
        Assert.Single(report.LibraryMembers);
        Assert.True(f.Source.Audit(f.Contract, [root], []).HasErrors);
    }
}
