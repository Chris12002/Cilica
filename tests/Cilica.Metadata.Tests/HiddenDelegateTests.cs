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
public sealed class HiddenDelegateTests
{
    private static readonly string[] Names = ["Static", "Internal", "Instance", "Array", "List", "Types", "Reference", "Void", "Fault", "Deep", "Value"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(HiddenDelegateProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task Lexically_visible_delegates_preserve_calls_identity_roots_and_faults(bool optimized, int group)
    {
        var names = Names.Skip(group * 3).Take(3).ToArray();
        var programs = (await Programs.Value).Skip(group * 3).Take(3).ToArray();
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, names.Select((name, i) =>
            (name, programs[i], typeof(HiddenDelegateProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).ToArray());
    }
    [Fact]
    public void Hidden_generic_delegates_remain_diagnostic()
    {
        var result = IntegerObjectTranslationTests.Translate("UnsupportedGeneric", fixture: typeof(HiddenDelegateProbe));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("delegate declarations", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("public")] [InlineData("missing")] [InlineData("token")] [InlineData("cycle")]
    [InlineData("generic")] [InlineData("parent")] [InlineData("legacy")]
    public async Task Both_stages_reject_invalid_or_inaccessible_delegate_facts(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        ObjectTypeCatalog Mutate(ObjectTypeCatalog model)
        {
            var target = model.Delegates.Single(d => !d.ExternallyVisible);
            var chain = target.LexicalOwners;
            var changed = mutation switch {
                "public" => target with { ExternallyVisible = true },
                "missing" => target with { LexicalOwners = [] },
                "token" => target with { LexicalOwners = chain.SetItem(0, chain[0] with { Token = 0x02007fff }) },
                "cycle" => target with { LexicalOwners = chain.SetItem(1, chain[1] with { Token = chain[0].Token }) },
                "generic" => target with { LexicalOwners = chain.SetItem(0, chain[0] with { GenericArity = 1 }) },
                "parent" => target with { LexicalOwners = chain.SetItem(1, chain[1] with { Token = 0x02007fff }) },
                _ => target };
            return model with { Delegates = model.Delegates.Replace(target, changed), Integer64 = mutation != "legacy" && model.Integer64 };
        }
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Types = flow.Types with { References = Mutate(flow.Types.References) } } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Types = Mutate(objects.Flow.Types) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Foreign_images_cannot_cast_to_hidden_delegates(bool nested)
    {
        using var files = new ResolverFiles();
        using var resolver = AssemblyResolver.Create([Path.GetDirectoryName(typeof(object).Assembly.Location)!, Path.GetDirectoryName(typeof(HiddenDelegateProbe).Assembly.Location)!]).Value!;
        var library = resolver.OpenInput(typeof(object).Assembly.Location).Value!;
        var fixture = resolver.OpenInput(typeof(HiddenDelegateProbe).Assembly.Location).Value!;
        var b = new ResolutionAssemblyBuilder(new("HiddenDelegateCaller", new Version(1, 0, 0, 0)));
        var reference = b.Reference(fixture.Identity);
        var owner = b.TypeReference("Cilica.Fixtures.Execution", "HiddenDelegateProbe", reference);
        var target = nested ? b.TypeReference("", "Operation", owner) : b.TypeReference("Cilica.Fixtures.Execution", "AssemblyCallback", reference);
        b.Type("Test", "Caller");
        var root = b.Method("Run", [0, 2, 8, 8, 8], emit: il => {
            il.OpCode(ILOpCode.Ldnull); il.OpCode(ILOpCode.Isinst); il.Token(target); il.OpCode(ILOpCode.Pop);
            il.LoadArgument(0); il.OpCode(ILOpCode.Ret);
        });
        var input = resolver.OpenInput(files.Write("caller.dll", b.Build())).Value!;
        var result = ValueTranslator.TranslateIntegerObjects(resolver, input, root, library);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("visible", StringComparison.OrdinalIgnoreCase));
    }
}
