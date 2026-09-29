using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class AssemblyResolutionTests
{
    private static AssemblyIdentity Identity(string name = "Library", string version = "1.2.3.4",
        string culture = "neutral", string? token = null) => new(name, Version.Parse(version), culture, token);

    [Fact]
    public void Manifest_identity_resolves_independently_of_filename_and_extension_case()
    {
        using var files = new ResolverFiles();
        files.Write("renamed.DLL", new ResolutionAssemblyBuilder(Identity()).Build());
        using var resolver = files.Open("refs");

        var result = resolver.ResolveAssembly(Identity("library"));

        Assert.True(result.Success);
        Assert.Equal(Identity(), result.Value!.Identity);
        Assert.Equal("renamed.DLL", result.Value.InputName);
        Assert.Equal(64, result.Value.ContentHash.Length);
    }

    [Theory]
    [InlineData("2.2.3.4", "neutral", null)]
    [InlineData("1.2.3.5", "neutral", null)]
    [InlineData("1.2.3.4", "fr", null)]
    [InlineData("1.2.3.4", "neutral", "0102030405060708")]
    public void Version_culture_and_key_mismatches_never_substitute(string version, string culture, string? token)
    {
        using var files = new ResolverFiles();
        files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity()).Build());
        using var resolver = files.Open("refs");

        var result = resolver.ResolveAssembly(Identity(version: version, culture: culture, token: token));

        Assert.Null(result.Value);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticIds.AssemblyNotFound, diagnostic.Id);
        Assert.Contains(Identity().DisplayName, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Full_public_key_and_reference_token_identify_the_same_assembly()
    {
        using var files = new ResolverFiles();
        byte[] key = [1, 2, 3, 4];
        var target = Identity(token: "6f373e443b2c2047");
        files.Write("Library.dll", new ResolutionAssemblyBuilder(target, key).Build());
        var inputBuilder = new ResolutionAssemblyBuilder(Identity("Input"));
        inputBuilder.Reference(target);
        inputBuilder.Reference(target, fullKey: key);
        string inputPath = files.Write("Input.dll", inputBuilder.Build(), "input");
        using var resolver = files.Open("refs");
        ResolvedAssembly input = resolver.OpenInput(inputPath).Value!;

        Assert.Equal(2, input.References.Length);
        foreach (AssemblyIdentity reference in input.References)
        {
            Assert.Equal(target, reference);
            Assert.True(resolver.ResolveAssembly(reference).Success);
        }
    }

    [Fact]
    public void Identity_equality_and_hashing_use_the_same_normalization()
    {
        var first = Identity("Library", culture: "en-US", token: "ABCDEF0123456789");
        var second = Identity("library", culture: "EN-us", token: "abcdef0123456789");
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(Identity(), Identity(culture: ""));
    }

    [Fact]
    public void Opening_an_input_does_not_probe_its_directory_or_add_its_identity()
    {
        using var files = new ResolverFiles();
        var builder = new ResolutionAssemblyBuilder(Identity("Input"));
        builder.Reference(Identity());
        string inputPath = files.Write("Input.dll", builder.Build(), "input");
        files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity()).Build(), "input");
        using var resolver = files.Open();
        ResolvedAssembly input = resolver.OpenInput(inputPath).Value!;

        Assert.Equal(DiagnosticIds.AssemblyNotFound, Assert.Single(resolver.ResolveAssembly(input.References[0]).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.AssemblyNotFound, Assert.Single(resolver.ResolveAssembly(input.Identity).Diagnostics).Id);
    }

    [Fact]
    public void No_implicit_installed_runtime_or_recursive_probing()
    {
        using var files = new ResolverFiles();
        files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity()).Build(), "refs/nested");
        using var resolver = files.Open("refs");
        var core = AssemblyInspector.Inspect(typeof(object).Assembly.Location).Identity!;

        Assert.False(resolver.ResolveAssembly(Identity()).Success);
        Assert.False(resolver.ResolveAssembly(new AssemblyIdentity(core.Name, Version.Parse(core.Version), core.Culture, core.PublicKeyToken)).Success);
    }

    [Fact]
    public void Identical_copies_and_repeated_directories_are_not_conflicts()
    {
        using var files = new ResolverFiles();
        byte[] bytes = new ResolutionAssemblyBuilder(Identity()).Build();
        files.Write("Library.dll", bytes, "a");
        files.Write("Library.dll", bytes, "b");
        using var resolver = files.Open("b", "a", "a");

        Assert.True(resolver.ResolveAssembly(Identity()).Success);
    }

    [Fact]
    public void Different_contents_with_one_identity_conflict_regardless_of_directory_order()
    {
        using var files = new ResolverFiles();
        files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity()).Build(), "a");
        var changed = new ResolutionAssemblyBuilder(Identity());
        changed.Type("Sample", "Additional");
        files.Write("Library.dll", changed.Build(), "b");
        using var first = files.Open("a", "b");
        using var second = files.Open("b", "a");

        var result = first.ResolveAssembly(Identity());

        Assert.Null(result.Value);
        Assert.Equal(DiagnosticIds.AssemblyConflict, Assert.Single(result.Diagnostics).Id);
        Assert.Equal(result.Diagnostics, second.ResolveAssembly(Identity()).Diagnostics);
        Assert.DoesNotContain(files.Root, result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_name_different_versions_resolve_to_the_requested_identity()
    {
        using var files = new ResolverFiles();
        files.Write("One.dll", new ResolutionAssemblyBuilder(Identity()).Build());
        var version2 = Identity(version: "2.0.0.0");
        files.Write("Two.exe", new ResolutionAssemblyBuilder(version2).Build());
        using var resolver = files.Open("refs");

        Assert.Equal(Identity(), resolver.ResolveAssembly(Identity()).Value!.Identity);
        Assert.Equal(version2, resolver.ResolveAssembly(version2).Value!.Identity);
    }

    [Fact]
    public void Missing_directory_and_bad_candidates_prevent_partial_catalogues()
    {
        using var files = new ResolverFiles();
        var missing = AssemblyResolver.Create([Path.Combine(files.Root, "missing")]);
        Assert.Null(missing.Value);
        Assert.Equal(DiagnosticIds.ReferenceDirectoryInvalid, Assert.Single(missing.Diagnostics).Id);
        Assert.DoesNotContain(files.Root, missing.Diagnostics[0].Message, StringComparison.Ordinal);
        files.Write("Valid.dll", new ResolutionAssemblyBuilder(Identity()).Build());
        files.Write("Invalid.dll", [1, 2, 3]);

        var damaged = AssemblyResolver.Create([files.DirectoryPath("refs")]);

        Assert.Null(damaged.Value);
        Assert.Equal(DiagnosticIds.NotPortableExecutable, Assert.Single(damaged.Diagnostics).Id);
    }

    [Theory]
    [InlineData(AssemblyFlags.Retargetable)]
    [InlineData(AssemblyFlags.WindowsRuntime)]
    public void Unsupported_binding_flags_are_diagnosed_not_ignored(AssemblyFlags flags)
    {
        using var files = new ResolverFiles();
        files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity(), flags: flags).Build());

        var result = AssemblyResolver.Create([files.DirectoryPath("refs")]);

        Assert.Null(result.Value);
        Assert.Equal(DiagnosticIds.ResolutionUnsupported, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("System.Runtime")]
    [InlineData("System.Private.CoreLib")]
    [InlineData("Custom.Marker.Scope")]
    public void Reference_marker_is_reported_and_does_not_select_a_winner(string markerScope)
    {
        using var files = new ResolverFiles();
        files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity()).Build(), "impl");
        var reference = new ResolutionAssemblyBuilder(Identity());
        reference.MarkReferenceAssembly(markerScope);
        string referencePath = files.Write("Library.dll", reference.Build(), "ref");
        using var resolver = files.Open("ref", "impl");

        Assert.True(resolver.OpenInput(referencePath).Value!.IsReferenceAssembly);
        Assert.Equal(DiagnosticIds.AssemblyConflict, Assert.Single(resolver.ResolveAssembly(Identity()).Diagnostics).Id);
    }

    [Fact]
    public void A_session_keeps_its_snapshot_until_disposed()
    {
        using var files = new ResolverFiles();
        string path = files.Write("Library.dll", new ResolutionAssemblyBuilder(Identity()).Build());
        var resolver = files.Open("refs");
        ResolvedAssembly original = resolver.ResolveAssembly(Identity()).Value!;
        File.WriteAllBytes(path, [1, 2, 3]);

        Assert.Same(original, resolver.OpenInput(path).Value);
        Assert.Same(original, resolver.ResolveAssembly(Identity()).Value);
        resolver.Dispose();
        resolver.Dispose();
        Assert.Throws<ObjectDisposedException>(() => resolver.ResolveAssembly(Identity()));
        Assert.Throws<ObjectDisposedException>(() => resolver.OpenInput(path));
        Assert.Throws<ObjectDisposedException>(() => resolver.ResolveType(original, "Sample", "Type"));
    }

    [Fact]
    public void Corrupt_metadata_does_not_escape_resolution_as_a_host_exception()
    {
        using var files = new ResolverFiles();
        byte[] original = Fixture.Bytes(Fixture.Basic);
        using var pe = new PEReader(ImmutableArray.Create(original));
        int start = pe.PEHeaders.MetadataStartOffset;
        int length = pe.PEHeaders.MetadataSize;
        var random = new Random(20260930);
        for (int iteration = 0; iteration < Fuzz.Iterations; iteration++)
        {
            byte[] bytes = (byte[])original.Clone();
            for (int count = random.Next(1, 6); count > 0; count--)
                bytes[start + random.Next(length)] = (byte)random.Next(256);
            files.Write("Damaged.dll", bytes);
            var result = AssemblyResolver.Create([files.DirectoryPath("refs")]);
            using AssemblyResolver? resolver = result.Value;
            if (resolver is null)
            {
                Assert.NotEmpty(result.Diagnostics);
                continue;
            }
            var input = resolver.OpenInput(Path.Combine(files.DirectoryPath("refs"), "Damaged.dll"));
            Assert.True(input.Success);
            // Exercise the lazy type index too. A mutation can be valid or invalid;
            // the robustness contract here is no uncaught malformed-input exception.
            _ = resolver.ResolveType(input.Value!, "Cilica.Fixtures.Basic", "Outer", "Inner");
        }
    }
}
