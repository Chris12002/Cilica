using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeInlineStringTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Single_units_need_one_allocation_and_preserve_storage_roots_and_retry(bool optimized)
    {
        var translated = ObjectStringTranslationTests.Authored("Length");
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var program = translated.Program!;
        var artifact = ObjectCppEmitter.EmitProgram(program);
        var type = program.Flow.Types.String!;
        string descriptor = "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture) + "_descriptor";
        string source = """
            #include <new>
            #include <cstddef>
            static unsigned objects = 0, buffers = 0;
            static bool fail_buffers = false, fail_object_once = false;
            void* operator new(std::size_t size, const std::nothrow_t&) noexcept {
                ++objects;
                if (fail_object_once) { fail_object_once = false; return nullptr; }
                return ::operator new(size);
            }
            void* operator new[](std::size_t size, const std::nothrow_t&) noexcept {
                ++buffers;
                return fail_buffers ? nullptr : ::operator new[](size);
            }
            """ + artifact.Text + $$"""

            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void check(Object* value, std::uint16_t expected) {
                assert(value && string_length(value) == 1 && string_code_unit(value, 0) == expected);
                auto* text = reinterpret_cast<StringObject*>(value);
                assert(text->units == &text->inline_unit && value->allocation_bytes == sizeof(StringObject));
            }
            int main() {
                // Exhaust the entire UTF-16 code-unit domain, including NUL and unpaired surrogates.
                for (std::uint32_t unit = 0; unit < 65536; ++unit) {
                    Instance heap; heap.string_pool_limit = 0; Context c(heap); objects = buffers = 0;
                    Object* first = string_from_character(c, {{descriptor}}, unit);
                    check(first, static_cast<std::uint16_t>(unit));
                    assert(objects == 1 && buffers == 0);
                    Object* second = string_from_character(c, {{descriptor}}, unit);
                    check(second, static_cast<std::uint16_t>(unit));
                    assert(first != second && objects == 2 && buffers == 0);
                    assert(heap.bytes == 2 * sizeof(StringObject) && heap.count == 2);
                }
                {
                    Instance heap; heap.string_pool_limit = 0; Context c(heap); Object* slots[5]{}; Frame frame;
                    RootScope roots(c, frame, slots, 5); heap.collect_before_allocation = true;
                    const std::uint16_t unit = 0xdfff; const std::uint32_t wide = 0x1dfff;
                    slots[0] = allocate_string(c, {{descriptor}}, &unit, 1, nullptr, 0);
                    slots[1] = allocate_string(c, {{descriptor}}, nullptr, 0, &unit, 1);
                    slots[2] = allocate_string(c, {{descriptor}}, nullptr, 0, nullptr, 0, &unit, 1);
                    slots[3] = allocate_string(c, {{descriptor}}, nullptr, 0, nullptr, 0, nullptr, 0, &unit, 1);
                    slots[4] = allocate_string(c, {{descriptor}}, nullptr, 1, nullptr, 0, nullptr, 0, nullptr, 0, &wide);
                    for (auto* value : slots) check(value, unit);
                    assert(collect(c).status == MarkStatus::complete && heap.count == 5);
                    for (auto& value : slots) value = nullptr;
                    assert(collect(c).status == MarkStatus::complete && heap.count == 0 && heap.bytes == 0);
                }
                {
                    Instance heap; heap.string_pool_limit = 0; Context c(heap); fail_buffers = true; objects = buffers = 0;
                    Object* value = string_from_character(c, {{descriptor}}, 0);
                    check(value, 0); assert(objects == 1 && buffers == 0);
                    const std::uint16_t units[] = {0, 0xffff};
                    // The longer-string buffer failure reclaims the unpublished header and retries safely.
                    assert(allocate_string(c, {{descriptor}}, units, 2, nullptr, 0) == nullptr);
                    assert(buffers >= 1 && heap.count == 0 && heap.bytes == 0);
                    fail_buffers = false;
                    value = allocate_string(c, {{descriptor}}, units, 2, nullptr, 0);
                    assert(value && string_length(value) == 2 && string_code_unit(value, 1) == 0xffff);
                    assert(value->allocation_bytes == sizeof(StringObject) + sizeof(units));
                }
                {
                    Instance heap; heap.string_pool_limit = 0; Context c(heap); fail_object_once = true;
                    Object* value = string_from_character(c, {{descriptor}}, 65);
                    check(value, 65); assert(heap.count == 1 && heap.bytes == sizeof(StringObject));
                    assert(c.pending == 0 && c.top == nullptr && c.depth == 0 && c.used == 0);
                }
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("inline-strings");
        string cpp = Path.Combine(directory, "probe.cpp"), exe = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source, new UTF8Encoding(false));
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
             "-DCILICA_VERIFY_INTERNAL_REFERENCES=1", "-DCILICA_VERIFY_ALLOCATION_ROOTS=1",
             "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var run = await NativeProcess.Run(exe, [], directory);
        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Empty(run.Stderr);
    }
}
