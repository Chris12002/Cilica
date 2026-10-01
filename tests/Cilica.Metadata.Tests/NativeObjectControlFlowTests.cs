using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeObjectControlFlowTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authored_object_branches_loops_and_stack_backedges_match_dotnet(bool optimized)
    {
        var swap = ObjectControlFlowTests.Translate(ObjectFlowFixtures.Create("swap")); Assert.True(swap.Success, string.Join("; ", swap.Diagnostics));
        var widen = ObjectControlFlowTests.Translate(ObjectFlowFixtures.Create("widen_loop")); Assert.True(widen.Success, string.Join("; ", widen.Diagnostics));
        var programs = new[] { ObjectControlFlowTests.Authored(nameof(ObjectFlowProbe.Chain)), ObjectControlFlowTests.Authored(nameof(ObjectFlowProbe.Choose)),
            ObjectControlFlowTests.Authored(nameof(ObjectFlowProbe.Identity)), ObjectControlFlowTests.Authored(nameof(ObjectFlowProbe.FaultLoop)), swap.Program!, widen.Program!, ObjectControlFlowTests.Authored(nameof(ObjectFlowProbe.ChooseObject)) };
        Func<int, int, int, int>[] references = [(a, b, _) => ObjectFlowProbe.Chain(a, b), (a, b, _) => ObjectFlowProbe.Choose(a, b),
            (a, b, _) => ObjectFlowProbe.Identity(a, b), (a, b, _) => ObjectFlowProbe.FaultLoop(a, b), RawSwap(), (a, _, _) => a == 0 ? 1 : 0, (a, b, _) => ObjectFlowProbe.ChooseObject(a, b)];
        var checkedMethod = programs[3].Flow.Methods.Single(m => m.Source.Token == typeof(ProbeNode).GetMethod(nameof(ProbeNode.CheckedChange))!.MetadataToken);
        int checkedOffset = checkedMethod.Blocks.SelectMany(b => b.Values).Single(v => v.Expression is IrExpression.Binary).ILOffset;
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        var source = new StringBuilder(); foreach (var artifact in artifacts) source.Append(artifact.Text).Append('\n');
        string scope = artifacts[4].EntryPoint[..^4] + "_closure";
        // Original references must remain published while loop temporaries/locals change.
        source.Append("#include <cassert>\n#include <iostream>\n#include <cstring>\nnamespace ").Append(scope).Append(" {\n")
            .Append("static Object* first = nullptr; static Object* second = nullptr;\n")
            .Append("static bool rooted(Context& c, Object* value) noexcept { for (Frame* f = c.top; f != nullptr; f = f->previous) for (std::size_t i = 0; i < f->count; ++i) if (f->slots[i] == value) return true; return false; }\n")
            .Append("static void observe(Context& c) noexcept { assert(c.top != nullptr && valid_roots(c));\n")
            .Append("  if (c.instance.count == 1) first = c.instance.head; if (c.instance.count == 2) second = c.instance.head;\n")
            .Append("  if (first != nullptr) assert(rooted(c, first)); if (second != nullptr) assert(rooted(c, second)); }\n}\n")
            .Append("int main() { int operation; std::int32_t a, b, d; while (std::cin >> operation >> a >> b >> d) {\n")
            .Append("  std::int32_t value = -123456789; std::uint32_t method = 0, il = 0; const char* image = nullptr; std::uint32_t status = 0;\n  switch(operation) {\n");
        for (int i = 0; i < programs.Length; i++)
        {
            var entry = programs[i].Flow.Methods.Single(m => IrMethodId.From(m.Source) == programs[i].Flow.Entry);
            string args = string.Join(", ", new[] { "a", "b", "d" }.Take(entry.Parameters.Length));
            string ownScope = artifacts[i].EntryPoint[..^4] + "_closure", root = "cilica_m_" + programs[i].Flow.Entry.ContentHash + "_" + programs[i].Flow.Entry.Token.ToString("x8", CultureInfo.InvariantCulture);
            source.Append("  case ").Append(i).Append(": { using namespace ").Append(ownScope).Append("; Instance heap; Context c(heap);");
            if (i == 4) source.Append(" first = nullptr; second = nullptr; c.observe = observe;");
            source.Append(" const auto returned = ").Append(root).Append("(c, ").Append(args).Append(");\n")
                .Append("    assert(c.top == nullptr && c.depth == 0); status = c.pending; if (status == 0) value = returned; method = c.method; il = c.il; image = c.image;\n");
            if (i == 3) source.Append("    if (status == 2u) assert(method == ").Append(checkedMethod.Source.Token).Append("u && il == ").Append(checkedOffset).Append("u);\n");
            source.Append("    std::int32_t boundary = -123456789; std::uint32_t bm = 0, bi = 0; const char* bh = nullptr; assert(")
                .Append(artifacts[i].EntryPoint).Append("(&boundary, &bm, &bi, &bh, ").Append(args).Append(") == status);\n")
                .Append("    assert(boundary == value && bm == method && bi == il && ((bh == nullptr && image == nullptr) || (bh != nullptr && image != nullptr && std::strcmp(bh, image) == 0))); break; }\n");
        }
        // Allocation failure from a loop exits through the same frame guard.
        source.Append("  default: return 2; } assert((status == 0 && method == 0 && il == 0 && image == nullptr) || (status != 0 && method != 0 && image != nullptr));\n")
            .Append("  std::cout << status << ' ' << value << '\\n'; }\n");
        var chainEntry = programs[0].Flow.Entry; string chainScope = artifacts[0].EntryPoint[..^4] + "_closure";
        source.Append("  { using namespace ").Append(chainScope).Append("; Instance heap; heap.object_limit = 2; Context c(heap); (void)cilica_m_")
            .Append(chainEntry.ContentHash).Append('_').Append(chainEntry.Token.ToString("x8", CultureInfo.InvariantCulture))
            .Append("(c, 4, 1); assert(c.pending == 5u && heap.count == 2 && c.top == nullptr && c.depth == 0); }\n  return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("object-flow-native"), cpp = Path.Combine(directory, "flow.cpp"), executable = Path.Combine(directory, "flow");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false)); string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string[] flags = ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable];
        var version = await NativeProcess.Run(compiler, ["--version"], directory); Assert.Equal(0, version.ExitCode); output.WriteLine(version.Stdout);
        var build = await NativeProcess.Run(compiler, flags, directory); Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr); output.WriteLine(string.Join(" ", flags));
        int[] edges = [int.MinValue, int.MinValue + 1, -65536, -1, 0, 1, 65536, int.MaxValue - 1, int.MaxValue];
        var vectors = new List<(int A, int B, int C)>(); foreach (int a in Enumerable.Range(0, 17).Concat([-7, -1])) foreach (int b in edges) vectors.Add((a, b, unchecked(b * 7 + 3)));
        var random = new Random(0x3bc2026); for (int i = 0; i < 128; i++) vectors.Add((random.Next(0, 17), Next(), Next()));
        int Next() => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        var input = new StringBuilder(); var expected = new List<string>();
        foreach (var (a, b, c) in vectors) for (int i = 0; i < programs.Length; i++)
        {
            int argument = i is 4 or 5 ? a & 15 : a;
            int value = -123456789, status = 0;
            try { value = references[i](argument, b, c); } catch (OverflowException) { status = 2; }
            input.Append(CultureInfo.InvariantCulture, $"{i} {argument} {b} {c}\n"); expected.Add(FormattableString.Invariant($"{status} {value}"));
        }
        var run = await NativeProcess.Run(executable, [], directory, input.ToString()); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n')); output.WriteLine($"7 entries; {expected.Count} native/.NET cases; seed=0x3bc2026; .NET {Environment.Version}");
    }

    private static Func<int, int, int, int> RawSwap()
    {
        var method = new DynamicMethod("AuthoredReferenceSwap", typeof(int), [typeof(int), typeof(int), typeof(int)]);
        var il = method.GetILGenerator(); var count = il.DeclareLocal(typeof(int)); var first = il.DeclareLocal(typeof(ProbeNode)); var second = il.DeclareLocal(typeof(ProbeNode));
        var ctor = typeof(ProbeNode).GetConstructor([typeof(int)])!; var field = typeof(ProbeNode).GetField(nameof(ProbeNode.Initial))!;
        var loop = il.DefineLabel(); var end = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stloc, count);
        il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Br, loop);
        il.MarkLabel(loop); il.Emit(OpCodes.Ldloc, count); il.Emit(OpCodes.Brfalse, end);
        il.Emit(OpCodes.Stloc, second); il.Emit(OpCodes.Stloc, first); il.Emit(OpCodes.Ldloc, second); il.Emit(OpCodes.Ldloc, first);
        il.Emit(OpCodes.Ldloc, count); il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldloc, count); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Sub); il.Emit(OpCodes.Stloc, count); il.Emit(OpCodes.Br, loop);
        il.MarkLabel(end); il.Emit(OpCodes.Stloc, second); il.Emit(OpCodes.Stloc, first);
        il.Emit(OpCodes.Ldloc, first); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ldc_I4_3); il.Emit(OpCodes.Mul);
        il.Emit(OpCodes.Ldloc, second); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Add); il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<int, int, int, int>>();
    }
}
