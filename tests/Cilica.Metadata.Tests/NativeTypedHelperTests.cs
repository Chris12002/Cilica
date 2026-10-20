using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;
public sealed class NativeTypedHelperTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Inlining_stays_within_acyclic_depth_and_expansion_bounds(bool optimized)
    {
        var translated = IntegerObjectTranslationTests.Translate(nameof(TypedInliningProbe.Run), fixture: typeof(TypedInliningProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program!));
        string Kernel(string method) => "cilica_m_" + artifact.Source.ContentHash + "_"
            + typeof(TypedInliningProbe).GetMethod(method)!.MetadataToken.ToString("x8", CultureInfo.InvariantCulture) + "_kernel";
        Assert.Contains(Kernel(nameof(TypedInliningProbe.Depth8)), artifact.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Kernel(nameof(TypedInliningProbe.Depth9)), artifact.Text, StringComparison.Ordinal);
        Assert.Contains(Kernel(nameof(TypedInliningProbe.Fan5)), artifact.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Kernel(nameof(TypedInliningProbe.Fan6)), artifact.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Kernel(nameof(TypedInliningProbe.Recursive)), artifact.Text, StringComparison.Ordinal);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("BoundedInlining", translated.Program!, TypedInliningProbe.Run)],
            nativeFlags: ["-DCILICA_TYPED_INTERNALS=1", "-Wframe-larger-than=16384"]);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(true, 0)] [InlineData(true, 1)]
    public async Task Scalar_kernels_preserve_bits_loops_and_caught_faults(bool optimized, int enabled)
    {
        var run = IntegerObjectTranslationTests.Translate(nameof(TypedHelperProbe.Run), fixture: typeof(TypedHelperProbe));
        var fault = IntegerObjectTranslationTests.Translate(nameof(TypedHelperProbe.Fault), fixture: typeof(TypedHelperProbe));
        Assert.True(run.Success, string.Join("; ", run.Diagnostics)); Assert.True(fault.Success, string.Join("; ", fault.Diagnostics));
        string text = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(run.Program!)).Text;
        Assert.Contains("// Proven typed kernel:", text, StringComparison.Ordinal);
        await IntegerObjectTranslationTests.VerifyTranslated(optimized,
            [("TypedRun", run.Program!, TypedHelperProbe.Run), ("TypedFault", fault.Program!, TypedHelperProbe.Fault)],
            nativeFlags: ["-DCILICA_TYPED_INTERNALS=" + enabled]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Fast_calls_preserve_cold_depth_pending_and_budget_fallbacks(bool optimized)
    {
        var translated = IntegerObjectTranslationTests.Translate(nameof(TypedHelperProbe.Run), fixture: typeof(TypedHelperProbe));
        Assert.True(translated.Success, string.Join("; ", translated.Diagnostics));
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions(translated.Program!));
        string hash = artifact.Source.ContentHash;
        string symbol = "cilica_m_" + hash + "_" + typeof(TypedHelperProbe).GetMethod(nameof(TypedHelperProbe.Rotate))!.MetadataToken.ToString("x8", CultureInfo.InvariantCulture);
        string deep = "cilica_m_" + hash + "_" + typeof(TypedHelperProbe).GetMethod(nameof(TypedHelperProbe.Deep))!.MetadataToken.ToString("x8", CultureInfo.InvariantCulture);
        string source = artifact.Text + $$"""
            #include <cassert>
            #include <pthread.h>
            using namespace {{artifact.EntryPoint[..^4]}}_closure;
            static void* execute(void*) {
            #if defined(__APPLE__)
                assert(pthread_get_stacksize_np(pthread_self()) <= 576u*1024u);
            #endif
                Instance heap; Context c(heap, 0); LocationArgument args[2]{}; args[0].bits=0x80000001u; args[1].bits=1;
                (void){{symbol}}(c,args); assert(c.pending==9 && c.used==0 && c.depth==0);
                c.pending=0; Frame f; RootScope roots(c,f,nullptr,0);
                assert({{symbol}}(c,args)==3 && c.pending==0 && c.depth==1 && c.used==0);
                c.pending=2; c.method=123; c.il=456; (void){{symbol}}(c,args);
                assert(c.pending==2 && c.method==123 && c.il==456 && c.depth==1);
                c.pending=0; c.depth=64; (void){{symbol}}(c,args);
                assert(c.pending==8 && c.depth==64 && c.used==0); c.depth=1;
                Context chain(heap); LocationArgument input[2]{}; input[0].bits=62; input[1].bits=17;
                assert({{deep}}(chain,input)=={{TypedHelperProbe.Deep(62,17)}} && chain.pending==0 && chain.depth==0 && chain.top==nullptr);
                input[0].bits=63; (void){{deep}}(chain,input);
                assert(chain.pending==8 && chain.depth==0 && chain.top==nullptr && chain.used==0);
                return nullptr;
            }
            int main() {
                pthread_attr_t attr; pthread_t thread; assert(pthread_attr_init(&attr)==0);
                assert(pthread_attr_setstacksize(&attr,512u*1024u)==0);
                assert(pthread_create(&thread,&attr,execute,nullptr)==0); assert(pthread_attr_destroy(&attr)==0);
                assert(pthread_join(thread,nullptr)==0);
            }
            """;
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("typed-budget"), cpp = Path.Combine(dir,"probe.cpp"), exe = Path.Combine(dir,"probe");
        File.WriteAllText(cpp,source);
        var build = await NativeProcess.Run(Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++",
            ["-std=c++20","-pthread",optimized ? "-O2" : "-O0","-DCILICA_TYPED_INTERNALS=1","-Wframe-larger-than=16384","-fno-exceptions","-fno-rtti","-Wall","-Wextra","-Werror","-fsanitize=address,undefined","-fno-sanitize-recover=all",cpp,"-o",exe],dir);
        Assert.True(build.ExitCode==0,build.Stderr);
        var result=await NativeProcess.Run(exe,[],dir);Assert.True(result.ExitCode==0,result.Stderr);Assert.Empty(result.Stderr);
    }
}
