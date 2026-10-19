using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeObjectHierarchyTests(ITestOutputHelper output)
{
    internal static ObjectIrProgram Translate(string name = nameof(ObjectHierarchyProbe.Run))
    {
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var image = resolver.OpenInput(typeof(ObjectHierarchyProbe).Assembly.Location).Value!;
        var runtime = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var result = ObjectTranslator.Translate(resolver, image,
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ObjectHierarchyProbe).GetMethod(name)!.MetadataToken), runtime);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Program!;
    }

    private static string Method(IrMethodId id) => "cilica_m_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);
    private static string Type(ObjectTypeId id) => "t_" + id.ContentHash + "_" + id.Token.ToString("x8", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inherited_fields_constructor_order_roots_and_faults_match_dotnet(bool optimized)
    {
        string[] names = [nameof(ObjectHierarchyProbe.Run), nameof(ObjectHierarchyProbe.ArgumentBeforeNull), nameof(ObjectHierarchyProbe.BaseArgumentFault)];
        Func<int, int, int>[] oracles = [ObjectHierarchyProbe.Run, ObjectHierarchyProbe.ArgumentBeforeNull, ObjectHierarchyProbe.BaseArgumentFault];
        var programs = names.Select(n => Translate(n)).ToArray();
        var artifacts = programs.Select(ObjectCppEmitter.EmitProgram).ToArray();
        string Scope(int index) => artifacts[index].EntryPoint[..^4] + "_closure";
        var source = new StringBuilder();
        for (int i = 0; i < artifacts.Length; i++) source.Append(artifacts[i].Text).Append(NativeObjectAdapters.Emit(programs[i], artifacts[i]));
        var first = programs[0];
        ObjectTypeId Id(System.Type type) => ObjectTypeId.From(first.Flow.Types.Types.Single(t => t.Source.Token == type.MetadataToken).Source);
        string leaf = Type(Id(typeof(HierarchyLeaf))), middle = Type(Id(typeof(HierarchyMiddle))), parent = Type(Id(typeof(HierarchyBase))),
            node = Type(Id(typeof(ProbeNode))), root = Method(first.Flow.Entry);
        string change = Method(IrMethodId.From(first.Flow.Methods.Single(m => m.Source.Token == typeof(HierarchyBase).GetMethod(nameof(HierarchyBase.Change))!.MetadataToken).Source));
        int baseConstructor = typeof(HierarchyBase).GetConstructor([typeof(int), typeof(ProbeNode)])!.MetadataToken;
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            #include <cstring>
            #include <iostream>
            namespace {{Scope(0)}} {
            static bool reaches(Object* root, Object* target, unsigned remaining = 8) noexcept {
                if (root == target) return true;
                if (root == nullptr || remaining == 0) return false;
                for (std::size_t i = 0; i < root->type->reference_count; ++i) {
                    Object* child = *static_cast<Object**>(root->type->field_address(root, root->type->references[i]));
                    if (reaches(child, target, remaining - 1)) return true;
                }
                return false;
            }
            static bool saw_base = false;
            static void observe(Context& c) noexcept {
                assert(c.top != nullptr && valid_roots(c));
                for (Object* p = c.instance.head; p != nullptr; p = p->next) {
                    bool found = false;
                    for (Frame* f = c.top; f != nullptr; f = f->previous)
                        for (std::size_t i = 0; i < f->count; ++i) found |= reaches(f->slots[i], p);
                    assert(found);
                }
                // The seed, derived allocation and pre-base initializer are live
                // when the base body allocates its child. The dynamic type stays leaf.
                if (c.instance.count == 3) {
                    assert(c.depth >= 4);
                    Object* derived = c.instance.head->next;
                    assert(derived->type == &{{leaf}}_descriptor && reaches(derived, c.instance.head));
                    saw_base = true;
                }
            }
            static unsigned destroyed = 0;
            static void counted_destroy(Object* p) noexcept { ++destroyed; delete reinterpret_cast<{{leaf}}*>(p); }
            }
            [[gnu::noinline]] static void ordered_faults() noexcept {
                using namespace {{Scope(0)}};
                // The pre-base initializer allocates before the checked base argument.
                Instance early; early.object_limit = 2; Context initializer_fault(early);
                (void){{root}}(initializer_fault, 2147483647, 7); assert(initializer_fault.pending == 5 && early.count == 2);
                Instance later; later.object_limit = 3; Context argument_fault(later);
                (void){{root}}(argument_fault, 2147483647, 7); assert(argument_fault.pending == 2 && later.count == 3);
                assert(argument_fault.used == 0 && argument_fault.top == nullptr && argument_fault.depth == 0);
                argument_fault.pending = 0; argument_fault.image = nullptr; argument_fault.method = 0; argument_fault.il = 0; later.object_limit = 4096;
                assert({{root}}(argument_fault, 3, 7) == {{ObjectHierarchyProbe.Run(3, 7)}} && argument_fault.pending == 0 && argument_fault.used == 0);
            }
            int main() {
                ordered_faults();
              { using namespace {{Scope(0)}};
                assert({{leaf}}_descriptor.base == &{{middle}}_descriptor && {{middle}}_descriptor.base == &{{parent}}_descriptor);
                assert({{leaf}}_descriptor.reference_count == 5 && {{leaf}}_descriptor.field_count == 8);
                Descriptor tracked = {{leaf}}_descriptor; tracked.destroy = counted_destroy;
                { Instance heap; Context c(heap); Object* p = allocate<{{leaf}}>(c, tracked); assert(p != nullptr);
                  for (std::size_t i = 0; i < tracked.reference_count; ++i)
                    assert(*static_cast<Object**>(tracked.field_address(p, tracked.references[i])) == nullptr);
                  // Private base integer and leaf readonly integer are separate zeroed slots.
                  assert(*static_cast<std::uint32_t*>(tracked.field_address(p, tracked.fields[0])) == 0);
                  assert(*static_cast<std::uint32_t*>(tracked.field_address(p, tracked.fields[7])) == 0);
                } assert(destroyed == 1);
                Instance heap; Context c(heap); c.observe = observe; saw_base = false;
                assert({{root}}(c, 3, 7) == {{ObjectHierarchyProbe.Run(3, 7)}} && c.pending == 0 && saw_base);
                assert(c.used == 0 && c.top == nullptr && c.depth == 0 && heap.count == 6);
                Instance second; Context exact(second, c.peak); exact.observe = observe;
                assert({{root}}(exact, 3, 7) == {{ObjectHierarchyProbe.Run(3, 7)}} && exact.pending == 0 && exact.used == 0);
                Instance third; Context short_by_one(third, c.peak - 1); (void){{root}}(short_by_one, 3, 7);
                assert(short_by_one.pending == 9 && short_by_one.used == 0 && short_by_one.top == nullptr && short_by_one.depth == 0);
                for (std::size_t count = 0; count < 6; ++count) {
                    Instance limited; limited.object_limit = count; Context failed(limited); (void){{root}}(failed, 3, 7);
                    assert(failed.pending == 5 && limited.count == count && failed.used == 0 && failed.top == nullptr && failed.depth == 0);
                    if (count == 3) assert(failed.method == {{baseConstructor}}u);
                }
                Instance owner; Context owned(owner); Object* p = allocate<{{leaf}}>(owned, {{leaf}}_descriptor);
                assert({{change}}(owned, p, 7) == 7 && owned.pending == 0 && owned.used == 0);
                Instance foreign; Context wrong(foreign); (void){{change}}(wrong, p, 7);
                assert(wrong.pending == 6 && wrong.used == 0 && wrong.top == nullptr);
                Context invalid(owner); (void){{change}}(invalid, reinterpret_cast<Object*>(std::uintptr_t{1}), 7); assert(invalid.pending == 6);
                Object* unrelated = allocate<{{node}}>(owned, {{node}}_descriptor);
                Context sibling(owner); (void){{change}}(sibling, unrelated, 7); assert(sibling.pending == 6 && sibling.used == 0);
                Context direct_null(owner); (void){{change}}(direct_null, nullptr, 7); assert(direct_null.pending == 4 && direct_null.used == 0);
              }
              int operation; std::int32_t a, b;
              while (std::cin >> operation >> a >> b) {
                std::int32_t value = -123456789; std::uint32_t status = 0, method = 0, il = 0; const char* image = nullptr;
                switch (operation) {
            """);
        for (int i = 0; i < programs.Length; i++)
        {
            source.Append(CultureInfo.InvariantCulture, $$"""
                  case {{i}}: { using namespace {{Scope(i)}}; Instance heap; Context c(heap);
                    const auto returned = {{Method(programs[i].Flow.Entry)}}(c, a, b);
                    assert(c.top == nullptr && c.depth == 0 && c.used == 0);
                    status = c.pending; method = c.method; il = c.il; image = c.image; if (status == 0) value = returned;
                    const std::int32_t parameters[] = {a, b}; std::int32_t boundary = -123456789;
                    std::uint32_t bm = 0, bi = 0; const char* bh = nullptr;
                    assert({{artifacts[i].EntryPoint}}(&boundary, &bm, &bi, &bh, parameters, 2) == status);
                    assert(boundary == value && bm == method && bi == il && ((bh == nullptr && image == nullptr)
                        || (bh != nullptr && image != nullptr && std::strcmp(bh, image) == 0)));
                    break; }
                """);
        }
        source.Append("default: return 2; } std::cout << status << ' ' << value << ' ' << method << ' ' << il << '\\n'; } return std::cin.eof() ? 0 : 3; }\n");
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("hierarchy-native"), cpp = Path.Combine(directory, "hierarchy.cpp"), executable = Path.Combine(directory, "hierarchy");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", /* Private forged-pointer probes require diagnostic membership checks. */ "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        int[] edges = [int.MinValue, int.MinValue + 1, -65536, -1, 0, 1, 65536, int.MaxValue - 1, int.MaxValue];
        var vectors = new List<(int A, int B)>(); foreach (int a in edges) foreach (int b in edges) vectors.Add((a, b));
        var random = new Random(0x3db2026); for (int i = 0; i < 64; i++) vectors.Add(((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1), (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1)));
        var input = new StringBuilder(); var expected = new List<string>();
        foreach (var (a, b) in vectors) for (int i = 0; i < programs.Length; i++)
        {
            int value = -123456789, status = 0, token = 0, offset = 0;
            try { value = oracles[i](a, b); }
            catch (DivideByZeroException) { status = 1; }
            catch (OverflowException) { status = 2; }
            catch (NullReferenceException) { status = 4; }
            if (status != 0)
            {
                bool argumentDivision = i == 2 && (status == 1 || a == int.MinValue && b == -1);
                token = i == 1 || argumentDivision ? typeof(ObjectHierarchyProbe).GetMethod(names[i])!.MetadataToken
                    : typeof(HierarchyMiddle).GetConstructor([typeof(int), typeof(ProbeNode)])!.MetadataToken;
                var method = programs[i].Flow.Methods.Single(m => m.Source.Token == token);
                offset = method.Blocks.SelectMany(block => block.Statements).Single(s => status == 4
                    ? s is IrStatement.Define { Value.Expression: IrExpression.Call { NullCheckReceiver: true } }
                    : s is IrStatement.Define { Value.Expression: IrExpression.Binary { Operation: Int32Operation.AddCheckedSigned or Int32Operation.DivideSigned } }) switch
                    { IrStatement.Define d => d.Value.ILOffset, _ => throw new InvalidOperationException() };
            }
            input.Append(CultureInfo.InvariantCulture, $"{i} {a} {b}\n");
            expected.Add(FormattableString.Invariant($"{status} {value} {token} {offset}"));
        }
        var run = await NativeProcess.Run(executable, [], directory, input.ToString());
        Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
        Assert.Equal(expected.Concat([""]), run.Stdout.Split('\n'));
        output.WriteLine($"{expected.Count} native/.NET result and exact fault-origin cases at {(optimized ? "O2" : "O0")}; sanitizer and 2 KiB stack gates enabled.");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Cross_image_hidden_fields_with_equal_tokens_keep_distinct_native_storage(bool optimized, bool wide)
    {
        var program = ObjectHierarchyTranslationTests.CrossImage();
        if (wide)
        {
            var parent = program.Flow.Types.Types.Single(t => t.Fields.Length == 1 && t.BaseType == program.Flow.Types.ObjectBase);
            var expanded = parent with { Fields = [.. parent.Fields, .. Enumerable.Range(2, 4094).Select(token =>
                new ObjectField(new(ObjectTypeId.From(parent.Source), 0x04000000 | token), "Extra", System.Reflection.FieldAttributes.Public, new ObjectStorage.Int32()))] };
            program = program with { Flow = program.Flow with { Types = program.Flow.Types with { Types = program.Flow.Types.Types.Replace(parent, expanded) } } };
        }
        var artifact = ObjectCppEmitter.EmitProgram(program);
        var fields = program.Flow.Types.Types.SelectMany(t => t.Fields).ToArray();
        Assert.Equal(wide ? 4096 : 2, fields.Length);
        var hidden = fields.Where(f => f.Id.Token == 0x04000001).ToArray();
        Assert.Equal(2, hidden.Length); Assert.NotEqual(hidden[0].Id.Owner.ContentHash, hidden[1].Id.Owner.ContentHash);
        string scope = artifact.EntryPoint[..^4] + "_closure";
        string source = artifact.Text + NativeObjectAdapters.Emit(program, artifact) + $$"""
            #include <cassert>
            int main() {
                using namespace {{scope}}; Instance heap; Context c(heap);
                assert({{Method(program.Flow.Entry)}}(c) == 37 && c.pending == 0 && c.used == 0 && heap.count == 1);
                std::int32_t value = 0; std::uint32_t method = 0, il = 0; const char* image = nullptr;
                assert({{artifact.EntryPoint}}(&value, &method, &il, &image, nullptr, 0) == 0 && value == 37);
            }
            """;
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("cross-image-native"), cpp = Path.Combine(directory, "fields.cpp"), executable = Path.Combine(directory, "fields");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(executable, [], directory); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }
}
