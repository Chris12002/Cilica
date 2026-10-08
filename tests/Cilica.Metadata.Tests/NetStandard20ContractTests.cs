using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.LibraryBuild;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NetStandard20ContractTests
{
    private static T Require<T>(ResolutionResult<T> result) where T : class
    { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Value!; }
    private static void Error<T>(ResolutionResult<T> result, string id) where T : class
    { Assert.False(result.Success); Assert.Equal(id, Assert.Single(result.Diagnostics).Id); }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Earlier_contract_list_probe_matches_normal_dotnet(bool optimized)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMapContracts(files, true);
        using var lifetime = resolver;
        const string source = "using System.Collections.Generic; public static class EarlierProbe { public static int Run(int a,int b) { var items=new List<int>(); items.Add(a); items.Add(b); return unchecked(items.Count*31+items[0]-items[1]); } }";
        var request = LibraryBuildProducerTests.Request(files, files.Write("EarlierProbe.cs", Encoding.UTF8.GetBytes(source), "source"), "probe")
            with { References = [NetStandard20Contract.ReferencePath], AssemblyName = "EarlierProbe" };
        var probe = await LibraryBuilder.Build(request);
        using var scope = Require(resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256));
        var input = Require(scope.OpenInput(probe.AssemblyPath));
        Assert.Contains(NetStandard20Contract.Identity, input.References);
        var owner = Require(scope.ResolveType(input, "", "EarlierProbe"));
        var method = Require(scope.ResolveClosedDeclaration(new(owner, SignatureList<TypeSignature>.Empty), "Run", 2, new()));
        var translated = ValueTranslator.TranslateIntegerObjects(scope, input, method.Handle, implementation);
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("EarlierProbe", translated.Program!, Oracle)]);
        static int Oracle(int a, int b) { var items = new System.Collections.Generic.List<int> { a, b }; return unchecked(items.Count * 31 + items[0] - items[1]); }
    }

    [Fact]
    public async Task Missing_earlier_contract_still_reports_CIL3003()
    {
        using var files = new ResolverFiles();
        var (_, _, resolver, _, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var caller = new ResolutionAssemblyBuilder(new("Caller", new(1, 0, 0, 0)));
        var token = caller.TypeReference("System", "String", caller.Reference(NetStandard20Contract.Identity));
        var input = Require(resolver.OpenInput(files.Write("caller.dll", caller.Build(), "input")));
        Error(resolver.ResolveLibraryType(plan, input, token), DiagnosticIds.AssemblyNotFound);
    }

    [Fact]
    public async Task Exact_identity_forwarders_and_member_boundaries_remain_checked()
    {
        using var files = new ResolverFiles();
        var (_, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMapContracts(files, true);
        using var lifetime = resolver;
        using var scope = resolver.CreateLibraryCompilationScope(plan);
        var contract = Require(resolver.ResolveAssembly(NetStandard20Contract.Identity));
        var contracts = plan.Bindings.Select(b => b.Contract.Assembly.Version).Distinct().ToArray();
        Assert.Contains(new Version(2, 0, 0, 0), contracts); Assert.Contains(new Version(2, 1, 0, 0), contracts);
        Assert.All(plan.Bindings.Where(b => b.Contract.Assembly.Equals(NetStandard20Contract.Identity)), b => Assert.Equal(NetStandard20Contract.ContentHash, b.Contract.ContentHash));
        var facade = new ResolutionAssemblyBuilder(new("Facade", new(1, 0, 0, 0)));
        facade.Forward("System", "String", facade.Reference(NetStandard20Contract.Identity));
        var forwarded = Require(resolver.OpenInput(files.Write("facade.dll", facade.Build(), "input")));
        Assert.Equal(contract, Require(resolver.ResolveType(forwarded, "System", "String")).Assembly);
        Assert.Equal(implementation, Require(scope.ResolveType(forwarded, "System", "String")).Assembly);
        Assert.Equal(contract, Require(resolver.ResolveType(forwarded, "System", "String")).Assembly);
        foreach (var identity in new[] {
            new AssemblyIdentity("netstandard", new(2, 0, 0, 1), "neutral", "cc7b13ffcd2ddd51"),
            new AssemblyIdentity("netstandard", new(2, 0, 0, 0), "neutral", "cc7b13ffcd2ddd50"),
            new AssemblyIdentity("netstandard", new(2, 0, 0, 0), "en-US", "cc7b13ffcd2ddd51") })
        {
            var wrong = new ResolutionAssemblyBuilder(new("Wrong", new(1, 0, 0, 0)));
            wrong.Forward("System", "String", wrong.Reference(identity));
            var input = Require(resolver.OpenInput(files.Write(Guid.NewGuid()+".dll", wrong.Build(), "input")));
            Error(scope.ResolveType(input, "System", "String"), DiagnosticIds.AssemblyNotFound);
        }
        var earlierOrdered = Require(resolver.ResolveType(contract, "System.Linq", "IOrderedEnumerable`1"));
        var ownedOrdered = Require(resolver.ResolveType(implementation, "System.Linq", "IOrderedEnumerable`1"));
        Error(resolver.CreateLibraryBindingPlan([new(earlierOrdered, ownedOrdered)]), DiagnosticIds.LibraryBindingInvalid);
        Error(resolver.ResolveLibraryType(plan, contract, earlierOrdered.Handle), DiagnosticIds.LibraryTypeUnmapped);
        var caller = new ResolutionAssemblyBuilder(new("Members", new(1, 0, 0, 0)));
        var reference = caller.Reference(NetStandard20Contract.Identity);
        var text = caller.TypeReference("System", "String", reference);
        var length = caller.Member(text, "get_Length", 0x20, 0, 8);
        var wrongSignature = caller.Member(text, "get_Length", 0x20, 0, 10);
        var unsupported = caller.Member(text, "Normalize", 0x20, 0, 0x0e);
        var newApi = caller.TypeReference("System", "HashCode", reference);
        var newInterface = caller.TypeReference("System.Runtime.CompilerServices", "ITuple", reference);
        var unmapped = caller.TypeReference("System", "Uri", reference);
        var inputMembers = Require(resolver.OpenInput(files.Write("members.dll", caller.Build(), "input")));
        var matched = Require(resolver.ResolveLibraryMethod(plan, inputMembers, length));
        Assert.Equal(contract, matched.Contract.DeclaringType.Definition.Assembly);
        Assert.Equal(implementation, matched.Implementation.DeclaringType.Definition.Assembly);
        Error(resolver.ResolveLibraryMethod(plan, inputMembers, wrongSignature), DiagnosticIds.ResolutionUnsupported);
        Error(resolver.ResolveLibraryMethod(plan, inputMembers, unsupported), DiagnosticIds.MemberNotFound);
        Error(resolver.ResolveLibraryType(plan, inputMembers, newApi), DiagnosticIds.TypeNotFound);
        Error(resolver.ResolveLibraryType(plan, inputMembers, newInterface), DiagnosticIds.TypeNotFound);
        Error(resolver.ResolveLibraryType(plan, inputMembers, unmapped), DiagnosticIds.LibraryTypeUnmapped);
        var changed = Require(resolver.OpenInput(files.Write("changed.dll", ChangedContract(), "changed")));
        var original = Require(resolver.ResolveType(contract, "System", "String"));
        Error(resolver.ResolveLibraryType(plan, changed, original.Handle), DiagnosticIds.LibraryBindingInvalid);
    }

    [Fact]
    public void Conflicting_bytes_for_the_exact_earlier_identity_are_not_selected_by_directory_order()
    {
        using var files = new ResolverFiles();
        files.Write("duplicate.dll", ChangedContract());
        foreach (bool reversed in new[] { false, true })
        {
            string[] dirs = [Path.GetDirectoryName(NetStandard20Contract.ReferencePath)!, files.DirectoryPath("refs")];
            using var resolver = Require(AssemblyResolver.Create(reversed ? dirs.Reverse() : dirs));
            Error(resolver.ResolveAssembly(NetStandard20Contract.Identity), DiagnosticIds.AssemblyConflict);
        }
    }

    private static byte[] ChangedContract()
    {
        var bytes = File.ReadAllBytes(NetStandard20Contract.ReferencePath);
        using var pe = new PEReader(new MemoryStream(bytes));
        bytes[pe.PEHeaders.CoffHeaderStartOffset + 4] ^= 1; // timestamp only: valid metadata, same identity, changed hash
        return bytes;
    }
}
