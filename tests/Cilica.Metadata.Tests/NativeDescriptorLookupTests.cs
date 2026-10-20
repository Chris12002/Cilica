using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;
public sealed class NativeDescriptorLookupTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Checked_lookup_matches_exact_sets_and_rejects_colliding_unknown_addresses(bool optimized)
    {
        var translated = IntegerObjectTranslationTests.Translate(nameof(TypedBorrowProbe.Run), fixture: typeof(TypedBorrowProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program!));
        Assert.Contains("static const DescriptorLookup<", artifact.Text, StringComparison.Ordinal);
        var checks = new StringBuilder();
        foreach (Match match in Regex.Matches(artifact.Text, @"static constexpr std::array<const Descriptor\*, \d+> (\w+)_types ="))
        {
            string name = match.Groups[1].Value;
            checks.Append("check_set(").Append(name).Append(", ").Append(name).Append("_types);\n");
        }
        string source = artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static std::array<Descriptor,4096> descriptors{};
            static std::array<const Descriptor*,4096> large{};
            static std::array<const Descriptor*,8> collisions{};
            static Descriptor unknown{};
            template<std::size_t N> [[gnu::noinline]] static void check_set(bool(*accepts)(const Descriptor*) noexcept, const std::array<const Descriptor*,N>& types) {
                for(auto* type:types) assert(accepts(type));
                assert(!accepts(nullptr) && !accepts(&unknown) && !accepts(reinterpret_cast<const Descriptor*>(1)));
            }
            int main() {
                {{checks}}
                for(std::size_t i=0;i<large.size();++i) large[i]=&descriptors[i];
                static const DescriptorLookup<4096,8192> large_lookup(large);
                for(auto* type:large) assert(large_lookup.contains(type));
                assert(!large_lookup.contains(nullptr) && !large_lookup.contains(&unknown));
                std::size_t found=0; const Descriptor* missing=nullptr;
                const auto first=reinterpret_cast<std::uintptr_t>(&descriptors[0]);
                const auto bucket=((first>>3)^(first>>17))&15;
                for(auto* type:large) {
                    const auto a=reinterpret_cast<std::uintptr_t>(type);
                    if((((a>>3)^(a>>17))&15)!=bucket) continue;
                    if(found==8){missing=type;break;} collisions[found++]=type;
                }
                assert(found==8 && missing!=nullptr);
                static const DescriptorLookup<8,16> collision_lookup(collisions);
                for(auto* type:collisions) assert(collision_lookup.contains(type));
                assert(!collision_lookup.contains(missing) && !collision_lookup.contains(nullptr));
                assert(!collision_lookup.contains(reinterpret_cast<const Descriptor*>(1)));
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("descriptor-lookup");
        string cpp = Path.Combine(dir,"probe.cpp"), exe=Path.Combine(dir,"probe"); File.WriteAllText(cpp, source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20", optimized ? "-O2" : "-O0", "-fno-exceptions", "-fno-rtti", "-Wall", "-Wextra", "-Werror",
             "-Wframe-larger-than=2048", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", cpp, "-o", exe],dir);
        Assert.True(build.ExitCode==0, build.Stderr);
        var run=await NativeProcess.Run(exe,[],dir);Assert.True(run.ExitCode==0,run.Stderr);Assert.Empty(run.Stderr);
    }
}
