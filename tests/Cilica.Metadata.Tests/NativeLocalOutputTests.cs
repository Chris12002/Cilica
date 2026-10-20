using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class NativeLocalOutputTests
{
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(LocalOutputProbe), ["Run"]));

    [Theory]
    [InlineData(false, 0, 0)] [InlineData(true, 0, 0)]
    [InlineData(false, 1, 0)] [InlineData(true, 1, 0)]
    [InlineData(false, 1, 1)] [InlineData(true, 1, 1)]
    public async Task Proven_local_outputs_preserve_fault_writes_wide_values_dispatch_and_aliases(bool optimized, int typed, int diagnostics)
    {
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("Outputs", (await Programs.Value)[0], LocalOutputProbe.Run)],
            nativeFlags: ["-DCILICA_TYPED_INTERNALS=" + typed, "-DCILICA_VERIFY_INTERNAL_REFERENCES=" + diagnostics,
                "-DCILICA_VERIFY_BORROW_LIFETIMES=" + diagnostics, "-Wframe-larger-than=16384"]);
    }

    [Fact]
    public async Task Only_single_local_outputs_get_the_new_entry_and_dispatch_is_exact()
    {
        var artifact = ObjectCppEmitter.EmitExceptions(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]));
        string Symbol(string name) => "cilica_m_" + artifact.Source.ContentHash + "_"
            + typeof(LocalOutputProbe).GetMethod(name)!.MetadataToken.ToString("x8", CultureInfo.InvariantCulture);
        Assert.Contains(Symbol("Write") + "_local_output(context,", artifact.Text, StringComparison.Ordinal);
        Assert.Contains(Symbol("Wide") + "_local_output(context,", artifact.Text, StringComparison.Ordinal);
        Assert.Contains("direct_local_output(context) && f->dispatch_int32 == &", artifact.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Symbol("Pair") + "_local_output", artifact.Text, StringComparison.Ordinal);
        Assert.Contains("auto staged = *p", artifact.Text, StringComparison.Ordinal);
        Assert.Contains("LocationOrigin::frame_wide_integer, slot", artifact.Text, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Local_output_entries_link_across_units_and_depth_boundaries_keep_framed_behavior()
    {
        var module = ObjectCppEmitter.EmitExceptionModule(ValueScalarLowering.LowerExceptions((await Programs.Value)[0]), 1);
        using var files = new ResolverFiles(); string dir = files.DirectoryPath("local-output-module");
        foreach (var file in module.Sources.Prepend(module.Header)) File.WriteAllText(Path.Combine(dir, file.Name), file.Text);
        string prefix = "cilica_m_" + module.Source.ContentHash + "_";
        string entry = prefix + module.Source.Token.ToString("x8", CultureInfo.InvariantCulture);
        string write = prefix + typeof(LocalOutputProbe).GetMethod("Write")!.MetadataToken.ToString("x8", CultureInfo.InvariantCulture);
        string main = Path.Combine(dir, "main.cpp");
        File.WriteAllText(main, $$"""
            #include "{{module.Header.Name}}"
            #include <cassert>
            using namespace {{module.EntryPoint[..^4]}}_closure;
            static void nested(Context& c, unsigned remaining, std::uint32_t* value, std::uint64_t token) {
                Frame roots; RootScope scope(c,roots,nullptr,0);
                if(remaining>1) nested(c,remaining-1,value,token);
                else {
                    assert(c.depth==64 && !direct_local_output(c));
                    assert({{write}}_local_output(c,3,value,token,0)==0 && c.pending==8 && *value==10);
                    c.pending=0;
                }
            }
            int main() {
                Instance heap; heap.collect_before_allocation=true; Context c(heap); LocationArgument args[2]{};
                args[0].bits=17;args[1].bits=1;assert({{entry}}(c,args)=={{LocalOutputProbe.Run(17,1)}} && c.pending==0);
                assert(c.depth==0 && c.used==0 && c.top==nullptr && c.borrows==nullptr);
                assert(!direct_local_output(c));
                {
                    Frame roots; RootScope scope(c,roots,nullptr,0);std::uint32_t value=7;
                    BorrowScope borrow(c,roots,&value,1,nullptr);assert(borrow.status==LocationStatus::complete);
                    assert(direct_local_output(c));
                    assert({{write}}_local_output(c,3,&value,borrow.frame.token,0)==70 && value==10);
                    c.pending=6;assert(!direct_local_output(c));c.pending=0;
                    heap.marking=true;assert(!direct_local_output(c));heap.marking=false;
                    nested(c,63,&value,borrow.frame.token);assert(c.pending==0 && value==10 && c.depth==1);
                }
                assert(c.depth==0 && c.used==0 && c.top==nullptr && c.borrows==nullptr);
            }
            """);
        string compiler = Environment.GetEnvironmentVariable("CILICA_CXX") ?? "clang++";
        var objects = new List<string>();
        foreach (string cpp in module.Sources.Select(f => Path.Combine(dir, f.Name)).Append(main))
        {
            string obj = cpp + ".o";
            var build = await NativeProcess.Run(compiler, ["-std=c++20", "-O2", "-DCILICA_TYPED_INTERNALS=1",
                "-DCILICA_VERIFY_INTERNAL_REFERENCES=0", "-DCILICA_VERIFY_BORROW_LIFETIMES=0", "-fno-exceptions", "-fno-rtti",
                "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined", "-fno-sanitize-recover=all", "-c", cpp, "-o", obj], dir);
            Assert.True(build.ExitCode == 0, build.Stderr); objects.Add(obj);
        }
        string exe = Path.Combine(dir, "probe");
        var link = await NativeProcess.Run(compiler, ["-fsanitize=address,undefined", .. objects, "-o", exe], dir);
        Assert.True(link.ExitCode == 0, link.Stderr);
        var run = await NativeProcess.Run(exe, [], dir); Assert.True(run.ExitCode == 0, run.Stderr); Assert.Empty(run.Stderr);
    }

}
