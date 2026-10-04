using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryBuildAdmissionTests
{
    private static T Success<T>(ResolutionResult<T> result) where T : class
    { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Value!; }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static AssemblyIdentity Id(string name) => new(name, new Version(1, 0, 0, 0));
    private static readonly string[] ExceptionNames = ["Exception", "SystemException", "ArithmeticException", "DivideByZeroException", "OverflowException",
        "NullReferenceException", "OutOfMemoryException", "InvalidCastException", "IndexOutOfRangeException", "ArrayTypeMismatchException", "ArgumentException"];

    private sealed class Fixture : IDisposable
    {
        internal readonly ResolverFiles Files = new();
        internal readonly AssemblyResolver Resolver;
        internal readonly ResolvedAssembly Library, App;
        internal readonly LibraryBindingPlan Plan;
        internal readonly LibraryBuildManifest Manifest;
        internal readonly MethodDefinitionHandle Entry;
        internal readonly MethodDefinitionHandle ExceptionEntry;
        internal readonly TypeDefinitionHandle Text = MetadataTokens.TypeDefinitionHandle(3);
        internal readonly MethodDefinitionHandle Length = MetadataTokens.MethodDefinitionHandle(1);
        internal Fixture()
        {
            static byte[] Image(string name, bool contract)
            {
                var b = new ResolutionAssemblyBuilder(Id(name)); if (contract) b.MarkReferenceAssembly();
                var obj = b.Type("System", "Object");
                b.Type("System", "String", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Sealed);
                b.Method("get_Length", [0x20, 0, 8], attributes: MethodAttributes.Public | MethodAttributes.SpecialName);
                b.Method("get_Other", [0x20, 0, 8], attributes: MethodAttributes.Public | MethodAttributes.SpecialName);
                b.Type("System", "ValueType", baseType: obj, attributes: TypeAttributes.Public | TypeAttributes.Abstract);
                TypeDefinitionHandle exception = default, system = default, arithmetic = default;
                foreach (var type in ExceptionNames)
                {
                    var parent = type == "Exception" ? obj : type == "SystemException" ? exception
                        : type is "DivideByZeroException" or "OverflowException" ? arithmetic : system;
                    var definition = b.Type("System", type, baseType: parent);
                    if (type == "Exception")
                    {
                        exception = definition;
                        b.Method(".ctor", [0x20, 1, 1, 0x0e], attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                        b.Method("get_Message", [0x20, 0, 0x0e], attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.NewSlot);
                    }
                    if (type == "SystemException") system = definition;
                    if (type == "ArithmeticException") arithmetic = definition;
                }
                return b.Build();
            }
            var left = Files.Write("reference.dll", Image("Contract", true)); var right = Files.Write("library.dll", Image("Cilica.Core", false));
            Resolver = Files.Open("refs"); var contract = Success(Resolver.OpenInput(left)); Library = Success(Resolver.OpenInput(right));
            Plan = Success(Resolver.CreateLibraryBindingPlan(new[] { "Object", "String", "ValueType" }.Concat(ExceptionNames).Select(name => new LibraryTypeMapping(
                Success(Resolver.ResolveType(contract, "System", name)), Success(Resolver.ResolveType(Library, "System", name))))));
            Manifest = new(LibraryBuildManifest.FormatName, 1, LibraryBuildManifest.CurrentRuntimeProfile,
                [new(Library.Identity.DisplayName, Library.ContentHash,
                    [new("source", "Core.cs", Hash([1])), new("compiler", "csc.dll", Hash([2]))],
                    ["/nostdlib+", "/target:library", "Core.cs"],
                    [Success(Resolver.DescribeLibraryIntrinsic(Library, Text, LibraryIntrinsicCapability.StringLayout)),
                        Success(Resolver.DescribeLibraryIntrinsic(Library, Length, LibraryIntrinsicCapability.StringLength)),
                        .. ExceptionNames.Select(name => Success(Resolver.DescribeLibraryIntrinsic(Library,
                            Success(Resolver.ResolveType(Library, "System", name)).Handle, LibraryIntrinsicCapability.ExceptionLayout))),
                        Success(Resolver.DescribeLibraryIntrinsic(Library, MetadataTokens.MethodDefinitionHandle(3), LibraryIntrinsicCapability.ExceptionConstructor)),
                        Success(Resolver.DescribeLibraryIntrinsic(Library, MetadataTokens.MethodDefinitionHandle(4), LibraryIntrinsicCapability.ExceptionMessage))])]);
            var app = new ResolutionAssemblyBuilder(Id("App")); var reference = app.Reference(Id("Contract"));
            var obj = app.TypeReference("System", "Object", reference); var text = app.TypeReference("System", "String", reference);
            var length = app.Member(text, "get_Length", 0x20, 0, 8); var literal = app.String("A😀");
            app.Type("Example", "Program", baseType: obj);
            Entry = app.Method("Run", [0, 0, 8], emit: il =>
            { il.LoadString(literal); il.OpCode(ILOpCode.Callvirt); il.Token(length); il.OpCode(ILOpCode.Ret); });
            var exceptionType = app.TypeReference("System", "Exception", reference);
            var ctor = app.Member(exceptionType, ".ctor", 0x20, 1, 1, 0x0e); var message = app.Member(exceptionType, "get_Message", 0x20, 0, 0x0e);
            ExceptionEntry = app.Method("ExceptionRun", [0, 0, 8], emit: il =>
            {
                il.LoadString(literal); il.OpCode(ILOpCode.Newobj); il.Token(ctor);
                il.OpCode(ILOpCode.Callvirt); il.Token(message); il.OpCode(ILOpCode.Callvirt); il.Token(length); il.OpCode(ILOpCode.Ret);
            });
            App = Success(Resolver.OpenInput(Files.Write("app.dll", app.Build(), "input")));
        }
        internal ResolutionResult<AssemblyResolver> Admit(LibraryBuildManifest? manifest = null)
        {
            var bytes = AssemblyResolver.SerializeLibraryBuildManifest(manifest ?? Manifest);
            return Resolver.CreateTrustedLibraryCompilationScope(Plan, bytes, Hash(bytes));
        }
        public void Dispose() { Resolver.Dispose(); Files.Dispose(); }
    }

    [Fact]
    public void Exact_reviewed_grants_enable_translation_and_are_scoped_to_their_member()
    {
        using var f = new Fixture(); using var scope = Success(f.Admit());
        Assert.Equal(Hash(AssemblyResolver.SerializeLibraryBuildManifest(f.Manifest)), scope.ApprovedLibraryManifestSha256);
        Assert.True(scope.AuthorizeLibraryIntrinsic(f.Library, f.Length, LibraryIntrinsicCapability.StringLength).Success);
        Assert.False(scope.AuthorizeLibraryIntrinsic(f.Library, MetadataTokens.MethodDefinitionHandle(2), LibraryIntrinsicCapability.StringLength).Success);
        Assert.False(scope.AuthorizeLibraryIntrinsic(f.Library, f.Length, LibraryIntrinsicCapability.StringEqual).Success);
        var compiled = ObjectTranslator.Translate(scope, f.App, f.Entry, f.Library);
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics));
        Assert.NotEmpty(ObjectCppEmitter.EmitProgram(compiled.Program!).Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Type_mapping_or_layout_permission_alone_cannot_enable_member_intrinsics(bool layoutOnly)
    {
        using var f = new Fixture(); var artifact = f.Manifest.Artifacts[0];
        using var scope = layoutOnly ? Success(f.Admit(f.Manifest with { Artifacts = [artifact with { Grants = [artifact.Grants[0]] }] }))
            : f.Resolver.CreateLibraryCompilationScope(f.Plan);
        var result = ObjectTranslator.Translate(scope, f.App, f.Entry, f.Library);
        Assert.False(result.Success);
        Assert.Equal(DiagnosticIds.LibraryIntrinsicDenied, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(LibraryIntrinsicCapability.ExceptionLayout)]
    [InlineData(LibraryIntrinsicCapability.ExceptionConstructor)]
    [InlineData(LibraryIntrinsicCapability.ExceptionMessage)]
    [InlineData(LibraryIntrinsicCapability.StringLength)]
    public void Exception_translation_requires_each_reached_capability(LibraryIntrinsicCapability? omitted)
    {
        using var f = new Fixture(); var artifact = f.Manifest.Artifacts[0];
        using var scope = Success(f.Admit(f.Manifest with { Artifacts = [artifact with { Grants = [.. artifact.Grants.Where(g => g.Capability != omitted)] }] }));
        var result = ValueTranslator.TranslateExceptions(scope, f.App, f.ExceptionEntry, f.Library);
        if (omitted is null) Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        else { Assert.False(result.Success); Assert.Equal(DiagnosticIds.LibraryIntrinsicDenied, Assert.Single(result.Diagnostics).Id); }
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("format")]
    [InlineData("version")]
    [InlineData("output")]
    [InlineData("identity")]
    [InlineData("missing-artifact")]
    [InlineData("duplicate-artifact")]
    [InlineData("inputs")]
    [InlineData("compiler")]
    [InlineData("source")]
    [InlineData("input-hash")]
    [InlineData("input-role")]
    [InlineData("duplicate-input")]
    [InlineData("arguments")]
    [InlineData("owner")]
    [InlineData("name")]
    [InlineData("signature")]
    [InlineData("token")]
    [InlineData("token-kind")]
    [InlineData("duplicate-grant")]
    public void Invalid_attestations_fail_even_when_the_host_approved_their_bytes(string change)
    {
        using var f = new Fixture(); var manifest = f.Manifest; var artifact = manifest.Artifacts[0]; var grant = artifact.Grants[1];
        artifact = change switch
        {
            "output" => artifact with { Sha256 = Hash([255]) },
            "identity" => artifact with { AssemblyIdentity = "Other" },
            "inputs" => artifact with { Inputs = [] },
            "compiler" => artifact with { Inputs = [artifact.Inputs[0]] },
            "source" => artifact with { Inputs = [artifact.Inputs[1]] },
            "input-hash" => artifact with { Inputs = [artifact.Inputs[0] with { Sha256 = "bad" }, artifact.Inputs[1]] },
            "input-role" => artifact with { Inputs = [artifact.Inputs[0] with { Role = "untracked" }, artifact.Inputs[1]] },
            "duplicate-input" => artifact with { Inputs = [.. artifact.Inputs, artifact.Inputs[0]] },
            "arguments" => artifact with { CompilerArguments = [] },
            "owner" => artifact with { Grants = [grant with { OwnerToken = 0x02000002 }] },
            "name" => artifact with { Grants = [grant with { Member = "get_Other" }] },
            "signature" => artifact with { Grants = [grant with { SignatureSha256 = Hash([255]) }] },
            "token" => artifact with { Grants = [grant with { Token = 0x06000002 }] },
            "token-kind" => artifact with { Grants = [grant with { Token = 0x02000003 }] },
            "duplicate-grant" => artifact with { Grants = [grant, grant] },
            _ => artifact,
        };
        manifest = change switch
        {
            "profile" => manifest with { RuntimeProfile = "future" },
            "format" => manifest with { Format = "other" },
            "version" => manifest with { Version = 2 },
            "missing-artifact" => manifest with { Artifacts = [] },
            "duplicate-artifact" => manifest with { Artifacts = [artifact, artifact] },
            _ => manifest with { Artifacts = [artifact] },
        };
        var result = f.Admit(manifest); Assert.Null(result.Value);
        Assert.Equal(DiagnosticIds.LibraryBuildInvalid, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("oversize")]
    [InlineData("malformed")]
    public void Manifest_bytes_are_bounded_unambiguous_and_independently_pinned(string change)
    {
        using var f = new Fixture(); byte[] bytes = AssemblyResolver.SerializeLibraryBuildManifest(f.Manifest);
        bytes = change switch
        {
            "duplicate" => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"Version\":1", "\"Version\":2,\"Version\":1", StringComparison.Ordinal)),
            "unknown" => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"Version\":1", "\"Surprise\":true,\"Version\":1", StringComparison.Ordinal)),
            "oversize" => new byte[AssemblyResolver.MaximumLibraryManifestBytes + 1],
            "malformed" => Encoding.UTF8.GetBytes("{invalid"),
            _ => bytes,
        };
        var result = f.Resolver.CreateTrustedLibraryCompilationScope(f.Plan, bytes, change == "hash" ? Hash([9]) : Hash(bytes));
        Assert.Null(result.Value); Assert.Equal(DiagnosticIds.LibraryBuildInvalid, Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approved_string_member_runs_with_the_existing_runtime_at_O0_and_O2(bool optimized)
    {
        using var f = new Fixture(); using var scope = Success(f.Admit());
        var result = ObjectTranslator.Translate(scope, f.App, f.Entry, f.Library);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var artifact = ObjectCppEmitter.EmitProgram(result.Program!);
        string ns = artifact.EntryPoint[..^4] + "_closure";
        string root = "cilica_m_" + f.App.ContentHash + "_" + MetadataTokens.GetToken(f.Entry).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        string directory = f.Files.DirectoryPath("native"), cpp = Path.Combine(directory, "approved.cpp"), exe = Path.Combine(directory, "approved");
        File.WriteAllText(cpp, artifact.Text + "\n#include <iostream>\nint main() { using namespace " + ns
            + "; Instance instance; Context context(instance); std::cout << " + root + "(context, nullptr); return context.pending; }\n");
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-fsanitize=undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.Equal(0, run.ExitCode); Assert.Empty(run.Stderr);
        Assert.Equal("A😀".Length.ToString(System.Globalization.CultureInfo.InvariantCulture), run.Stdout);
    }
}
