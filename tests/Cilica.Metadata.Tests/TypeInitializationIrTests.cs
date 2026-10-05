using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class TypeInitializationIrTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Basis = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(TypeInitializationExceptionProbe), ["Payload"]));
    private static async Task<ManagedExceptionIrProgram> Model(bool failure)
    {
        var program = (await Basis.Value)[0]; var flow = program.Flow.Flow.Flow;
        var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        var binding = program.Flow.Bindings.Single(b => b.Method == flow.Entry);
        var source = entry.Source with { Token = 0x0600fff0, Name = "Initialized::.cctor" };
        var id = IrMethodId.From(source); var counter = new ObjectFieldId(binding.Owner, 0x0400fff0);
        var statements = ImmutableArray.Create<IrStatement>(
            new IrStatement.InitializeType(0, id), // Reentrant initialization must not execute twice.
            new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.LoadStatic(counter)), IrExceptionTarget.MethodExit),
            new IrStatement.Define(new(1, IrType.Int32, 0, new IrExpression.Constant(1))),
            new IrStatement.Define(new(2, IrType.Int32, 0, new IrExpression.Binary(Int32Operation.AddWrapping, 0, 1))),
            new IrStatement.StoreStatic(0, counter, 2));
        if (failure) statements = statements.Add(new IrStatement.Define(new(3, IrType.Reference(program.Exceptions.Base), 0,
            new IrExpression.NewException(program.Exceptions.Base)), IrExceptionTarget.MethodExit));
        var initializer = new IrMethod(source, [], IrType.Void,
            [new(0, 0, [], statements, failure ? new IrThrow(1, 3) : new IrVoidReturn(1))], []);
        var changed = entry with { Blocks = entry.Blocks.SetItem(0, entry.Blocks[0] with {
            Statements = entry.Blocks[0].Statements.InsertRange(0, ImmutableArray.Create<IrStatement>(new IrStatement.InitializeType(0, id), new IrStatement.InitializeType(0, id))) }) };
        var types = flow.Types with { References = flow.Types.References with { Statics = [new(counter, IrType.Int32, FieldAttributes.Private | FieldAttributes.Static, true)] } };
        var result = program with { Flow = program.Flow with {
            Bindings = program.Flow.Bindings.Add(new(id, binding.Owner, ObjectMethodKind.Static,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, true) { TypeInitializerName = "Initialized" }),
            Flow = program.Flow.Flow with { Flow = flow with { Types = types, Methods = flow.Methods.Replace(entry, changed).Add(initializer) } } } };
        ManagedExceptionIrValidator.Validate(result); return result;
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task Initialization_is_once_per_instance_reentrant_and_caches_failure(bool optimized, bool failure)
    {
        var program = await Model(failure);
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var entry = program.Flow.Flow.Flow.Entry;
        string method = $"cilica_m_{entry.ContentHash}_{entry.Token:x8}";
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            [[gnu::noinline]] static void run(Context& c) {
                LocationArgument args[2]{};
                (void){{method}}(c, args);
                assert(c.pending == {{(failure ? 13 : 0)}}u && c.instance.statics[0].bits == 1);
                auto& slot = c.instance.initializers[0];
                assert(slot.state == {{(failure ? 3 : 2)}}u);
                auto* saved = slot.failure;
                if (saved != nullptr) {
                    assert(c.exception == saved && exception_inner(c, saved).value != nullptr);
                    assert(string_length(exception_type_name(c, saved).value) == 11);
                }
                c.pending = 0; c.exception = nullptr;
                assert(collect(c).status == MarkStatus::complete);
                (void){{method}}(c, args);
                assert(c.pending == {{(failure ? 13 : 0)}}u && c.instance.statics[0].bits == 1 && slot.failure == saved);
                assert(c.top == nullptr && c.depth == 0 && c.used == 0);
            }
            [[gnu::noinline]] static void instance() {
                Instance heap; heap.collect_before_allocation = true; Context c(heap); run(c);
            }
            int main() { instance(); instance(); }
            """;
        await Native(source, optimized);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Preparation_retry_managed_failure_fatal_failure_and_corruption_are_bounded(bool optimized)
    {
        var program = await Model(false); var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(program));
        var type = program.Exceptions.TypeInitialization!;
        var source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static int calls = 0;
            static void success(Context&, const LocationArgument*) noexcept { ++calls; }
            static void exhausted(Context& c, const LocationArgument*) noexcept { ++calls; c.instance.limit = c.instance.bytes; c.pending = 1; }
            static void fatal(Context& c, const LocationArgument*) noexcept { ++calls; c.pending = 8; }
            [[gnu::noinline]] static std::uint32_t trigger(Context& c, void (*body)(Context&, const LocationArgument*) noexcept) {
                return initialize_type(c, 0, &t_{{type.ContentHash}}_{{type.Token.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)}}_descriptor, initialization_name_0, 11, body);
            }
            [[gnu::noinline]] static void retry(Context& c) {
                auto& heap = c.instance; calls = 0;
                assert(prepare_exceptions(c) == 0); heap.limit = heap.bytes;
                assert(trigger(c, success) == 5 && calls == 0 && heap.initializers == nullptr);
                c.pending = 0; heap.limit = heap.bytes + sizeof(InitializationSlot);
                assert(trigger(c, success) == 5 && calls == 0 && heap.initializers[0].state == 0);
                c.pending = 0; heap.limit = 1024 * 1024;
                assert(trigger(c, success) == 0 && calls == 1);
                assert(trigger(c, success) == 0 && calls == 1);
                assert(collect(c).status == MarkStatus::complete && heap.count == 1);
            }
            [[gnu::noinline]] static void failures(Context& c, bool managed) {
                auto& heap = c.instance; calls = 0; assert(prepare_exceptions(c) == 0);
                auto body = managed ? exhausted : fatal;
                assert(trigger(c, body) == (managed ? 13u : 8u) && calls == 1);
                auto* saved = heap.initializers[0].failure;
                if (managed) assert(exception_inner(c, saved).value == heap.emergency_exception);
                c.pending = 0; c.exception = nullptr;
                assert(collect(c).status == MarkStatus::complete);
                assert(trigger(c, body) == (managed ? 13u : 8u) && calls == 1 && heap.initializers[0].failure == saved);
                c.pending = 0; c.exception = nullptr;
                heap.initializers[0].failure = reinterpret_cast<Object*>(std::uintptr_t{1});
                assert(trigger(c, body) == 6 && collect(c).status == MarkStatus::invalid);
                heap.initializers[0].failure = saved; c.pending = 0;
                auto fatalCode = heap.initializers[0].fatal; heap.initializers[0].fatal = 1;
                assert(trigger(c, body) == 6 && collect(c).status == MarkStatus::invalid);
                heap.initializers[0].fatal = fatalCode; c.pending = 0;
                assert(collect(c).status == MarkStatus::complete);
            }
            [[gnu::noinline]] static void instance(int mode) {
                Instance heap; heap.collect_before_allocation = true; Context c(heap);
                if (mode == 0) retry(c); else failures(c, mode == 1);
            }
            int main() { instance(0); instance(1); instance(2); }
            """;
        await Native(source, optimized);
    }
    [Fact]
    public async Task Readonly_static_writes_require_the_exact_owning_initializer()
    {
        var program = await Model(false); var flow = program.Flow.Flow.Flow;
        var field = flow.Types.References.Statics[0] with { Attributes = FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly };
        flow = flow with { Types = flow.Types with { References = flow.Types.References with { Statics = [field] } } };
        program = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow } } };
        ManagedExceptionIrValidator.Validate(program);
        var binding = program.Flow.Bindings.Last();
        var changed = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.SetItem(program.Flow.Bindings.Length - 1,
            binding with { Owner = binding.Owner with { Token = binding.Owner.Token + 1 } }) } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(changed));
    }
    internal static async Task Native(string source, bool optimized)
    {
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("type-initialization");
        string cpp = Path.Combine(directory, "initialize.cpp"), exe = Path.Combine(directory, "initialize"); File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory); Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
    [Theory]
    [InlineData("flags")] [InlineData("name")] [InlineData("ordinary-call")] [InlineData("target")]
    public async Task Initializers_require_exact_declarations_and_a_dedicated_trigger(string mutation)
    {
        var program = await Model(false); var flow = program.Flow.Flow.Flow;
        var binding = program.Flow.Bindings.Last(); var entry = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
        if (mutation is "flags" or "name") program = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.SetItem(program.Flow.Bindings.Length - 1,
            mutation == "flags" ? binding with { Attributes = binding.Attributes | MethodAttributes.Public } : binding with { TypeInitializerName = "" }) } };
        else {
            var block = entry.Blocks[0];
            var changed = entry with { Blocks = entry.Blocks.SetItem(0, block with { Statements = block.Statements.SetItem(0,
                mutation == "ordinary-call" ? new IrStatement.Invoke(0, binding.Method, []) : new IrStatement.InitializeType(0, flow.Entry)) }) };
            program = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(entry, changed) } } } };
        }
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(program));
    }
}
