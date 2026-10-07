using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Cilica.Compiler;
using Cilica.Fixtures.Execution;
using Xunit;
namespace Cilica.Metadata.Tests;
public sealed class ExceptionMessageConstructionTests
{
    private static readonly string[] Names = ["Plain", "Derived", "Utf16", "Evaluation", "Two"];
    private static readonly Lazy<Task<ManagedExceptionIrProgram[]>> Programs = new(() => CoreLibraryBuildTests.TranslateFixtures(typeof(ExceptionMessageProbe), Names));
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public async Task Constructed_messages_match_dotnet(bool optimized, int group)
    {
        var programs = await Programs.Value;
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, Names.Select((name, i) =>
            (name, programs[i], typeof(ExceptionMessageProbe).GetMethod(name)!.CreateDelegate<Func<int, int, int>>())).Skip(group * 2).Take(2).ToArray());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Internal_constructor_message_matches_dotnet(bool optimized)
    {
        var program = (await CoreLibraryBuildTests.TranslateFixtures(typeof(InternalExceptionProbe), "ConstructedMessage"))[0];
        await IntegerObjectTranslationTests.VerifyTranslated(optimized, [("ConstructedMessage", program, InternalExceptionProbe.ConstructedMessage)]);
    }
    [Theory]
    [InlineData("operand")] [InlineData("message")] [InlineData("forward")] [InlineData("result")] [InlineData("fault")] [InlineData("duplicate")]
    public async Task Both_stages_require_fresh_shell_typed_message_and_fault_edge(string mutation)
    {
        var program = (await Programs.Value)[mutation == "duplicate" ? 4 : 0]; var flow = program.Flow.Flow.Flow;
        System.Collections.Immutable.ImmutableArray<IrMethod> Methods(System.Collections.Immutable.ImmutableArray<IrMethod> methods) =>
            [.. methods.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Statements = [.. b.Statements.Select(statement =>
                statement is IrStatement.Define d && d.Value.Expression is IrExpression.InitializeExceptionMessage init ? mutation switch {
                    "duplicate" => d with { Value = d.Value with { Expression = init with { Operand = b.Values.Select(v => v.Expression).OfType<IrExpression.InitializeExceptionMessage>().First().Operand } } },
                    "operand" => d with { Value = d.Value with { Expression = init with { Operand = init.Message } } },
                    "message" => d with { Value = d.Value with { Expression = init with { Message = init.Operand } } },
                    "forward" => d with { Value = d.Value with { Expression = init with { Message = d.Value.Id } } },
                    "result" => d with { Value = d.Value with { Type = IrType.Int32 } },
                    "fault" => d with { Exceptional = null }, _ => statement } : statement)] })] })];
        var invalid = program with { Flow = program.Flow with { Flow = program.Flow.Flow with { Flow = flow with { Methods = Methods(flow.Methods) } } } };
        Assert.Throws<ArgumentException>(() => ManagedExceptionIrValidator.Validate(invalid));
        var scalar = ValueScalarLowering.LowerExceptions(program); var boxes = scalar.Flow.Flow; var objects = boxes.Flow.Flow;
        var invalidScalar = scalar with { Flow = scalar.Flow with { Flow = boxes with { Flow = boxes.Flow with { Flow = objects with { Flow = objects.Flow with { Methods = Methods(objects.Flow.Methods) } } } } } };
        Assert.Throws<ArgumentException>(() => ScalarManagedExceptionIrValidator.Validate(invalidScalar));
    }
    [Theory]
    [InlineData("UnsupportedSideEffect")] [InlineData("UnsupportedBase")] [InlineData("UnsupportedLookalike")]
    public async Task Unrecognized_behavior_stays_diagnostic(string name)
    {
        using var files = new ResolverFiles();
        var (built, _, resolver, implementation, plan) = await CoreLibraryBuildTests.BuildAndMap(files);
        using var lifetime = resolver;
        using var scope = resolver.CreateTrustedLibraryCompilationScope(plan,
            await File.ReadAllBytesAsync(built.ManifestPath, TestContext.Current.CancellationToken), built.ManifestSha256).Value!;
        var input = scope.OpenInput(typeof(ExceptionMessageProbe).Assembly.Location).Value!;
        var method = (MethodDefinitionHandle)MetadataTokens.EntityHandle(typeof(ExceptionMessageProbe).GetMethod(name)!.MetadataToken);
        var result = ValueTranslator.TranslateIntegerObjects(scope, input, method, implementation);
        Assert.False(result.Success); Assert.Contains(result.Diagnostics, d => d.Message.Contains("unchanged arguments", StringComparison.Ordinal));
    }
}
