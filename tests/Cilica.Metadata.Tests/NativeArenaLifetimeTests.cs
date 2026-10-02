using System;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeArenaLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Asan_detects_released_slots_and_keeps_live_neighbors_and_reused_slots_accessible(bool optimized)
    {
        var program = NativeObjectExecutionTests.Translate(nameof(ObjectProbe.ArgumentBeforeNull));
        var artifact = ObjectCppEmitter.EmitProgram(program);
        var source = new StringBuilder(artifact.Text);
        source.Append(CultureInfo.InvariantCulture, $$"""
            #include <cassert>
            int main(int argc, char**) {
              using namespace {{artifact.EntryPoint[..^4]}}_closure;
              Instance heap; Context c(heap, 128);
              { Storage<std::byte> prefix(c); assert(prefix.value != nullptr); *prefix.value = std::byte{42};
                assert(!__asan_address_is_poisoned(prefix.value));
                volatile std::byte* expired;
                { Storage<std::byte> nested(c); assert(nested.value != nullptr); *nested.value = std::byte{17}; expired = nested.value;
                  assert(!__asan_address_is_poisoned(nested.value));
                  assert(__asan_address_is_poisoned(c.execution + c.used)); }
                assert(__asan_address_is_poisoned(expired));
                assert(!__asan_address_is_poisoned(prefix.value) && *prefix.value == std::byte{42});
                if (argc > 1) return static_cast<int>(*expired); // Required ASan failure in a test-owned subprocess.
                { Storage<Argument> reused(c); assert(reused.value != nullptr);
                  assert(reinterpret_cast<volatile void*>(reused.value) == reinterpret_cast<volatile void*>(expired));
                  assert(!__asan_address_is_poisoned(reused.value));
                  assert(reused.value->bits == 0 && reused.value->reference == nullptr);
                  reused.value->bits = 71; }
                assert(__asan_address_is_poisoned(expired));
                assert(*prefix.value == std::byte{42});
              }
              assert(c.used == 0 && __asan_address_is_poisoned(c.execution));
              { Storage<Argument> next(c); assert(next.value != nullptr && next.value->bits == 0); }
              assert(c.used == 0 && __asan_address_is_poisoned(c.execution));
            }
            """);
        using var files = new ResolverFiles(); string directory = files.DirectoryPath("arena-lifetime");
        string cpp = Path.Combine(directory, "lifetime.cpp"), executable = Path.Combine(directory, "lifetime");
        File.WriteAllText(cpp, source.ToString(), new UTF8Encoding(false));
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var build = await NativeProcess.Run(compiler, ["-std=c++20", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
            optimized ? "-O2" : "-O0", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", executable], directory);
        Assert.True(build.ExitCode == 0, build.Stderr);
        var valid = await NativeProcess.Run(executable, [], directory); Assert.True(valid.ExitCode == 0, valid.Stderr); Assert.Empty(valid.Stderr);
        var stale = await NativeProcess.Run(executable, ["stale"], directory);
        Assert.NotEqual(0, stale.ExitCode);
        Assert.Contains("AddressSanitizer: use-after-poison", stale.Stderr, StringComparison.Ordinal);
    }
}
