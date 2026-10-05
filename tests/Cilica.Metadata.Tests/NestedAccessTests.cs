using System;
using System.Linq;
using System.Reflection;
using Cilica.Metadata.Resolution;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class NestedAccessTests
{
    private static readonly (string Name, Func<int, int, int> Oracle)[] Probes = [
        ("Generated", NestedCacheProbe.Generated), ("Declared", NestedCacheProbe.Declared), ("Siblings", NestedCacheProbe.Siblings)];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() =>
        CoreLibraryBuildTests.TranslateFixtures(typeof(NestedCacheProbe), Probes.Select(p => p.Name).ToArray()));
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Lexical_cache_access_matches_dotnet(bool optimized)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Probes.Select((p, i) => (p.Name, programs[i], p.Oracle)).ToArray(), persistentRoots: true);
    }
    [Theory]
    [InlineData("unrelated")] [InlineData("private-member")] [InlineData("cycle")]
    [InlineData("contradiction")] [InlineData("visibility")] [InlineData("missing")] [InlineData("default")] [InlineData("cross-image")] [InlineData("assembly-claim")]
    public async Task Lexical_access_requires_valid_consistent_scope_and_member_visibility(string mutation)
    {
        var program = (await Programs.Value)[0]; var flow = program.Flow.Flow.Flow;
        if (mutation == "assembly-claim")
        {
            var hidden = program.Flow.Bindings.First(b => !b.OwnerExternallyVisible && !b.OwnerAssemblyVisible);
            program = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(hidden, hidden with { OwnerAssemblyVisible = true }) } };
        }
        else if (mutation == "cross-image")
        {
            var root = program.Flow.Bindings.Single(b => b.Method == flow.Entry);
            var method = flow.Methods.Single(m => IrMethodId.From(m.Source) == flow.Entry);
            var changed = method with { Source = method.Source with { ContentHash = new string('1', 64) } };
            var id = IrMethodId.From(changed.Source);
            flow = flow with { Entry = id, Methods = flow.Methods.Replace(method, changed) };
            program = program with { Flow = program.Flow with {
                Bindings = program.Flow.Bindings.Replace(root, root with { Method = id, Owner = root.Owner with { ContentHash = id.ContentHash } }),
                Flow = program.Flow.Flow with { Flow = flow } } };
        }
        else if (mutation == "unrelated")
        {
            var root = program.Flow.Bindings.Single(b => b.Method == flow.Entry);
            var owner = root.Owner with { Token = 0x0200fffe };
            var changed = root with { Owner = owner, LexicalOwners = [new(owner.Token, TypeAttributes.Public, 0)] };
            program = program with { Flow = program.Flow with { Bindings = program.Flow.Bindings.Replace(root, changed) } };
        }
        else
        {
            var field = flow.Types.References.Statics.First(f => !f.OwnerExternallyVisible);
            var changed = mutation switch {
                "private-member" => field with { Attributes = (field.Attributes & ~FieldAttributes.FieldAccessMask) | FieldAttributes.Private },
                "cycle" => field with { LexicalOwners = field.LexicalOwners.Add(field.LexicalOwners[0]) },
                "contradiction" => field with { LexicalOwners = field.LexicalOwners.SetItem(1, field.LexicalOwners[1] with { Token = 0x0200fffe }) },
                "visibility" => field with { OwnerExternallyVisible = true },
                "missing" => field with { LexicalOwners = [] },
                _ => field with { LexicalOwners = default } };
            flow = flow with { Types = flow.Types with { References = flow.Types.References with { Statics = flow.Types.References.Statics.Replace(field, changed) } } };
            program = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow } } };
        }
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(program));
    }

}
