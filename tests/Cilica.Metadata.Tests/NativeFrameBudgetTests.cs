using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeFrameBudgetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reservation_alignment_roots_argument_packets_and_fault_order_are_bounded(bool optimized)
    {
        var programs = new[] { NativeObjectExecutionTests.Translate(nameof(ObjectProbe.AcrossCalls)),
            NativeObjectExecutionTests.Translate(nameof(ObjectProbe.ArgumentBeforeNull)), ManyArguments() };
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        var source = new StringBuilder();
        for (int i = 0; i < programs.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        string Scope(int i) => artifacts[i].EntryPoint[..^4] + "_closure";
        string Root(int i) => "cilica_m_" + programs[i].Flow.Entry.ContentHash + "_" + programs[i].Flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <limits>
            namespace {{Scope(0)}} {
            static void inspect(Context& c) noexcept {
                assert(valid_roots(c));
                for (Frame* f = c.top; f != nullptr; f = f->previous) {
                    const auto first = reinterpret_cast<std::uintptr_t>(c.execution), last = first + c.used;
                    assert(reinterpret_cast<std::uintptr_t>(f) >= first && reinterpret_cast<std::uintptr_t>(f) + sizeof(Frame) <= last);
                    if (f->count != 0) assert(reinterpret_cast<std::uintptr_t>(f->slots) >= first && reinterpret_cast<std::uintptr_t>(f->slots) + sizeof(Object*) * f->count <= last);
                }
            }
            }
            int main() {
              { using namespace {{Scope(0)}}; Instance heap; Context c(heap); c.observe = inspect;
                assert({{Root(0)}}(c, 3, 7) == {{ObjectProbe.AcrossCalls(3, 7)}} && c.pending == 0 && c.used == 0);
                const auto required = c.peak;
                Instance second; Context exact(second, required); exact.observe = inspect;
                assert({{Root(0)}}(exact, 3, 7) == {{ObjectProbe.AcrossCalls(3, 7)}} && exact.pending == 0 && exact.used == 0);
                Instance third; Context short_by_one(third, required - 1); short_by_one.observe = inspect;
                (void){{Root(0)}}(short_by_one, 3, 7);
                assert(short_by_one.pending == 9u && short_by_one.top == nullptr && short_by_one.depth == 0 && short_by_one.used == 0);
                assert(third.count != 0); // Exhaustion while references and caller frames are live.
                constexpr std::size_t size = sizeof({{Root(0)}}_storage), alignment = alignof({{Root(0)}}_storage);
                Context aligned(heap, size + alignment);
                { Storage<std::byte> prefix(aligned); assert(prefix.value != nullptr && aligned.used == 1);
                  { Storage<{{Root(0)}}_storage> frame(aligned); assert(frame.value != nullptr);
                    assert(reinterpret_cast<std::uintptr_t>(frame.value) % alignment == 0 && aligned.used == size + alignment); }
                  assert(aligned.used == 1); }
                assert(aligned.used == 0);
                Context insufficient(heap, size + alignment - 1);
                { Storage<std::byte> prefix(insufficient); auto* before = insufficient.execution;
                  Storage<{{Root(0)}}_storage> failed(insufficient);
                  assert(failed.value == nullptr && insufficient.used == 1 && insufficient.depth == 0 && insufficient.top == nullptr && insufficient.execution == before); }
                assert(insufficient.used == 0);
                Context overflow(heap, 1); overflow.used = std::numeric_limits<std::size_t>::max();
                { Storage<{{Root(0)}}_storage> failed(overflow); assert(failed.value == nullptr && overflow.execution == nullptr && overflow.used == std::numeric_limits<std::size_t>::max()); }
              }
              { using namespace {{Scope(1)}}; Instance heap;
                Context null_receiver(heap, sizeof({{Root(1)}}_storage)); (void){{Root(1)}}(null_receiver, 1, 2);
                assert(null_receiver.pending == 4u && null_receiver.used == 0 && null_receiver.depth == 0 && null_receiver.top == nullptr);
                Context argument_fault(heap, sizeof({{Root(1)}}_storage)); (void){{Root(1)}}(argument_fault, 2147483647, 1);
                assert(argument_fault.pending == 2u && argument_fault.used == 0 && argument_fault.depth == 0 && argument_fault.top == nullptr);
              }
              { using namespace {{Scope(2)}}; Instance heap; Context c(heap);
                assert({{Root(2)}}(c, 7) == 1037 && c.pending == 0 && c.used == 0 && c.top == nullptr);
                Context exact(heap, c.peak); assert({{Root(2)}}(exact, 7) == 1037 && exact.pending == 0 && exact.used == 0);
                Context insufficient(heap, c.peak - 1); (void){{Root(2)}}(insufficient, 7);
                assert(insufficient.pending == 9u && insufficient.used == 0 && insufficient.depth == 0 && insufficient.top == nullptr);
              }
            }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("frame-budgets"), cpp = Path.Combine(directory, "budget.cpp"), executable = Path.Combine(directory, "budget");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
            "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }

    private static ObjectIrProgram ManyArguments()
    {
        const int count = 1024;
        var builder = new ResolutionAssemblyBuilder(new("WideCallFrame", new Version(1, 0, 0, 0)));
        var objectBase = builder.Type("System", "Object"); builder.Type("Test", "Driver", baseType: objectBase);
        var signature = new BlobBuilder(); signature.WriteByte(0); signature.WriteCompressedInteger(count); signature.WriteByte(8);
        for (int i = 0; i < count; i++) signature.WriteByte(8);
        var callee = builder.Method("Many", signature.ToArray(), emit: il =>
        { il.LoadArgument(0); il.LoadArgument(count - 1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret); });
        var root = builder.Method("Run", [0, 1, 8, 8], maxStack: count + 1, emit: il =>
        {
            for (int i = 0; i < count; i++) { il.LoadArgument(0); il.LoadConstantI4(i); il.OpCode(ILOpCode.Add); }
            il.Call(callee); il.OpCode(ILOpCode.Ret);
        });
        var result = ObjectControlFlowTests.Translate(new(builder, root)); Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Program!;
    }
}
