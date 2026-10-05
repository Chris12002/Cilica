using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cilica.LibraryBuild;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryIntrinsicRequestTests
{
    [Theory]
    [InlineData("missing")] [InlineData("ambiguous")] [InlineData("duplicate")] [InlineData("wrong-kind")]
    public async Task Unresolved_intrinsic_requests_publish_nothing_and_clean_staging(string shape)
    {
        using var files = new ResolverFiles();
        string source = files.Write("Probe.cs", Encoding.UTF8.GetBytes("namespace LibraryTrial { public static class Probe { public static int M() => 1; public static int M(int x) => x; public static int M(string x) => 2; } }"), "source");
        var selector = new LibraryIntrinsicRequest("LibraryTrial", "Probe", shape == "missing" ? "Absent" : "M", shape == "ambiguous" ? 1 : 0,
            shape == "wrong-kind" ? LibraryIntrinsicCapability.StringLayout : LibraryIntrinsicCapability.StringLength);
        var request = LibraryBuildProducerTests.Request(files, source) with { Intrinsics = shape == "duplicate" ? [selector, selector] : [selector] };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => LibraryBuilder.Build(request));
        Assert.Contains(shape switch { "missing" => "not emitted", "ambiguous" => "ambiguous", "duplicate" => "Duplicate", _ => "appropriate" }, failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(request.OutputDirectory)); Assert.Empty(Directory.EnumerateDirectories(files.Root, ".cilica-build-*"));
    }

    [Theory]
    [InlineData("missing-capability")] [InlineData("duplicate-property")] [InlineData("unknown-property")]
    [InlineData("unknown-capability")] [InlineData("numeric-capability")] [InlineData("null")]
    public void Request_JSON_must_be_explicit_and_unambiguous(string shape)
    {
        using var files = new ResolverFiles();
        string json = "[{\"Namespace\":\"System\",\"Type\":\"String\",\"Member\":null,\"ParameterCount\":null,\"Capability\":\"StringLayout\"}]";
        json = shape switch {
            "missing-capability" => json.Replace(",\"Capability\":\"StringLayout\"", "", StringComparison.Ordinal),
            "duplicate-property" => json.Replace("\"Type\":\"String\"", "\"Type\":\"String\",\"Type\":\"Exception\"", StringComparison.Ordinal),
            "unknown-property" => json.Replace("\"Namespace\"", "\"Unexpected\"", StringComparison.Ordinal),
            "unknown-capability" => json.Replace("StringLayout", "Everything", StringComparison.Ordinal),
            "numeric-capability" => json.Replace("\"StringLayout\"", "0", StringComparison.Ordinal),
            _ => "null"
        };
        var path = files.Write("requests.json", Encoding.UTF8.GetBytes(json));
        Assert.Throws<JsonException>(() => LibraryIntrinsicRequests.Read(path));
    }

    [Fact]
    public async Task Core_profile_and_request_bounds_are_checked_before_creating_output()
    {
        using var files = new ResolverFiles();
        var request = LibraryBuildProducerTests.Request(files, "not-read.cs");
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with { CoreLibrary = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with { References = [] }));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with {
            Intrinsics = [new("System", "String", null, 0, LibraryIntrinsicCapability.StringLayout)] }));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with {
            Intrinsics = [new("System", "String", null, null, (LibraryIntrinsicCapability)999)] }));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with {
            Intrinsics = [new("System", "String", "Empty", 0, LibraryIntrinsicCapability.StringEmpty)] }));
        await Assert.ThrowsAsync<ArgumentException>(() => LibraryBuilder.Build(request with {
            Intrinsics = [new("System", "String", null, null, LibraryIntrinsicCapability.StringEmpty)] }));
        Assert.False(Directory.Exists(request.OutputDirectory)); Assert.False(Directory.Exists(files.Root));
    }

    [Fact]
    public void Declaration_selection_is_bounded_and_never_grants_authority()
    {
        using var files = new ResolverFiles(); using var resolver = files.Open();
        var b = new ResolutionAssemblyBuilder(new("Requests", new Version(1, 0, 0, 0)));
        var ownerHandle = b.Type("System", "String", attributes: TypeAttributes.Public | TypeAttributes.Sealed);
        b.Method("get_Length", [0x20, 0, 8], attributes: MethodAttributes.Public | MethodAttributes.SpecialName);
        b.Method("Unused", [0, 0, 1]);
        var image = resolver.OpenInput(files.Write("Requests.dll", b.Build())).Value!;
        var owner = resolver.ResolveType(image, ownerHandle).Value!;
        var found = resolver.DescribeLibraryMethodIntrinsic(owner, "get_Length", 0, LibraryIntrinsicCapability.StringLength, new(3));
        Assert.True(found.Success, string.Join("; ", found.Diagnostics));
        Assert.Equal(0x06000001, found.Value!.Token); Assert.Null(resolver.ApprovedLibraryManifestSha256);
        var shortRead = resolver.DescribeLibraryMethodIntrinsic(owner, "get_Length", 0, LibraryIntrinsicCapability.StringLength, new(2));
        Assert.False(shortRead.Success); Assert.Contains(shortRead.Diagnostics, d => d.Id == DiagnosticIds.TranslationLimit);
        Assert.Equal(resolver.DescribeLibraryIntrinsic(image, MetadataTokens.MethodDefinitionHandle(1), LibraryIntrinsicCapability.StringLength).Value, found.Value);
    }
}
