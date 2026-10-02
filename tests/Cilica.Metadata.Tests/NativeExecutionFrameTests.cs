using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeExecutionFrameTests(ITestOutputHelper output)
{
    private const int Width = 1024;
    private const int LocalCount = 2048;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wide_finite_and_mutual_recursion_obey_memory_depth_and_native_stack_bounds(bool optimized)
    {
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("execution-frames"), cpp = Path.Combine(directory, "frames.cpp"), executable = Path.Combine(directory, "frames");
        var source = new StringBuilder(); var checks = new StringBuilder();
        foreach (bool mutual in new[] { false, true })
        {
            var fixture = Recursion(mutual);
            var translated = ObjectControlFlowTests.Translate(fixture);
            Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var program = translated.Program!; var artifact = ObjectCppEmitter.EmitProgram(program);
            source.Append(artifact.Text).Append(NativeObjectAdapters.Emit(program, artifact));
            string scope = artifact.EntryPoint[..^4] + "_closure", root = Symbol(program.Flow.Entry);
            int expected = Oracle(mutual)(40, 3);
            checks.Append(CultureInfo.InvariantCulture, $$"""
                { using namespace {{scope}};
                  Instance heap; Context generous(heap, 8 * 1048576);
                  assert({{root}}(generous, 40, 3) == {{expected}} && generous.pending == 0);
                  assert(generous.used == 0 && generous.depth == 0 && generous.top == nullptr);
                  const auto required = generous.peak;
                  assert(required > 262144);
                  Context exact(heap, required); assert({{root}}(exact, 40, 3) == {{expected}} && exact.pending == 0 && exact.peak == required);
                  assert(exact.used == 0 && exact.depth == 0 && exact.top == nullptr);
                  Context short_by_one(heap, required - 1); (void){{root}}(short_by_one, 40, 3);
                  assert(short_by_one.pending == 9u && short_by_one.used == 0 && short_by_one.depth == 0 && short_by_one.top == nullptr);
                  assert(short_by_one.method != 0 && short_by_one.image != nullptr);
                  short_by_one.pending = 0; short_by_one.method = 0; short_by_one.il = 0; short_by_one.image = nullptr;
                  assert({{root}}(short_by_one, 0, 3) == {{Width + 3}} && short_by_one.pending == 0 && short_by_one.used == 0);
                  Context empty(heap, 0); (void){{root}}(empty, 0, 3);
                  assert(empty.pending == 9u && empty.used == 0 && empty.peak == 0 && empty.top == nullptr && empty.depth == 0);
                  Context one(heap, sizeof({{root}}_storage));
                  assert({{root}}(one, 0, 3) == {{Width + 3}} && one.pending == 0 && one.used == 0);
                  (void){{root}}(one, 1, 2147483647);
                  assert(one.pending == 2u && one.used == 0 && one.depth == 0 && one.top == nullptr);
                  Context depth(heap, 8 * 1048576);
                  assert({{root}}(depth, 63, 0) == {{64 * Width}} && depth.pending == 0);
                  (void){{root}}(depth, 64, 0);
                  assert(depth.pending == 8u && depth.used == 0 && depth.depth == 0 && depth.top == nullptr);
                  std::int32_t value = 123; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                  assert({{artifact.EntryPoint}}(&value, &method, &il, &image, 63, 3) == 9u && value == 123 && method != 0 && image != nullptr);
                }
                """);
        }
        string generated = Path.Combine(directory, "generated.cpp");
        File.WriteAllText(generated, source.ToString(), new UTF8Encoding(false));
        source.Append("#include <cassert>\nint main() {\n").Append(checks).Append("}\n");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        // A targeted local target check, including instrumentation overhead. .su
        // evidence is inspected, so a silently ignored warning is not sufficient.
        string[] flags = ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
            "-fstack-usage", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable];
        var build = await NativeProcess.Run(compiler, flags, directory, timeout: TimeSpan.FromSeconds(60));
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        // ASan's fake-stack/realignment paths can be labelled dynamic even for
        // fixed source storage. Measure the non-instrumented target separately;
        // retain the same frame warning for the instrumented execution above.
        string measuredObject = Path.Combine(directory, "measured.o");
        var measured = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", optimized ? "-O2" : "-O0",
            "-fstack-usage", "-Werror", "-Wframe-larger-than=2048", "-c", generated, "-o", measuredObject], directory);
        Assert.True(measured.ExitCode == 0, measured.Stderr);
        var usageFiles = Directory.GetFiles(directory, "measured.su"); Assert.NotEmpty(usageFiles);
        var rows = usageFiles.SelectMany(File.ReadAllLines).Select(line => line.Split('\t')).Where(parts => parts.Length >= 3).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => { Assert.Equal("static", row[^1]); Assert.InRange(int.Parse(row[^2], CultureInfo.InvariantCulture), 0, 2048); });
        output.WriteLine($"{(optimized ? "O2" : "O0")}: {rows.Length} measured native functions, maximum {rows.Max(row => int.Parse(row[^2], CultureInfo.InvariantCulture))} stack bytes; {Width} checked additions per invocation.");
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }

    private static ObjectFlowFixtures.Fixture Recursion(bool mutual)
    {
        var builder = new ResolutionAssemblyBuilder(new("ExecutionFrames" + mutual, new Version(1, 0, 0, 0)));
        var objectBase = builder.Type("System", "Object"); builder.Type("Test", "Driver", baseType: objectBase);
        var locals = new BlobBuilder(); locals.WriteByte(7); locals.WriteCompressedInteger(LocalCount);
        for (int i = 0; i < LocalCount; i++) locals.WriteByte(8);
        var root = builder.Method("Run", [0, 2, 8, 8, 8], locals: locals.ToArray(), emit: il => Emit(il, mutual ? 2 : 1));
        if (mutual) builder.Method("Other", [0, 2, 8, 8, 8], locals: locals.ToArray(), emit: il => Emit(il, 1));
        return new(builder, root);

        static void Emit(InstructionEncoder il, int target)
        {
            var done = il.DefineLabel(); il.LoadArgument(1);
            for (int i = 0; i < Width; i++) { il.LoadConstantI4(1); il.OpCode(ILOpCode.Add_ovf); }
            il.LoadArgument(0); il.Branch(ILOpCode.Brfalse, done);
            il.LoadArgument(0); il.LoadConstantI4(1); il.OpCode(ILOpCode.Sub); il.LoadArgument(1);
            il.Call(MetadataTokens.MethodDefinitionHandle(target)); il.OpCode(ILOpCode.Add_ovf);
            il.MarkLabel(done); il.OpCode(ILOpCode.Ret);
        }
    }

    private static Func<int, int, int> Oracle(bool mutual)
    {
        var first = new DynamicMethod("FrameRecursion", typeof(int), [typeof(int), typeof(int)]);
        var second = mutual ? new DynamicMethod("FrameMutual", typeof(int), [typeof(int), typeof(int)]) : first;
        Emit(first, second); if (mutual) Emit(second, first);
        return first.CreateDelegate<Func<int, int, int>>();
        static void Emit(DynamicMethod method, DynamicMethod target)
        {
            var il = method.GetILGenerator();
            for (int i = 0; i < LocalCount; i++) il.DeclareLocal(typeof(int));
            var done = il.DefineLabel(); il.Emit(OpCodes.Ldarg_1);
            for (int i = 0; i < Width; i++) { il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add_Ovf); }
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Brfalse, done);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Sub); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, target); il.Emit(OpCodes.Add_Ovf); il.MarkLabel(done); il.Emit(OpCodes.Ret);
        }
    }

    private static string Symbol(IrMethodId id) => "cilica_m_" + id.ContentHash.ToLowerInvariant() + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
}
