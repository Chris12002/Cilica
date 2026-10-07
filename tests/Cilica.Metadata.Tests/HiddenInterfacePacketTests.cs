using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class HiddenInterfacePacketTests
{
    private static readonly string[] Names = ["InternalCount", "InternalCopy", "PrivateRead", "CopyFault"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(async () => {
        var programs = new ManagedExceptionIrProgram[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            var result = await Translate(Names[i]);
            Assert.True(result.Success, Names[i] + ": " + string.Join("; ", result.Diagnostics)); programs[i] = result.Program!;
        }
        return programs;
    });
    [Fact]
    public async Task Foreign_entry_can_reach_authorized_hidden_array_interface_packets()
    {
        var result = await Translate("InternalCount");
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }
    [Theory]
    [InlineData(false,0)] [InlineData(true,0)] [InlineData(false,1)] [InlineData(true,1)]
    public async Task Native_hidden_packets_match_the_owning_assembly(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name,i) =>
            (name, programs[i], typeof(HiddenInterfacePacketProbe).GetMethod(name)!.CreateDelegate<Func<int,int,int>>())).Skip(group*2).Take(2).ToArray());
    }
    [Fact]
    public async Task Authorized_call_does_not_grant_foreign_explicit_enum_access()
    {
        var result = await Translate("InternalCount", explicitName: true);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("calling assembly", StringComparison.Ordinal));
    }
    private static async Task<ManagedExceptionTranslationResult> Translate(string name, bool explicitName = false)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files); using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan, await System.IO.File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var owner = scope.OpenInput(typeof(HiddenInterfacePacketProbe).Assembly.Location).Value!;
        var wrapper = new ResolutionAssemblyBuilder(new("ForeignInterfacePacketEntry", new Version(1,0,0,0)));
        var reference = wrapper.Reference(owner.Identity);
        var target = wrapper.TypeReference("Cilica.Fixtures.Execution", "HiddenInterfacePacketProbe", reference);
        var method = wrapper.Member(target, name, 0, 2, 8, 8, 8);
        var hidden = wrapper.TypeReference("Cilica.Fixtures.Execution", "HiddenDispatchPhase", reference);
        wrapper.Type("Test", "Entry");
        var entry = wrapper.Method("Run", [0,2,8,8,8], emit: il => {
            il.LoadArgument(0); il.LoadArgument(1); il.Call(method);
            if (explicitName) { il.OpCode(ILOpCode.Ldtoken); il.Token(hidden); il.OpCode(ILOpCode.Pop); }
            il.OpCode(ILOpCode.Ret);
        });
        var input = scope.OpenInput(files.Write("ForeignInterfacePacketEntry.dll", wrapper.Build())).Value!;
        return ValueTranslator.TranslateIntegerObjects(scope, input, entry, implementation);
    }
}
