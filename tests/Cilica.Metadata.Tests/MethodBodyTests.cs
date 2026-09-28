using System;
using System.IO;
using System.Linq;
using Cilica.Metadata.IL;
using Cilica.Metadata.Output;
using Xunit;

namespace Cilica.Metadata.Tests;

public sealed class MethodBodyTests
{
    private const string FlowType = "Cilica.Fixtures.ControlFlow.Flow";

    [Fact]
    public void Text_output_matches_golden_file()
    {
        ILReport report = MethodBodyInspector.Inspect(Fixture.PathOf(Fixture.ControlFlow));

        Golden.Verify(Fixture.ControlFlow + ".il.txt", ILTextWriter.Write(report));
    }

    [Fact]
    public void Json_output_matches_golden_file()
    {
        ILReport report = MethodBodyInspector.Inspect(Fixture.PathOf(Fixture.ControlFlow), name => name.Contains("::TypeTestFilter", StringComparison.Ordinal));

        Golden.Verify(Fixture.ControlFlow + ".TypeTestFilter.il.json", ILJsonWriter.Write(report));
    }

    [Theory]
    [InlineData("TypeTestFilter", "type-test")]
    [InlineData("CallingFilter", "general")]
    [InlineData("StateFilter", "general")]
    public void Classifies_exception_filters(string method, string expected)
    {
        MethodBodyReport body = Method(method);

        ExceptionRegionReport filter = Assert.Single(body.Regions);
        Assert.Equal("filter", filter.Kind);
        Assert.Equal(expected, filter.FilterKind);
    }

    [Fact]
    public void Loops_have_a_back_edge()
    {
        MethodBodyReport body = Method("Loop");

        Assert.Contains(body.Blocks, block => block.Successors.Any(successor => successor <= block.Index));
        Assert.All(body.Blocks, block => Assert.NotNull(block.StackOnEntry));
    }

    [Fact]
    public void Switch_targets_every_case()
    {
        MethodBodyReport body = Method("Switch");

        InstructionReport instruction = Assert.Single(body.Instructions, i => i.OpCode == "switch");
        Assert.Equal(4, instruction.Operand!.Split(',').Length);
        BasicBlockReport block = body.Blocks.Single(b => b.Start <= instruction.Offset && instruction.Offset < b.End);
        Assert.Equal(5, block.Successors.Length);
    }

    [Fact]
    public void Handlers_start_with_the_exception_object_on_the_stack()
    {
        MethodBodyReport body = Method("TryCatchFinally");

        ExceptionRegionReport handler = Assert.Single(body.Regions, r => r.Kind == "catch");
        ExceptionRegionReport cleanup = Assert.Single(body.Regions, r => r.Kind == "finally");
        Assert.Equal("[netstandard]System.IndexOutOfRangeException", handler.CatchType);
        Assert.Equal(1, body.Blocks.Single(b => b.Start == handler.HandlerStart).StackOnEntry);
        Assert.Equal(0, body.Blocks.Single(b => b.Start == cleanup.HandlerStart).StackOnEntry);
    }

    [Fact]
    public void Formats_tokens_with_the_callers_generic_parameters()
    {
        MethodBodyReport body = Method("Largest");

        Assert.Equal($"{FlowType}::Largest<T>(!!T[])", body.Name);
        Assert.Contains(body.Instructions, i => i.OpCode == "constrained." && i.Operand == "!!T");
        Assert.Contains(body.Instructions, i => i.Operand == "[netstandard]System.IComparable`1<!!T>::CompareTo(!0)");
    }

    [Fact]
    public void Formats_call_site_signatures_and_type_tokens()
    {
        Assert.Contains(Method("CallPointer").Instructions, i => i.OpCode == "calli" && i.Operand == "int32(int32)");
        Assert.Contains(Method("TypeToken").Instructions,
            i => i.OpCode == "ldtoken" && i.Operand == "[netstandard]System.Collections.Generic.List`1<string>");
        Assert.Contains(Method("Switch").Instructions, i => i.OpCode == "ldstr" && i.Operand == "\"zero\"");
    }

    [Fact]
    public void Real_compiler_output_produces_no_diagnostics()
    {
        // Roslyn output, including the .NET core library, is valid IL; any diagnostic is a false positive.
        string[] assemblies =
        [
            Fixture.PathOf(Fixture.Basic),
            Fixture.PathOf(Fixture.ControlFlow),
            Path.Combine(AppContext.BaseDirectory, "Cilica.Metadata.dll"),
            typeof(object).Assembly.Location,
        ];
        foreach (string assembly in assemblies)
        {
            ILReport report = MethodBodyInspector.Inspect(assembly);

            Assert.True(report.Diagnostics.IsEmpty, $"{Path.GetFileName(assembly)}: {string.Join("; ", report.Diagnostics.Take(3))}");
            Assert.NotEmpty(report.Methods);
        }
    }

    private static MethodBodyReport Method(string name) =>
        Assert.Single(MethodBodyInspector.Inspect(Fixture.PathOf(Fixture.ControlFlow)).Methods,
            m => m.Name.StartsWith(FlowType + "::" + name + "(", StringComparison.Ordinal)
                || m.Name.StartsWith(FlowType + "::" + name + "<", StringComparison.Ordinal));
}
