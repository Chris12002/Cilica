using System;
using System.Collections.Immutable;
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
public sealed class LexicalCallTests
{
    private static readonly string[] Names = ["Static", "Instance", "Null", "GenericMethod", "Borrow", "Fault", "Callback", "Deep", "EnumBorrow"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(LexicalCallProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Nested_private_calls_preserve_values_borrows_faults_and_receivers(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(LexicalCallProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray(), persistentRoots: group == 2);
    }
    [Theory]
    [InlineData("caller-chain")] [InlineData("target-chain")] [InlineData("generic-chain")] [InlineData("private-scope")]
    public async Task Detached_lexical_permissions_require_consistent_bounded_evidence_in_both_stages(string mutation)
    {
        var program = (await Programs.Value)[0];
        ImmutableArray<ObjectMethodBinding> Mutate(ImmutableArray<ObjectMethodBinding> bindings)
        {
            var target = bindings.Single(b => (b.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private);
            return [.. bindings.Select(b => {
                if (mutation == "target-chain" && b == target) return b with { LexicalOwners = [] };
                if (mutation == "private-scope" && b == target) return b with { Attributes = b.Attributes & ~MethodAttributes.MemberAccessMask };
                if (mutation == "caller-chain" && b.LexicalOwners.Length > 1) return b with { LexicalOwners = [] };
                if (mutation == "generic-chain") return b with { LexicalOwners = [.. b.LexicalOwners.Select(n => n.Token == target.Owner.Token ? n with { GenericArity = 1 } : n)] };
                return b;
            })];
        }
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(program with { Flow = program.Flow with { Bindings = Mutate(program.Flow.Bindings) } }));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var invalid = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Bindings = Mutate(objects.Bindings) } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalid));
    }
    [Fact]
    public void Generic_owner_private_access_remains_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("UnsupportedGenericOwner", fixture: typeof(LexicalCallProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Calls require", StringComparison.Ordinal));
    }
    [Fact]
    public void Matching_nested_tokens_in_another_image_do_not_grant_private_access()
    {
        using var files = new ResolverFiles();
        var identity = new AssemblyIdentity("PrivateTarget", new Version(1,0,0,0));
        var target = new ResolutionAssemblyBuilder(identity);
        target.Type("Test", "Outer");
        target.Method("Hidden", [0, 1, 8, 8], attributes: MethodAttributes.Private | MethodAttributes.Static,
            emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        files.Write("PrivateTarget.dll", target.Build());
        var caller = new ResolutionAssemblyBuilder(new("PrivateCaller", new Version(1,0,0,0)));
        var foreignOwner = caller.TypeReference("Test", "Outer", caller.Reference(identity));
        var hidden = caller.Member(foreignOwner, "Hidden", 0, 1, 8, 8);
        var localOwner = caller.Type("Test", "Outer"); caller.Type("", "Caller", parent: localOwner);
        var root = caller.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.LoadArgument(0); il.Call(hidden); il.OpCode(ILOpCode.Ret);
        });
        string path = files.Write("PrivateCaller.dll", caller.Build());
        using var resolver = AssemblyResolver.Create([files.DirectoryPath("refs"), Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var input = resolver.OpenInput(path).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("Calls require", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("nested", true, false)] [InlineData("sibling", false, false)] [InlineData("unrelated", false, false)] [InlineData("parent", false, false)] [InlineData("nested", false, true)]
    public void Private_member_access_requires_containment_in_the_declaring_type(string relation, bool success, bool legacy)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("PrivateContainment", new Version(1,0,0,0)));
        var outer = b.Type("Test", "Outer"); MethodDefinitionHandle root;
        if (relation == "parent")
        {
            root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.Call(MetadataTokens.MethodDefinitionHandle(2)); il.OpCode(ILOpCode.Ret); });
            b.Type("", "Inner", parent: outer);
            b.Method("Hidden", [0, 1, 8, 8], attributes: MethodAttributes.Private | MethodAttributes.Static, emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
        }
        else
        {
            if (relation == "sibling") b.Type("", "Left", parent: outer);
            var target = b.Method("Hidden", [0, 1, 8, 8], attributes: MethodAttributes.Private | MethodAttributes.Static, emit: il => { il.LoadArgument(0); il.OpCode(ILOpCode.Ret); });
            b.Type("Test", "Caller", parent: relation == "unrelated" ? default : outer);
            root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => { il.LoadArgument(0); il.Call(target); il.OpCode(ILOpCode.Ret); });
        }
        var input = resolver.OpenInput(files.Write("input.dll", b.Build())).Value!;
        var result = legacy ? ValueTranslator.TranslateExceptions(resolver, input, root, library) : ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.True(result.Success == success, string.Join("; ", result.Diagnostics));
        if (!success) Assert.Contains(result.Diagnostics, d => d.Message.Contains("Calls require", StringComparison.Ordinal));
    }
}
