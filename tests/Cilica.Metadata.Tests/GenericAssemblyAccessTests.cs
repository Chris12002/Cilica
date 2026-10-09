using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class GenericAssemblyAccessTests
{
    private static readonly string[] Names = ["Test", "Cast", "Nested", "Constructed", "Arrays"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(GenericAssemblyAccessProbe), Names));
    [Theory] [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    public async Task Generic_callers_share_their_assembly_type_visibility(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(GenericAssemblyAccessProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }
    [Theory]
    [InlineData(false, false, true)] [InlineData(true, false, false)] [InlineData(true, true, true)]
    public void Assembly_visibility_requires_the_exact_source_image(bool foreign, bool publicType, bool success)
    {
        using var files = new ResolverFiles();
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        using var bootstrap = AssemblyResolver.Create([runtime]).Value!;
        var core = bootstrap.OpenInput(typeof(object).Assembly.Location).Value!.Identity;
        var targetIdentity = new AssemblyIdentity("AssemblyAccessTarget", new Version(1, 0, 0, 0));
        var target = new ResolutionAssemblyBuilder(targetIdentity);
        var obj = target.TypeReference("System", "Object", target.Reference(core));
        var declaration = target.Type("Test", "Visible", baseType: obj, attributes: publicType ? TypeAttributes.Public : TypeAttributes.NotPublic);
        var caller = foreign ? new ResolutionAssemblyBuilder(new("AssemblyAccessCaller", new Version(1, 0, 0, 0))) : target;
        if (foreign) files.Write("AssemblyAccessTarget.dll", target.Build());
        EntityHandle tested = foreign ? caller.TypeReference("Test", "Visible", caller.Reference(targetIdentity)) : declaration;
        var owner = caller.Type("Test", "Caller", arity: 1);
        caller.Method("Test", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Isinst); il.Token(tested); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); il.LoadArgument(1); il.OpCode(ILOpCode.Add); il.OpCode(ILOpCode.Ret);
        });
        var closed = caller.Specification(0x15, 0x12, checked((byte)(MetadataTokens.GetRowNumber(owner) << 2)), 1, 8);
        var call = caller.Member(closed, "Test", 0, 2, 8, 8, 8);
        caller.Type("Test", "Root");
        var root = caller.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(0); il.LoadArgument(1); il.Call(call); il.OpCode(ILOpCode.Ret);
        });
        string path = files.Write(foreign ? "AssemblyAccessCaller.dll" : "AssemblyAccessTarget.dll", caller.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), runtime]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(path).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.True(result.Success == success, string.Join("; ", result.Diagnostics));
        if (success) ScalarManagedExceptionIrValidator.Validate(ValueScalarLowering.LowerExceptions(result.Program!));
        else Assert.Contains(result.Diagnostics, d => d.Message.Contains("lexical declaring scope", StringComparison.Ordinal));
    }
}
