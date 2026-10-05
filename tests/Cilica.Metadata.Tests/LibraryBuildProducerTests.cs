using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.LibraryBuild;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryBuildProducerTests
{
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static LibraryBuildRequest Request(ResolverFiles files, string source, string output = "build")
    {
        string root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "../../.."));
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "global.json"))) repository = repository.Parent;
        Assert.NotNull(repository);
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository.FullName, "global.json")));
        string sdk = settings.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
        return new(Environment.GetEnvironmentVariable("CILICA_CSC_DIRECTORY") ?? Path.Combine(root, "sdk", sdk, "Roslyn", "bincore"),
            [source], [Path.Combine(root, "packs", "NETStandard.Library.Ref", "2.1.0", "ref", "netstandard2.1", "netstandard.dll")],
            Path.Combine(files.Root, output), "LibraryTrial");
    }

    [Fact]
    public async Task Actual_compiler_inputs_output_and_arguments_produce_admissible_evidence()
    {
        using var files = new ResolverFiles();
        byte[] source = Encoding.UTF8.GetBytes("namespace LibraryTrial { public static class Probe { public static int Run(int value) => value + 1; } }");
        string path = files.Write("Probe.cs", source, "source");
        var request = Request(files, path);
        var result = await LibraryBuilder.Build(request);
        byte[] evidence = File.ReadAllBytes(result.ManifestPath);
        Assert.Equal(Hash(evidence), result.ManifestSha256);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(evidence)!;
        var artifact = Assert.Single(manifest.Artifacts);
        Assert.Empty(artifact.Grants);
        Assert.Equal(Hash(File.ReadAllBytes(result.AssemblyPath)), artifact.Sha256);
        Assert.Equal(Hash(source), Assert.Single(artifact.Inputs.Where(i => i.Role == "source")).Sha256);
        Assert.Contains("-noconfig", artifact.CompilerArguments);
        Assert.Contains("-nostdlib+", artifact.CompilerArguments);
        Assert.Contains("-unsafe-", artifact.CompilerArguments);
        foreach (var input in artifact.Inputs)
            Assert.Equal(input.Sha256, Hash(File.ReadAllBytes(Path.IsPathRooted(input.Path) ? input.Path : Path.Combine(request.OutputDirectory, input.Path))));

        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var implementation = resolver.OpenInput(result.AssemblyPath).Value!;
        var contract = new ResolutionAssemblyBuilder(new("Trial.Contract", new Version(1, 0, 0, 0)));
        contract.MarkReferenceAssembly();
        var core = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var obj = contract.TypeReference("System", "Object", contract.Reference(core.Identity));
        contract.Type("LibraryTrial", "Probe", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
        contract.Method("Run", [0, 1, 8, 8]);
        var reference = resolver.OpenInput(files.Write("Trial.Contract.dll", contract.Build())).Value!;
        var plan = resolver.CreateLibraryBindingPlan([new(resolver.ResolveType(reference, "LibraryTrial", "Probe").Value!,
            resolver.ResolveType(implementation, "LibraryTrial", "Probe").Value!)]);
        Assert.True(plan.Success, string.Join("; ", plan.Diagnostics));
        var admission = resolver.CreateTrustedLibraryCompilationScope(plan.Value!, evidence, result.ManifestSha256);
        Assert.True(admission.Success, string.Join("; ", admission.Diagnostics));
        var audit = admission.Value!.AuditLibraryImplementation(reference, [System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(1)]);
        Assert.Equal(result.ManifestSha256, audit.ApprovedLibraryManifestSha256);
        Assert.Contains(audit.Nodes, n => audit.Assemblies[n.Assembly].ContentHash == artifact.Sha256 && n.Kind == "method");
        admission.Value!.Dispose();
        Assert.False(resolver.CreateTrustedLibraryCompilationScope(plan.Value!, evidence, new string('0', 64)).Success);
        // The caller's independent approval is simulated above only for this authored fixture.
        // The producer itself returns no resolver scope and never approves its digest.

        var second = await LibraryBuilder.Build(request with { OutputDirectory = Path.Combine(files.Root, "again") });
        Assert.Equal(artifact.Sha256, Hash(File.ReadAllBytes(second.AssemblyPath)));
        Assert.Empty(Directory.EnumerateDirectories(files.Root, ".cilica-build-*"));
    }

    [Fact]
    public async Task Compiler_failure_publishes_no_output_or_evidence_and_cleans_staging()
    {
        using var files = new ResolverFiles();
        string path = files.Write("Bad.cs", Encoding.UTF8.GetBytes("#error intentional-build-failure"), "source");
        var request = Request(files, path);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => LibraryBuilder.Build(request));
        Assert.Contains("Compiler failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("intentional-build-failure", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(request.OutputDirectory));
        Assert.Empty(Directory.EnumerateDirectories(files.Root, ".cilica-build-*"));
    }

    [Fact]
    public async Task Excessive_compiler_output_is_bounded_and_publishes_nothing()
    {
        using var files = new ResolverFiles();
        string source = string.Concat(Enumerable.Repeat("#error " + new string('x', 8192) + "\n", 64));
        var request = Request(files, files.Write("Loud.cs", Encoding.UTF8.GetBytes(source), "source"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => LibraryBuilder.Build(request));
        Assert.Contains("capture bound", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(request.OutputDirectory));
        Assert.Empty(Directory.EnumerateDirectories(files.Root, ".cilica-build-*"));
    }

    [Fact]
    public async Task Existing_outputs_and_option_injection_are_rejected_without_changes()
    {
        using var files = new ResolverFiles();
        string path = files.Write("Probe.cs", Encoding.UTF8.GetBytes("class Probe {}"), "source");
        var request = Request(files, path);
        Directory.CreateDirectory(request.OutputDirectory);
        string marker = Path.Combine(request.OutputDirectory, "keep"); File.WriteAllText(marker, "untouched");
        await Assert.ThrowsAsync<IOException>(() => LibraryBuilder.Build(request));
        Assert.Equal("untouched", File.ReadAllText(marker));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with { AssemblyName = "../escape" }));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with { Sources = [] }));
        Assert.Empty(Directory.EnumerateDirectories(files.Root, ".cilica-build-*"));
    }
}
