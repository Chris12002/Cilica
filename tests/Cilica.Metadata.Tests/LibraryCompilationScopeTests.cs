using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class LibraryCompilationScopeTests
{
    private static AssemblyIdentity Id(string name) => new(name, new Version(1, 0, 0, 0));
    private static T Success<T>(ResolutionResult<T> result) where T : class
    { Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Value!; }

    private sealed class Fixture : IDisposable
    {
        internal readonly ResolverFiles Files = new();
        internal readonly AssemblyResolver Source, Scope;
        internal readonly ResolvedAssembly Contract, Implementation, App;
        internal readonly LibraryBindingPlan Plan;
        internal readonly MethodDefinitionHandle Entry;
        internal readonly TypeReferenceHandle Api;
        internal readonly MemberReferenceHandle Call;
        internal Fixture()
        {
            var a = new ResolutionAssemblyBuilder(Id("Contract")); a.MarkReferenceAssembly();
            a.Type("Example", "Api"); a.Method("Increment", [0, 1, 8, 8]);
            a.Type("Example", "Unmapped");
            var b = new ResolutionAssemblyBuilder(Id("Library"));
            b.Type("Example", "Api");
            b.Method("Increment", [0, 1, 8, 8], emit: il =>
            { il.LoadArgument(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret); });
            var left = Files.Write("contract.dll", a.Build()); var right = Files.Write("library.dll", b.Build());
            Source = Files.Open("refs"); Contract = Success(Source.OpenInput(left)); Implementation = Success(Source.OpenInput(right));
            Plan = Success(Source.CreateLibraryBindingPlan([new(Success(Source.ResolveType(Contract, "Example", "Api")),
                Success(Source.ResolveType(Implementation, "Example", "Api")))]));
            Scope = Source.CreateLibraryCompilationScope(Plan);
            var app = new ResolutionAssemblyBuilder(Id("App"));
            Api = app.TypeReference("Example", "Api", app.Reference(Id("Contract")));
            Call = app.Member(Api, "Increment", 0, 1, 8, 8);
            app.Type("Example", "Program");
            app.Field("Api", 6, 0x12, 5);
            Entry = app.Method("Run", [0, 1, 8, 8], emit: il =>
            { il.LoadArgument(0); il.Call(Call); il.Call(Call); il.OpCode(ILOpCode.Ret); });
            app.Method("WithLocal", [0, 0, 1], emit: il => il.OpCode(ILOpCode.Ret), locals: [7, 1, 0x12, 5]);
            // Opening through a view joins the source's immutable snapshot catalogue.
            App = Success(Scope.OpenInput(Files.Write("app.dll", app.Build(), "input")));
        }
        public void Dispose() { Scope.Dispose(); Source.Dispose(); Files.Dispose(); }
    }

    [Fact]
    public void View_maps_members_locals_and_fields_without_mutating_strict_resolution()
    {
        using var f = new Fixture();
        Assert.True(f.Scope.IsLibraryCompilationScope); Assert.False(f.Source.IsLibraryCompilationScope);
        Assert.Equal(f.Implementation, Success(f.Scope.ResolveType(f.App, f.Api)).Assembly);
        Assert.Equal(f.Contract, Success(f.Source.ResolveType(f.App, f.Api)).Assembly);
        Assert.Equal(f.Implementation, Success(f.Scope.ResolveDeclaredMethod(f.App, f.Call)).DeclaringType.Definition.Assembly);
        Assert.Equal(f.Contract, Success(f.Source.ResolveDeclaredMethod(f.App, f.Call)).DeclaringType.Definition.Assembly);
        var expected = new TypeSignature.Named(Success(f.Source.ResolveType(f.Implementation, "Example", "Api")), SignatureTypeKind.Class);
        var input = Success(f.Scope.ReadMethodInput(f.App, MetadataTokens.MethodDefinitionHandle(2)));
        Assert.Equal(expected, Assert.Single(input.Locals));
        Assert.Equal(expected, Assert.Single(Success(f.Scope.ReadTypeInput(f.App, MetadataTokens.TypeDefinitionHandle(2))).Fields).Signature);
        Assert.Equal(expected, Success(f.Scope.ResolveDeclaredField(f.App, MetadataTokens.FieldDefinitionHandle(1))).Signature);
    }

    [Fact]
    public void View_and_source_disposal_have_explicit_one_way_lifetimes()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Scope.CreateLibraryCompilationScope(f.Plan));
        Assert.Throws<InvalidOperationException>(() => f.Scope.CreateLibraryBindingPlan([]));
        Assert.Throws<InvalidOperationException>(() => f.Scope.AnalyzeReachability(f.App, [f.Entry]));
        Assert.Throws<InvalidOperationException>(() => f.Scope.AnalyzePublicSurface(f.App));
        f.Scope.Dispose();
        Assert.Equal(f.Contract, Success(f.Source.ResolveType(f.App, f.Api)).Assembly);
        Assert.Throws<ObjectDisposedException>(() => f.Scope.ResolveDeclaredMethod(f.App, f.Call));
        using var otherView = f.Source.CreateLibraryCompilationScope(f.Plan);
        f.Source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => otherView.ResolveType(f.App, f.Api));
        Assert.Throws<ObjectDisposedException>(() => otherView.ResolveAssembly(Id("Contract")));
        Assert.Throws<ObjectDisposedException>(() => otherView.OpenInput("missing.dll"));
    }

    [Fact]
    public void Missing_mappings_and_changed_images_are_diagnostic_in_the_view()
    {
        using var f = new Fixture();
        Assert.Equal(DiagnosticIds.LibraryTypeUnmapped, Assert.Single(f.Scope.ResolveType(f.Contract, "Example", "Unmapped").Diagnostics).Id);
        var changed = new ResolutionAssemblyBuilder(Id("Contract")); changed.MarkReferenceAssembly();
        changed.Type("Example", "Api"); changed.Method("Other", [0, 0, 1]);
        var image = Success(f.Scope.OpenInput(f.Files.Write("changed.dll", changed.Build(), "input")));
        Assert.Equal(DiagnosticIds.LibraryBindingInvalid, Assert.Single(f.Scope.ResolveDeclaredMethod(image, MetadataTokens.MethodDefinitionHandle(1)).Diagnostics).Id);
        Assert.Equal(DiagnosticIds.LibraryBindingInvalid, Assert.Single(f.Scope.ResolveType(image, "Example", "Api").Diagnostics).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_calls_compile_implementation_IL_and_match_authored_dotnet_arithmetic(bool optimized)
    {
        using var f = new Fixture();
        Assert.False(Int32Translator.TranslateProgram(f.Source, f.App, f.Entry).Success);
        var result = Int32Translator.TranslateProgram(f.Scope, f.App, f.Entry);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains(result.Program!.Methods, m => m.Source.ContentHash == f.Implementation.ContentHash);
        Assert.DoesNotContain(result.Program.Methods, m => m.Source.ContentHash == f.Contract.ContentHash);
        var artifact = CppEmitter.EmitProgram(result.Program);
        var directory = f.Files.DirectoryPath("native"); var source = Path.Combine(directory, "mapped.cpp");
        var executable = Path.Combine(directory, "mapped");
        File.WriteAllText(source, artifact.Text + "\n#include <iostream>\nint main() { std::int32_t x; while (std::cin >> x) std::cout << "
            + artifact.EntryPoint + "(x) << '\\n'; }\n");
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-fsanitize=undefined", "-fno-sanitize-recover=all", source, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        int[] values = [int.MinValue, -1, 0, 1, int.MaxValue - 1, int.MaxValue];
        var run = await NativeProcess.Run(executable, [], directory, string.Join("\n", values.Select(x => x.ToString(CultureInfo.InvariantCulture))) + "\n");
        Assert.Equal(0, run.ExitCode); Assert.Empty(run.Stderr);
        Assert.Equal(values.Select(x => unchecked(x + 2).ToString(CultureInfo.InvariantCulture)), run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
}
