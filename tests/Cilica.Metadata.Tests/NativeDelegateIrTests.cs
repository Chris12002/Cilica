using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Metadata.IL;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeDelegateIrTests
{
    private sealed class Receiver(int number)
    {
        internal int Number = number;
        internal int Add(int value) => unchecked(value + Number);
        internal void Set(int value) => Number = value;
    }
    private static int AddOne(int value) => unchecked(value + 1);
    private static Func<int, int> Make(int number)
    {
        var receiver = new Receiver(number);
        var add = new Func<int, int>(AddOne);
        var captured = new Func<int, int>(receiver.Add);
        var setter = new Action<int>(receiver.Set);
        setter(captured(add(number)));
        return captured;
    }
    private static int Oracle(int number, bool sole) => sole ? Make(number)(number) : Make(number)(0);

    internal static DelegateValueIrProgram Model(int kind)
    {
        var program = DelegateValueIrTests.Model(); var flow = program.Flow.Flow; var root = flow.Methods[0];
        var block = root.Blocks[0]; var statements = block.Statements.ToBuilder();
        if (kind is 1 or 2)
        {
            int index = kind == 1 ? 4 : 5;
            var definition = (IrStatement.Define)statements[index];
            statements[index] = definition with { Value = definition.Value with { Expression = kind == 1
                ? ((IrExpression.NewDelegate)definition.Value.Expression) with { Receiver = 1 }
                : ((IrExpression.CallDelegate)definition.Value.Expression) with { Delegate = 1 } } };
            root = root with { Blocks = [block with { Statements = statements.ToImmutable() }] };
            flow = flow with { Methods = flow.Methods.SetItem(0, root) };
        }
        if (kind == 3)
        {
            var target = flow.Methods[1]; var values = target.Blocks[0].Statements.ToBuilder();
            var constant = (IrStatement.Define)values[1]; var binary = (IrStatement.Define)values[2];
            values[1] = constant with { Value = constant.Value with { Expression = new IrExpression.Constant(0) } };
            values[2] = binary with { Value = binary.Value with { Expression = new IrExpression.Binary(Int32Operation.DivideSigned, 0, 1) }, Exceptional = IrExceptionTarget.MethodExit };
            flow = flow with { Methods = flow.Methods.SetItem(1, target with { Blocks = [target.Blocks[0] with { Statements = values.ToImmutable() }] }) };
        }
        if (kind == 4)
        {
            var delegateType = flow.Types.References.Delegates[0]; var result = IrType.Reference(ObjectTypeId.From(delegateType.Source));
            var makeSource = root.Source with { Token = 0x06000007, Name = "Make" };
            var make = root with { Source = makeSource, ReturnType = result, Blocks = [block with { Terminator = new IrReturn(10, 4) }] };
            var pressureSource = root.Source with { Token = 0x06000008, Name = "Pressure" };
            var pressureBody = new List<IrStatement> { new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))) };
            for (int i = 1; i <= 4; ++i) pressureBody.Add(new IrStatement.Define(new(i, IrType.Reference(program.Bindings[2].Owner), i,
                new IrExpression.NewObject(program.Bindings[4].Method, [0])), IrExceptionTarget.MethodExit));
            var pressure = new IrMethod(pressureSource, [IrType.Int32], IrType.Int32,
                [new(0, 0, [], [.. pressureBody], new IrReturn(5, 0))], []);
            root = root with { Blocks = [new(0, 0, [],
                [new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Parameter(0))),
                 new IrStatement.Define(new(1, result, 1, new IrExpression.Call(IrMethodId.From(makeSource), [0])), IrExceptionTarget.MethodExit),
                 new IrStatement.Define(new(2, IrType.Int32, 2, new IrExpression.Call(IrMethodId.From(pressureSource), [0])), IrExceptionTarget.MethodExit),
                 new IrStatement.Define(new(3, IrType.Int32, 3, new IrExpression.CallDelegate(result.Class!, 1, [0])), IrExceptionTarget.MethodExit)], new IrReturn(4, 3))] };
            flow = flow with { Methods = flow.Methods.SetItem(0, root).Add(make).Add(pressure) };
            program = program with { Bindings = program.Bindings.Add(program.Bindings[0] with { Method = IrMethodId.From(makeSource) })
                .Add(program.Bindings[0] with { Method = IrMethodId.From(pressureSource) }) };
        }
        root = flow.Methods[0] with { Source = flow.Methods[0].Source with { Specialization = new string((char)('1' + kind), 64) } };
        flow = flow with { Entry = IrMethodId.From(root.Source), Methods = flow.Methods.SetItem(0, root) };
        return program with { Flow = new(flow), Bindings = program.Bindings.SetItem(0, program.Bindings[0] with { Method = flow.Entry }) };
    }

    [Fact]
    public void Dotnet_null_binding_and_invocation_have_distinct_exception_types()
    {
        Receiver receiver = null!;
        Assert.Throws<ArgumentException>(() => new Func<int, int>(receiver.Add));
        Func<int, int> callable = null!;
        Assert.Throws<NullReferenceException>(() => callable(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delegates_preserve_identity_captures_roots_faults_and_cleanup(bool optimized)
    {
        var source = new StringBuilder(); var calls = new List<string>();
        for (int kind = 0; kind <= 4; ++kind)
        {
            var program = Model(kind); var flow = program.Flow.Flow;
            var scalar = ValueScalarLowering.LowerDelegates(program);
            var artifact = ObjectCppEmitter.EmitDelegates(scalar);
            string method = "cilica_m_" + flow.Entry.ContentHash + "_" + flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture) + "_g_" + flow.Entry.Specialization;
            uint fault = kind switch { 1 => 12, 2 => 4, 3 => 1, _ => 0 };
            int token = kind == 3 ? flow.Methods[1].Source.Token : flow.Entry.Token;
            int offset = kind switch { 1 => 4, 2 => 5, 3 => 2, _ => 0 };
            source.Append(artifact.Text).Append(CultureInfo.InvariantCulture, $$"""
                #include <cassert>
                [[gnu::noinline]] static void probe_{{kind}}(std::int32_t number, std::int32_t expected) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.object_limit = 6; heap.collect_before_allocation = true; Context c(heap);
                    LocationArgument argument; argument.bits = std::bit_cast<std::uint32_t>(number);
                    for (int repeat = 0; repeat < 3; ++repeat) {
                        auto output = {{method}}(c, &argument);
                        assert(c.pending == {{fault}}u && ({{fault}}u != 0 || output == expected));
                        if ({{fault}}u != 0) assert(c.method == {{token}}u && c.il == {{offset}}u);
                        assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                        c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    }
                    std::int32_t output = 123; std::uint32_t token = 0, offset = 0; const char* image = nullptr;
                    assert({{artifact.EntryPoint}}(&output, &token, &offset, &image, &number, 1) == {{fault}}u);
                    assert(output == ({{fault}}u == 0 ? expected : 123));
                    if ({{fault}}u != 0) assert(token == {{token}}u && offset == {{offset}}u);
                }
                """);
            foreach (int number in new[] { 0, -19, int.MinValue, int.MaxValue })
                calls.Add(FormattableString.Invariant($"probe_{kind}({number}, {(fault == 0 ? Oracle(number, kind == 4) : 0)});"));
            if (kind != 0) continue;
            source.Append(CultureInfo.InvariantCulture, $$"""
                [[gnu::noinline]] static std::size_t arena(std::size_t limit, bool success) {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.collect_before_allocation = true; Context c(heap, limit); LocationArgument argument;
                    auto output = {{method}}(c, &argument);
                    assert(c.pending == (success ? 0u : 9u) && (!success || output == {{Oracle(0, false)}}));
                    assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    return c.peak;
                }
                [[gnu::noinline]] static void invalid_payloads() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap, foreign; Context c(heap), other(foreign);
                    Object* roots[2]{}; Frame frame; RootScope scope(c, frame, roots, 2);
                    roots[0] = make_delegate(c, 0, nullptr).value;
                    roots[1] = make_delegate(c, 0, nullptr).value;
                    assert(roots[0] && roots[1] && roots[0] != roots[1]);
                    const auto* type = delegate_targets[0].type;
                    auto* outsider = make_delegate(other, 0, nullptr).value;
                    auto* forged = reinterpret_cast<Object*>(std::uintptr_t{1});
                    auto* unknown = reinterpret_cast<const Descriptor*>(std::uintptr_t{1});
                    const Descriptor copied = *type;
                    for (Object* bad : {outsider, forged}) assert(prepare_delegate(c, bad, type).status == 6);
                    for (const Descriptor* bad : {unknown, &copied, delegate_targets[2].type})
                        assert(prepare_delegate(c, roots[0], bad).status == 6);
                    auto* payload = reinterpret_cast<DelegateObject*>(roots[0]);
                    payload->target = delegate_targets.size(); assert(prepare_delegate(c, roots[0], type).status == 6);
                    payload->target = 0; payload->receiver = forged; assert(prepare_delegate(c, roots[0], type).status == 6);
                    payload->receiver = nullptr;
                    const auto count = heap.count; c.pending = 2; c.method = 123; c.il = 456;
                    assert(make_delegate(c, delegate_targets.size(), nullptr).status == 6);
                    assert(make_delegate(c, 0, forged).status == 6);
                    assert(make_delegate(c, 1, nullptr).status == 12);
                    assert(make_delegate(c, 1, outsider).status == 6);
                    assert(heap.count == count && c.pending == 2 && c.method == 123 && c.il == 456);
                    c.pending = 0; assert(prepare_delegate(c, roots[0], type).status == 0);
                    roots[0] = roots[1] = nullptr;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                [[gnu::noinline]] static void allocation_failure() {
                    using namespace {{artifact.EntryPoint[..^4]}}_closure;
                    Instance heap; heap.object_limit = 3; heap.collect_before_allocation = true; Context c(heap); LocationArgument argument;
                    (void){{method}}(c, &argument); assert(c.pending == 5 && c.method == {{flow.Entry.Token}}u && c.il == 7);
                    assert(c.top == nullptr && c.depth == 0 && c.borrows == nullptr && c.used == 0);
                    c.pending = 0; assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                    heap.object_limit = 4; assert({{method}}(c, &argument) == {{Oracle(0, false)}} && c.pending == 0);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                """);
        }
        source.Append("int main() {\n").AppendJoin('\n', calls)
            .Append("\nconst auto peak = arena(1048576, true); assert(peak > 0); arena(peak, true); arena(peak - 1, false); allocation_failure(); invalid_payloads();\n}\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("native-delegate-ir");
        string cpp = Path.Combine(directory, "delegates.cpp"), executable = Path.Combine(directory, "delegates");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", /* Private forged-pointer probes require diagnostic membership checks. */ "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
                "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stdout + run.Stderr); Assert.Empty(run.Stderr);
    }
}
