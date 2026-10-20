using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeStringPoolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slabs_preserve_live_identity_quotas_isolation_and_failure_recovery(bool optimized)
    {
        var program = ObjectStringTranslationTests.Authored("Length").Program!;
        var artifact = ObjectCppEmitter.EmitProgram(program);
        var type = program.Flow.Types.String!;
        string descriptor = "t_" + type.ContentHash + "_" + type.Token.ToString("x8", CultureInfo.InvariantCulture) + "_descriptor";
        string source = """
            #include <new>
            #include <cstddef>
            static std::size_t fail_size = 0;
            static bool fail_all = false, fail_buffers = false;
            void* operator new(std::size_t size, const std::nothrow_t&) noexcept {
                return fail_all || size == fail_size ? nullptr : ::operator new(size);
            }
            void* operator new[](std::size_t size, const std::nothrow_t&) noexcept {
                return fail_buffers ? nullptr : ::operator new[](size);
            }
            """ + artifact.Text + $$"""
            #include <cassert>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static Object* make(Context& c, std::uint32_t unit) {
                return string_from_character(c, {{descriptor}}, unit);
            }
            int main() {
                {
                    Instance heap, other; Context c(heap), d(other);
                    heap.string_pool_limit = sizeof(StringSlab);
                    Object* slots[300]{}; Frame frame; RootScope roots(c, frame, slots, 300);
                    for (unsigned i=0;i<300;++i) {
                        slots[i]=make(c,i+0xd800);
                        assert(slots[i] && string_code_unit(slots[i],0)==i+0xd800);
                        assert(reinterpret_cast<StringObject*>(slots[i])->pooled == (i<256));
                        for(unsigned j=0;j<i;++j) assert(slots[i]!=slots[j]);
                    }
                    assert(heap.string_pool_bytes == sizeof(StringSlab));
                    assert(heap.count==300 && heap.bytes==300*sizeof(StringObject));
                    assert(collect(c).status==MarkStatus::complete && heap.count==300);
                    auto* kept=slots[0]; auto* stale=slots[1];
                    for(unsigned i=1;i<300;++i) slots[i]=nullptr;
                    assert(collect(c).status==MarkStatus::complete && heap.count==1);
                    assert(!heap.owns(stale) && !traceable_reference(heap,stale));
                    assert(__asan_address_is_poisoned(stale));
                    for(unsigned i=0;i<2000;++i) {
                        slots[1]=make(c,i);
                        assert(slots[1]!=kept && string_code_unit(slots[1],0)==i);
                        assert(string_code_unit(kept,0)==0xd800);
                        assert(collect(c).status==MarkStatus::complete);
                        slots[1]=nullptr;
                    }
                    auto* foreign=make(d,65);
                    assert(foreign && !heap.owns(foreign) && !other.owns(kept));
                    assert(other.string_slabs!=heap.string_slabs);
                    assert(heap.string_pool_bytes==sizeof(StringSlab));
                    slots[1]=nullptr; assert(collect(c).status==MarkStatus::complete);
                    heap.object_limit=1;
                    assert(make(c,7)==nullptr && heap.count==1 && slots[0]==kept);
                    heap.object_limit=4096; heap.limit=sizeof(StringObject);
                    assert(make(c,7)==nullptr && heap.count==1);
                    heap.limit=1048576; slots[1]=make(c,7);
                    assert(slots[1] && c.pending==0);
                }
                {
                    // A slab allocation failure or a disabled reserve falls back,
                    // without imposing a new managed object-count limit.
                    Instance heap; Context c(heap); fail_size=sizeof(StringSlab);
                    Object* first=make(c,1);
                    assert(first && !reinterpret_cast<StringObject*>(first)->pooled && heap.string_pool_bytes==0);
                    fail_size=0; heap.string_pool_limit=0;
                    assert(make(c,2) && heap.string_pool_bytes==0);
                    fail_all=true; assert(make(c,3)==nullptr && heap.count==0);
                    fail_all=false; assert(make(c,4) && c.pending==0);
                }
                {
                    Instance heap; Context c(heap);
                    Object* slots[3]{}; Frame frame; RootScope roots(c,frame,slots,3);
                    heap.collect_before_allocation=true;
                    slots[0]=make(c,0xffff);
                    const std::uint16_t units[]={0,0xd800,0xffff};
                    fail_buffers=true;
                    assert(allocate_string(c,{{descriptor}},units,3,nullptr,0)==nullptr);
                    assert(heap.count==1 && string_code_unit(slots[0],0)==0xffff);
                    fail_buffers=false;
                    slots[1]=allocate_string(c,{{descriptor}},units,3,nullptr,0);
                    slots[2]=allocate_string(c,{{descriptor}},nullptr,0,nullptr,0);
                    assert(slots[1] && string_length(slots[1])==3 && string_code_unit(slots[1],1)==0xd800);
                    assert(slots[2] && string_length(slots[2])==0);
                    for(auto& slot:slots) slot=nullptr;
                    assert(collect(c).status==MarkStatus::complete && heap.count==0 && heap.bytes==0);
                    assert(heap.string_pool_bytes==sizeof(StringSlab));
                }
            }
            """;
        using var files = new ResolverFiles();
        string directory = files.DirectoryPath("string-pool");
        string cpp = Path.Combine(directory, "probe.cpp"), exe = Path.Combine(directory, "probe");
        File.WriteAllText(cpp, source);
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
