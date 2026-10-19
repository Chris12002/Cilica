using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cilica.Metadata.IL;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeObjectExecutionTests(ITestOutputHelper output)
{
    private sealed record Case(string Name, Func<int, int, int> Oracle, int Arity = 2);
    internal static ObjectIrProgram Translate(string name)
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ObjectProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var token = typeof(ObjectProbe).GetMethod(name)!.MetadataToken;
        var result = ObjectTranslator.Translate(resolver, image, (MethodDefinitionHandle)MetadataTokens.EntityHandle(token), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics)); return result.Program!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_objects_match_dotnet_and_preserve_roots_ownership_faults_and_teardown(bool optimized)
    {
        Case[] cases = [new(nameof(ObjectProbe.Run), ObjectProbe.Run), new(nameof(ObjectProbe.AcrossCalls), ObjectProbe.AcrossCalls),
            new(nameof(ObjectProbe.ReadNull), (a, _) => ObjectProbe.ReadNull(a), 1), new(nameof(ObjectProbe.Checked), ObjectProbe.Checked),
            new(nameof(ObjectProbe.ArgumentBeforeNull), ObjectProbe.ArgumentBeforeNull), new(nameof(ObjectProbe.Division), ObjectProbe.Division),
            new(nameof(ObjectProbe.Constructing), ObjectProbe.Constructing)];
        var programs = cases.Select(c => Translate(c.Name)).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        var source = new StringBuilder();
        for (int i = 0; i < artifacts.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i])).Append('\n');
        source.Append("#include <cassert>\n#include <cstring>\n#include <iostream>\n");
        // Observe published root contents at allocation, including references returned
        // from a now-popped callee and receiver/arguments in an allocating callee.
        foreach (int i in new[] { 0, 1, 6 })
        {
            string scope = Scope(artifacts[i]);
            source.Append("namespace ").Append(scope).Append(" {\n")
                .Append("static std::size_t polls = 0;\n")
                .Append("static bool reaches(Object* root, Object* target, int remaining = 16) noexcept {\n")
                .Append("  if (root == target) return true; if (root == nullptr || remaining == 0) return false;\n")
                .Append("  for (std::size_t j = 0; j < root->type->reference_count; ++j) { Object* child = nullptr;\n")
                .Append("    std::memcpy(&child, reinterpret_cast<const char*>(root) + root->type->references[j], sizeof(child));\n")
                .Append("    if (reaches(child, target, remaining - 1)) return true; } return false;\n}\n")
                .Append("static void inspect(Context& c) noexcept { ++polls; assert(c.top != nullptr && c.depth != 0 && valid_roots(c));\n")
                .Append("  for (Frame* f = c.top; f != nullptr; f = f->previous) { const auto begin = reinterpret_cast<std::uintptr_t>(c.execution), end = begin + c.used;\n")
                .Append("    assert(reinterpret_cast<std::uintptr_t>(f) >= begin && reinterpret_cast<std::uintptr_t>(f) + sizeof(Frame) <= end);\n")
                .Append("    if (f->count != 0) assert(reinterpret_cast<std::uintptr_t>(f->slots) >= begin && reinterpret_cast<std::uintptr_t>(f->slots) + f->count * sizeof(Object*) <= end); }\n")
                .Append("  for (Object* p = c.instance.head; p != nullptr; p = p->next) { bool found = false;\n")
                .Append("    for (Frame* f = c.top; f != nullptr; f = f->previous) for (std::size_t j = 0; j < f->count; ++j) found |= reaches(f->slots[j], p);\n")
                .Append("    assert(found); }\n}\n}\n");
        }
        string firstScope = Scope(artifacts[0]);
        var node = programs[0].Flow.Types.Types.Single(t => t.Source.Name.EndsWith("ProbeNode", StringComparison.Ordinal));
        string nodeType = "t_" + node.Source.ContentHash.ToLowerInvariant() + "_" + node.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        var instanceMethod = programs[0].Flow.Methods.Single(m => m.Source.Name.Contains("::Change", StringComparison.Ordinal));
        string change = CppEmitterSymbol(IrMethodId.From(instanceMethod.Source));
        source.Append("namespace ").Append(firstScope).Append(" { static int destroyed = 0; static void counted_destroy(Object* p) noexcept { ++destroyed; delete reinterpret_cast<")
            .Append(nodeType).Append("*>(p); } }\nint main() {\n");
        source.Append("  { using namespace ").Append(firstScope).Append("; Descriptor tracked = ").Append(nodeType).Append("_descriptor; tracked.destroy = counted_destroy;\n")
            .Append("    { Instance heap; Context c(heap); Object* a = allocate<").Append(nodeType).Append(">(c, tracked); Object* b = allocate<").Append(nodeType).Append(">(c, tracked);\n")
            .Append("      assert(a != nullptr && b != nullptr && a != b && a->synchronization == 0 && a->owner == &heap && heap.count == 2);\n")
            .Append("      assert(tracked.size == sizeof(").Append(nodeType).Append(") && tracked.alignment == alignof(").Append(nodeType).Append(") && tracked.reference_count == 1);\n")
            .Append("      Object* child = a; std::memcpy(&child, reinterpret_cast<const char*>(a) + tracked.references[0], sizeof(child)); assert(child == nullptr);\n")
            .Append("    } assert(destroyed == 2); }\n");
        source.Append("  { using namespace ").Append(firstScope).Append("; Instance heap; Context c(heap); Object* p = allocate<").Append(nodeType).Append(">(c, ").Append(nodeType).Append("_descriptor); assert(p != nullptr);\n");
        foreach (var field in node.Fields.Where(f => f.Storage is ObjectStorage.Int32))
            source.Append("    assert(reinterpret_cast<").Append(nodeType).Append("*>(p)->f_").Append(field.Id.Token.ToString("x8", CultureInfo.InvariantCulture)).Append(" == 0u);\n");
        source.Append("  }\n");
        // Cross-instance and arbitrary-pointer inputs fail before dereferencing; frames pop.
        source.Append("  { using namespace ").Append(firstScope).Append("; Instance a, b; Context ca(a), cb(b); Object* p = allocate<").Append(nodeType).Append(">(ca, ").Append(nodeType).Append("_descriptor);\n")
            .Append("    assert(").Append(change).Append("(cb, p, 1) == 0 && cb.pending == 6u && cb.top == nullptr && cb.depth == 0 && b.count == 0);\n")
            .Append("    Context invalid(b); assert(").Append(change).Append("(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}), 1) == 0 && invalid.pending == 6u && invalid.top == nullptr);\n")
            .Append("    Context poisoned(b); poisoned.observe = inspect; Object* slots[] = {p}; { Frame header; RootScope frame(poisoned, header, slots, 1); assert(!valid_roots(poisoned));\n")
            .Append("      assert(allocate<").Append(nodeType).Append(">(poisoned, ").Append(nodeType).Append("_descriptor) == nullptr && poisoned.pending == 6u && b.count == 0); } assert(poisoned.top == nullptr); }\n");
        for (int i = 0; i < artifacts.Length; i++)
        {
            var program = programs[i]; string scope = Scope(artifacts[i]), root = CppEmitterSymbol(program.Flow.Entry);
            source.Append("  { using namespace ").Append(scope).Append("; Instance heap; heap.limit = 0; Context c(heap);\n    (void)")
                .Append(root).Append("(c, 1").Append(cases[i].Arity == 2 ? ", 1" : "").Append(");\n")
                .Append("    assert(c.pending == ").Append(i == 4 ? "4u" : "5u").Append(" && c.top == nullptr && c.depth == 0 && c.used == 0 && heap.count == 0); }\n");
        }
        // Failure at the second allocation also unwinds an allocating constructor.
        foreach (int i in new[] { 0, 6 })
        {
            source.Append("  { using namespace ").Append(Scope(artifacts[i])).Append("; Instance heap; heap.object_limit = 1; Context c(heap); (void)")
                .Append(CppEmitterSymbol(programs[i].Flow.Entry)).Append("(c, 1, 2); assert(c.pending == 5u && heap.count == 1 && c.top == nullptr && c.depth == 0 && c.used == 0);");
            if (i == 6) source.Append(" assert(c.method == ").Append(typeof(ProbeWrapper).GetConstructor([typeof(int)])!.MetadataToken).Append("u);");
            source.Append(" }\n");
        }
        for (int pointer = 0; pointer < 4; pointer++)
        {
            string[] outputs = ["&value", "&method", "&il", "&image"]; outputs[pointer] = "nullptr";
            source.Append("  { const std::int32_t parameters[] = {1, 2}; std::int32_t value = 7; std::uint32_t method = 8, il = 9; const char* image = nullptr; assert(")
                .Append(artifacts[0].EntryPoint).Append('(').Append(string.Join(", ", outputs))
                .Append(", parameters, 2) == 3u && value == 7 && method == 8 && il == 9 && image == nullptr); }\n");
        }
        source.Append("  int operation; std::int32_t a, b; while (std::cin >> operation >> a >> b) {\n")
            .Append("    std::int32_t value = -123456789; std::uint32_t method = 0, il = 0; const char* image = nullptr; std::uint32_t status = 0;\n    switch (operation) {\n");
        for (int i = 0; i < artifacts.Length; i++)
        {
            string root = CppEmitterSymbol(programs[i].Flow.Entry), scope = Scope(artifacts[i]);
            source.Append("    case ").Append(i).Append(": { using namespace ").Append(scope).Append("; Instance heap; Context c(heap);");
            if (i is 0 or 1 or 6) source.Append(" polls = 0; c.observe = inspect;");
            source.Append(" const auto returned = ").Append(root).Append("(c, a").Append(cases[i].Arity == 2 ? ", b" : "").Append(");\n")
                .Append("      assert(c.top == nullptr && c.depth == 0 && c.used == 0); status = c.pending; method = c.method; il = c.il; image = c.image; if (status == 0) value = returned;\n");
            if (i is 0 or 1 or 6) source.Append("      assert(polls == ").Append(i == 0 ? "2" : "3").Append(");\n");
            source.Append("      const std::int32_t parameters[] = {a, b}; std::int32_t boundary = -123456789; std::uint32_t bm = 0, bi = 0; const char* bh = nullptr; assert(")
                .Append(artifacts[i].EntryPoint).Append("(&boundary, &bm, &bi, &bh, parameters, ").Append(cases[i].Arity).Append(") == status);\n")
                .Append("      assert(boundary == value && bm == method && bi == il && ((bh == nullptr && image == nullptr) || (bh != nullptr && image != nullptr && std::strcmp(bh, image) == 0)));\n")
                .Append("      break; }\n");
        }
        source.Append("    default: return 2; }\n    assert((status == 0 && method == 0 && il == 0 && image == nullptr) || (status != 0 && method != 0 && image != nullptr));\n")
            .Append("    std::cout << status << ' ' << value << ' ' << method << ' ' << il << ' ' << (image == nullptr ? \"-\" : image) << '\\n';\n  } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("objects-native");
        string cpp = Path.Combine(directory, "objects.cpp"), executable = Path.Combine(directory, "objects");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        string[] flags = ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", optimized ? "-O2" : "-O0",
            "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable];
        var version = await NativeProcess.Run(compiler, ["--version"], directory); Assert.Equal(0, version.ExitCode); output.WriteLine(version.Stdout);
        var build = await NativeProcess.Run(compiler, flags, directory); Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr);
        output.WriteLine(string.Join(" ", flags));
        int[] edges = [int.MinValue, int.MinValue + 1, -65536, -1, 0, 1, 65536, int.MaxValue - 1, int.MaxValue];
        var vectors = new List<(int A, int B)>(); foreach (int a in edges) foreach (int b in edges) vectors.Add((a, b));
        var random = new Random(0x3c2026); for (int i = 0; i < 128; i++) vectors.Add(((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1), (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1)));
        var input = new StringBuilder(); var expected = new List<string>();
        foreach (var (a, b) in vectors) for (int i = 0; i < cases.Length; i++)
        {
            int result = -123456789, status = 0;
            try { result = cases[i].Oracle(a, b); }
            catch (DivideByZeroException) { status = 1; }
            catch (OverflowException) { status = 2; }
            catch (NullReferenceException) { status = 4; }
            input.Append(CultureInfo.InvariantCulture, $"{i} {a} {b}\n");
            if (status == 0) expected.Add(FormattableString.Invariant($"0 {result} 0 0 -"));
            else
            {
                var origin = i switch
                {
                    2 => typeof(ProbeNode).GetMethod(nameof(ProbeNode.ReadConnected))!,
                    3 => typeof(ProbeNode).GetMethod(nameof(ProbeNode.CheckedChange))!,
                    _ => typeof(ObjectProbe).GetMethod(cases[i].Name)!,
                };
                var method = programs[i].Flow.Methods.Single(m => m.Source.Token == origin.MetadataToken);
                var fault = method.Blocks[0].Values.Last(v => i == 2 ? v.Expression is IrExpression.LoadField
                    : status == 4 ? v.Expression is IrExpression.Call { NullCheckReceiver: true } : v.Expression is IrExpression.Binary);
                expected.Add(FormattableString.Invariant($"{status} {result} {origin.MetadataToken} {fault.ILOffset} {method.Source.ContentHash.ToLowerInvariant()}"));
            }
        }
        var run = await NativeProcess.Run(executable, [], directory, input.ToString()); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n')); output.WriteLine($"{cases.Length} entries, {expected.Count} differential cases; seed=0x3c2026; .NET {Environment.Version}");
    }

    [Fact]
    public async Task Authored_raw_null_calls_identity_and_bounded_recursion_execute_correctly()
    {
        using var files = new ResolverFiles();
        var source = new StringBuilder(); var artifacts = new List<(CppArtifact Artifact, int Status, int Result)>();
        for (int mode = 0; mode < 8; mode++)
        {
            var oracle = new DynamicMethod("AuthoredObjectIdentity", typeof(int), Type.EmptyTypes);
            var code = oracle.GetILGenerator();
            if (mode == 6) { code.Emit(OpCodes.Ldc_I4, 42); code.Emit(OpCodes.Dup); code.Emit(OpCodes.Ceq); }
            else if (mode < 2) { code.Emit(OpCodes.Ldnull); code.Emit(OpCodes.Ldnull); code.Emit(mode == 0 ? OpCodes.Ceq : OpCodes.Cgt_Un); }
            else if (mode < 4) { code.Emit(OpCodes.Ldnull); code.Emit(mode == 2 ? OpCodes.Call : OpCodes.Callvirt, typeof(ProbeNode).GetMethod(nameof(ProbeNode.Constant))!); }
            else
            {
                code.Emit(OpCodes.Ldc_I4_0); code.Emit(OpCodes.Newobj, typeof(ProbeNode).GetConstructor([typeof(int)])!);
                if (mode == 4) code.Emit(OpCodes.Dup);
                else { code.Emit(OpCodes.Ldc_I4_0); code.Emit(OpCodes.Newobj, typeof(ProbeNode).GetConstructor([typeof(int)])!); }
                code.Emit(OpCodes.Ceq);
            }
            code.Emit(OpCodes.Ret); int expected = -123456789, status = 0;
            if (mode == 7) status = 8; // Never run unbounded recursion in the .NET oracle.
            else try { expected = oracle.CreateDelegate<Func<int>>()(); } catch (NullReferenceException) { status = 4; }
            var builder = new ResolutionAssemblyBuilder(new("NativeRawObjects" + mode, new Version(1, 0, 0, 0)));
            var objectBase = builder.Type("System", "Object");
            const MethodAttributes constructor = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var baseCtor = builder.Method(".ctor", [0x20, 0, 1], attributes: constructor, emit: il => il.OpCode(ILOpCode.Ret));
            builder.Type("Test", "Node", baseType: objectBase);
            var ctor = builder.Method(".ctor", [0x20, 0, 1], attributes: constructor, emit: il => { il.LoadArgument(0); il.Call(baseCtor); il.OpCode(ILOpCode.Ret); });
            var constant = builder.Method("Constant", [0x20, 0, 8], attributes: MethodAttributes.Public, emit: il => { il.LoadConstantI4(17); il.OpCode(ILOpCode.Ret); });
            builder.Type("Test", "Driver", baseType: objectBase);
            var root = builder.Method("Run", [0, 0, 8], emit: il =>
            {
                if (mode == 7) il.Call(MetadataTokens.MethodDefinitionHandle(4)); // Authored self-call.
                else if (mode == 6) { il.LoadConstantI4(42); il.OpCode(ILOpCode.Dup); il.OpCode(ILOpCode.Ceq); }
                else if (mode < 2) { il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Ldnull); il.OpCode(mode == 0 ? ILOpCode.Ceq : ILOpCode.Cgt_un); }
                else if (mode < 4) { il.OpCode(ILOpCode.Ldnull); il.OpCode(mode == 2 ? ILOpCode.Call : ILOpCode.Callvirt); il.Token(constant); }
                else
                {
                    il.OpCode(ILOpCode.Newobj); il.Token(ctor);
                    if (mode == 4) il.OpCode(ILOpCode.Dup); else { il.OpCode(ILOpCode.Newobj); il.Token(ctor); }
                    il.OpCode(ILOpCode.Ceq);
                }
                il.OpCode(ILOpCode.Ret);
            });
            using var resolver = files.Open(); var image = resolver.OpenInput(files.Write("raw" + mode + ".dll", builder.Build())).Value!;
            var translated = ObjectTranslator.Translate(resolver, image, root, image); Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
            var artifact = mode == 6 ? CppEmitter.EmitProgram(new(translated.Program!.Flow.Entry, translated.Program.Flow.Methods))
                : ObjectCppEmitter.EmitProgram(translated.Program!); artifacts.Add((artifact, status, expected)); source.Append(artifact.Text).Append('\n');
            if (mode != 6) source.Append(NativeObjectAdapters.Emit(translated.Program!, artifact));
        }
        source.Append("#include <cassert>\nint main() {\n");
        foreach (var (artifact, status, result) in artifacts)
        {
            if (status == 8)
                source.Append("  { using namespace ").Append(Scope(artifact)).Append("; Instance heap; Context c(heap); (void)")
                    .Append(CppEmitterSymbol(IrMethodId.From(artifact.Source))).Append("(c); assert(c.pending == 8u && c.top == nullptr && c.depth == 0 && c.used == 0); }\n");
            source.Append("  { std::int32_t value = -123456789; std::uint32_t method = 0, il = 0; const char* image = nullptr; assert(")
                .Append(artifact.FaultEntryPoint).Append("(&value, &method, &il, &image").Append(artifact.FaultEntryPoint.Contains("_objects_packed_", StringComparison.Ordinal) ? ", nullptr, 0" : "").Append(") == ").Append(status).Append("u && value == ").Append(result).Append("); }\n");
        }
        source.Append("}\n"); string directory = files.DirectoryPath("raw-native"), cpp = Path.Combine(directory, "raw.cpp"), executable = Path.Combine(directory, "raw");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false)); string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-O2", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stdout + build.Stderr); var run = await NativeProcess.Run(executable, [], directory);
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }

    private static string Scope(CppArtifact artifact) => artifact.EntryPoint[..^4] + "_closure";
    private static string CppEmitterSymbol(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
}
