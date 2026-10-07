using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata;
using Cilica.Metadata.Resolution;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ObjectDisposedTests
{
    private static readonly string[] Names = ["Default", "Message", "Payload", "Catch", "Null", "ResourceSet"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ObjectDisposedProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Selected_disposed_fault_messages_catches_and_identity_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        var pairs = (from a in Enumerable.Range(0, 16) from b in Enumerable.Range(-1, 64) select (a, b)).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ObjectDisposedProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), pairs);

    }
    [Theory]
    [InlineData("name")] [InlineData("parent")] [InlineData("resource")]
    public async Task Disposed_resource_requires_the_exact_framework_name_and_ancestry(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        var resource = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.ObjectDisposed);
        var layout = flow.Types.References.Types.Single(t => ObjectTypeId.From(t.Source) == resource.Type);
        var changed = mutation == "parent" ? layout with { BaseType = program.Exceptions.Base }
            : layout with { Source = layout.Source with { Name = "Other.ObjectDisposedException" } };
        var invalid = mutation == "resource" ? program with { Exceptions = program.Exceptions with { Resources = program.Exceptions.Resources.Replace(resource, resource with { Type = program.Exceptions.Base }) } }
            : program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = flow.Types with { References = flow.Types.References with { Types = flow.Types.References.Types.Replace(layout, changed) } } } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var boxes = scalar.Flow.Flow;
        var invalidScalar = mutation == "resource" ? scalar with { Exceptions = invalid.Exceptions }
            : scalar with { Flow = scalar.Flow with { Flow = boxes with { Values = boxes.Values with { References = invalid.Flow.Flow.Flow.Types.References } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData("missing")] [InlineData("parameter")] [InlineData("inner")] [InlineData("fault")]
    public async Task Detached_object_names_require_exclusive_payload_and_fault_edges(string mutation)
    {
        var program = (await Programs.Value)[1];
        IrMethod Mutate(IrMethod method)
        {
            var block = method.Blocks.First(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { ObjectName: not null } }));
            var statement = (IrStatement.Define)block.Statements.First(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { ObjectName: not null } });
            var creation = (IrExpression.NewException)statement.Value.Expression;
            var changed = mutation switch {
                "missing" => creation with { ObjectName = null },
                "parameter" => creation with { ParameterName = creation.ObjectName },
                "inner" => creation with { Inner = creation.ObjectName },
                _ => creation };
            var bad = block with { Statements = block.Statements.Replace(statement, statement with {
                Value = statement.Value with { Expression = changed }, Exceptional = mutation == "fault" ? null : statement.Exceptional }) };
            return method with { Blocks = method.Blocks.Replace(block, bad) };
        }
        static IrMethod Target(System.Collections.Immutable.ImmutableArray<IrMethod> methods) => methods.First(m => m.Blocks.Any(b => b.Statements.Any(s => s is IrStatement.Define { Value.Expression: IrExpression.NewException { ObjectName: not null } })));
        var flow = program.Flow.Flow.Flow; var method = Target(flow.Methods);
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, Mutate(method)) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var scalarMethod = Target(objects.Flow.Methods);
        var changedObjects = objects with { Flow = objects.Flow with { Methods = objects.Flow.Methods.Replace(scalarMethod, Mutate(scalarMethod)) } };
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = changedObjects } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Names_remain_rooted_and_message_allocation_failure_preserves_origin(bool optimized)
    {
        var program = (await Programs.Value)[2];
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        string Symbol(ObjectTypeId type) => $"t_{type.ContentHash}_{type.Token:x8}_descriptor";
        var disposed = program.Exceptions.Resources.Single(r => r.Kind == ExceptionResourceKind.ObjectDisposed).Type;
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void create(Context& c, Object** roots) {
                static constexpr std::uint16_t units[] = {110, 0, 0xd800};
                roots[0] = allocate_string(c, {{Symbol(program.Flow.Flow.Flow.Types.References.String!)}}, units, 3, nullptr, 0);
                assert(roots[0] != nullptr);
                roots[2] = make_exception(c, &{{Symbol(disposed)}}, nullptr, 1, roots[0]).value;
                assert(roots[2] != nullptr && exception_object_name(c, roots[2]).value == roots[0]);
                roots[0] = nullptr;
            }
            [[gnu::noinline]] static void verify(Context& c, Object** roots) {
                auto& heap = c.instance;
                assert(collect(c).status == MarkStatus::complete && heap.count == 3);
                heap.limit = heap.bytes; c.pending = 2; c.image = "saved"; c.method = 17; c.il = 31;
                assert(exception_message(c, roots[2]).status == 5);
                assert(c.pending == 2 && c.method == 17 && c.il == 31);
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                assert(payload->message == nullptr && exception_object_name(c, roots[2]).value == payload->parameter_name);
                heap.limit = 1024 * 1024; c.pending = 0;
                roots[1] = exception_message(c, roots[2]).value;
                assert(roots[1] != nullptr && string_length(roots[1]) == 52 && string_code_unit(roots[1], 49) == 0xd800);
                assert(collect(c).status == MarkStatus::complete && heap.count == 5);
                roots[0] = exception_message(c, roots[2]).value;
                assert(roots[0] != nullptr && roots[0] != roots[1] && string_equals(roots[0], roots[1]));
            }
            [[gnu::noinline]] static void format_failure(Context& c, Object** roots) {
                auto& heap = c.instance;
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                auto* cached = payload->message;
                heap.limit = heap.bytes; c.pending = 2; c.method = 19; c.il = 37;
                assert(exception_message(c, roots[2]).status == 5);
                assert(c.pending == 2 && c.method == 19 && c.il == 37 && payload->message == cached);
                assert(exception_object_name(c, roots[2]).value == payload->parameter_name);
                heap.limit = 1024 * 1024; c.pending = 0;
            }
            [[gnu::noinline]] static void corrupt(Context& c, Object** roots) {
                auto& heap = c.instance;
                auto* payload = reinterpret_cast<ExceptionObject*>(roots[2]);
                assert(exception_object_name(c, nullptr).status == 4);
                assert(exception_object_name(c, heap.emergency_exception).status == 6);
                assert(exception_parameter_name(c, roots[2]).status == 6);
                auto* name = payload->parameter_name;
                payload->parameter_name = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(exception_object_name(c, roots[2]).status == 6 && exception_message(c, roots[2]).status == 6);
                assert(collect(c).status == MarkStatus::invalid); payload->parameter_name = name;
                roots[0] = roots[1] = roots[2] = nullptr;
                assert(collect(c).status == MarkStatus::complete && heap.count == 2);
                assert(c.used == 0 && c.borrows == nullptr);
            }
            int main() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                assert(prepare_exceptions(c) == 0);
                Object* roots[3]{}; Frame frame; RootScope scope(c, frame, roots, 3);
                create(c, roots); verify(c, roots); format_failure(c, roots); corrupt(c, roots);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("disposed-gc");
        string cpp = Path.Combine(directory, "payload.cpp"), exe = Path.Combine(directory, "payload");
        await File.WriteAllTextAsync(cpp, source, TestContext.Current.CancellationToken);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++", ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Fact]
    public async Task Layout_and_each_constructor_require_exact_grants_and_inner_ctor_stays_unsupported()
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        var bytes = await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<LibraryBuildManifest>(bytes, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>() } })!;
        var artifact = Assert.Single(manifest.Artifacts);
        var input = resolver.OpenInput(typeof(ObjectDisposedProbe).Assembly.Location).Value!;
        // C# generally names the original Exception virtual slot. Exercise the
        // explicit derived getter operand as raw metadata for its own grant.
        var raw = new ResolutionAssemblyBuilder(new("DisposedGetter", new Version(1, 0, 0, 0)));
        var core = raw.Reference(implementation.Identity);
        var disposedType = raw.TypeReference("System", "ObjectDisposedException", core);
        var stringType = raw.TypeReference("System", "String", core);
        var ctor = raw.Member(disposedType, ".ctor", 0x20, 1, 1, 0x0e);
        var getter = raw.Member(disposedType, "get_Message", 0x20, 0, 0x0e);
        var length = raw.Member(stringType, "get_Length", 0x20, 0, 8);
        raw.Type("Test", "Entry");
        var root = raw.Method("Read", [0, 2, 8, 8, 8], emit: il => {
            il.LoadString(raw.String("name")); il.OpCode(ILOpCode.Newobj); il.Token(ctor);
            il.OpCode(ILOpCode.Callvirt); il.Token(getter);
            il.OpCode(ILOpCode.Callvirt); il.Token(length); il.OpCode(ILOpCode.Ret);
        });
        var rawInput = resolver.OpenInput(files.Write("DisposedGetter.dll", raw.Build())).Value!;
        var grants = artifact.Grants.Where(g => g.Owner == "System.ObjectDisposedException").ToArray(); Assert.Equal(5, grants.Length);
        foreach (var grant in grants)
        {
            var changed = manifest with { Artifacts = [artifact with { Grants = artifact.Grants.Remove(grant) }] };
            var deniedBytes = AssemblyResolver.SerializeLibraryBuildManifest(changed);
            using var denied = resolver.CreateTrustedLibraryCompilationScope(plan, deniedBytes, Convert.ToHexStringLower(SHA256.HashData(deniedBytes))).Value!;
            string name = grant.Capability == LibraryIntrinsicCapability.ExceptionObjectName ? "Payload" : "Message";
            if (grant.Capability == LibraryIntrinsicCapability.ObjectDisposedConstructor
                && resolver.ReadMethodDeclarationInput(implementation, (MethodDefinitionHandle)MetadataTokens.EntityHandle(grant.Token), new()).Value!.Signature.Parameters.Count == 1) name = "Default";
            var result = grant.Capability == LibraryIntrinsicCapability.ExceptionMessage
                ? ValueTranslator.TranslateIntegerObjects(denied, rawInput, root, implementation)
                : ValueTranslator.TranslateIntegerObjects(denied, input, Method(name), implementation);
            Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Id == DiagnosticIds.LibraryIntrinsicDenied);
        }
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, bytes, built.ManifestSha256).Value!;
        var accepted = ValueTranslator.TranslateIntegerObjects(scope, rawInput, root, implementation);
        Assert.True(accepted.Success, string.Join("; ", accepted.Diagnostics));
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedInner"), implementation).Success);
        Assert.False(ValueTranslator.TranslateIntegerObjects(scope, input, Method("UnsupportedDerived"), implementation).Success);
        MethodDefinitionHandle Method(string name) => (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ObjectDisposedProbe).GetMethod(name)!.MetadataToken);
    }
}
