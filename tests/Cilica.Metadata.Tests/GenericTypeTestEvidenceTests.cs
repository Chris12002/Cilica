using System;
using System.Linq;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class GenericTypeTestEvidenceTests
{
    private static readonly string[] Names = ["IsAs", "Cast", "NullCast", "Owner", "Constructed", "Vectors", "ValueVectors", "EnumVectors", "BoundVector", "PrimitiveVectors", "CastCollection", "OfTypeSequence", "CastSequence", "Skip"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(GenericTypeTestProbe), Names));

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)] [InlineData(false, 3)] [InlineData(true, 3)]
    [InlineData(false, 4)] [InlineData(true, 4)]
    public async Task Private_generic_type_tests_match_dotnet_with_precise_roots(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(GenericTypeTestProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 3).Take(3).ToArray());
    }

    [Fact]
    public async Task Generic_type_test_scan_bounds_detached_blocks_before_enumeration()
    {
        var program = (await Programs.Value)[0];
        var flow = program.Flow.Flow.Flow; var method = flow.Methods.First(m => m.GenericTypeTests is not null);
        foreach (bool tooManyBlocks in new[] { false, true })
        {
            var changed = method with { Blocks = tooManyBlocks
                ? [.. Enumerable.Repeat(method.Blocks[0], IrLimits.MaximumBlocks + 1)]
                : [method.Blocks[0] with { Statements = [.. Enumerable.Repeat<IrStatement>(new IrStatement.Define(new(0, IrType.Int32, 0, new IrExpression.Constant(0))), IrLimits.MaximumStatements + 1)] }] };
            var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, changed) } } } };
            Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        }
    }

    [Theory]
    [InlineData("missing")] [InlineData("index")] [InlineData("scope")] [InlineData("offset")]
    [InlineData("vector")] [InlineData("operation")] [InlineData("context")] [InlineData("duplicate")] [InlineData("default")]
    [InlineData("null-use")] [InlineData("null-context")] [InlineData("owner-context")]
    public async Task Detached_type_test_evidence_cannot_be_reused_for_different_contexts_or_instructions(string mutation)
    {
        foreach (int index in mutation == "owner-context" ? new[] {3} : new[] {0, 4, 5, 6, 7, 8})
        {
        // The constructed Box owner is public; its existing type visibility is sufficient without opaque evidence.
        if (mutation == "missing" && index == 4) continue;
        var program = (await Programs.Value)[index];
        var flow = program.Flow.Flow.Flow;
        var method = flow.Methods.First(m => m.GenericTypeTests is not null);
        var proof = method.GenericTypeTests!; var use = proof.Uses[0];
        GenericTypeTestEvidence? changed = mutation switch {
            "missing" => null,
            "index" => proof with { Uses = [use with { ParameterIndex = 7 }] },
            "scope" => proof with { Uses = [use with { MethodParameter = !use.MethodParameter }] },
            "offset" => proof with { Uses = [use with { ILOffset = use.ILOffset + 1 }] },
            "vector" => proof with { Uses = [use with { Vector = !use.Vector }] },
            "operation" => proof with { Uses = [use with { Cast = !use.Cast }] },
            "context" => proof with { Context = proof.Context with { Methods = [] } },
            "owner-context" => proof with { Context = proof.Context with { Types = [] } },
            "duplicate" => proof with { Uses = [use, use] },
            "default" => proof with { Uses = default },
            "null-use" => proof with { Uses = [null!] },
            _ => proof with { Context = null! } };
        var mutated = method with { GenericTypeTests = changed };
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = flow.Methods.Replace(method, mutated) } } } };
        Assert.Throws<ArgumentException>(() => ValueScalarLowering.LowerExceptions(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program);
        var scalarFlow = scalar.Flow.Flow.Flow.Flow.Flow;
        var scalarMethod = scalarFlow.Methods.Single(m => IrMethodId.From(m.Source) == IrMethodId.From(method.Source));
        var scalarInvalid = scalar with { Flow = scalar.Flow with { Flow = scalar.Flow.Flow with { Flow = scalar.Flow.Flow.Flow with {
            Flow = scalar.Flow.Flow.Flow.Flow with { Flow = scalarFlow with { Methods = scalarFlow.Methods.Replace(scalarMethod, scalarMethod with { GenericTypeTests = changed }) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(scalarInvalid));
        }
    }
}
