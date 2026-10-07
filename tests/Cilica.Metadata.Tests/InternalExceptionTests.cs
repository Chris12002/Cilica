using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Cilica.Metadata.Resolution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class InternalExceptionTests
{
    private static readonly string[] Names = ["Message", "Default", "Inheritance", "PublicOwner", "Identity"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(InternalExceptionProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Internal_exception_messages_catches_and_identity_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(InternalExceptionProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void Foreign_constructor_calls_stay_rejected_after_local_cache_population(bool warm, bool publicOwner)
    {
        using var files = new ResolverFiles();
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var bootstrap = AssemblyResolver.Create([runtime]).Value!;
        var coreIdentity = bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var homeIdentity = new AssemblyIdentity("LocalExceptions", new Version(1, 0, 0, 0));
        var foreignIdentity = new AssemblyIdentity("ForeignExceptions", new Version(1, 0, 0, 0));
        var home = new ResolutionAssemblyBuilder(homeIdentity);
        var basis = home.TypeReference("System", "Exception", home.Reference(coreIdentity));
        var baseConstructor = home.Member(basis, ".ctor", 0x20, 0, 1);
        home.Type("Test", "HiddenFault", baseType: basis, attributes: (publicOwner ? TypeAttributes.Public : TypeAttributes.NotPublic) | TypeAttributes.Sealed);
        var constructor = home.Method(".ctor", [0x20, 0, 1],
            attributes: MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            emit: il => { il.LoadArgument(0); il.Call(baseConstructor); il.OpCode(ILOpCode.Ret); });
        var foreignType = home.TypeReference("Test", "Foreign", home.Reference(foreignIdentity));
        var foreignCall = home.Member(foreignType, "Run", 0, 0, 8);
        home.Type("Test", "Entry");
        var root = home.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Newobj); il.Token(constructor); il.OpCode(ILOpCode.Pop);
            il.Call(foreignCall); il.OpCode(ILOpCode.Ret);
        });
        var foreign = new ResolutionAssemblyBuilder(foreignIdentity);
        var target = foreign.TypeReference("Test", "HiddenFault", foreign.Reference(homeIdentity));
        var illegal = foreign.Member(target, ".ctor", 0x20, 0, 1);
        foreign.Type("Test", "Foreign");
        var foreignRoot = foreign.Method("Run", [0, 0, 8], emit: il => {
            il.OpCode(ILOpCode.Newobj); il.Token(illegal); il.OpCode(ILOpCode.Pop); il.LoadConstantI4(0); il.OpCode(ILOpCode.Ret);
        });
        var homePath = files.Write("LocalExceptions.dll", home.Build());
        var foreignPath = files.Write("ForeignExceptions.dll", foreign.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), runtime]).Value!;
        var implementation = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(warm ? homePath : foreignPath).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, warm ? root : foreignRoot, implementation);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("visibility", StringComparison.Ordinal)
            || d.Message.Contains("lexical declaring scope", StringComparison.Ordinal)
            || d.Message.Contains("same-assembly", StringComparison.Ordinal));
    }
}
