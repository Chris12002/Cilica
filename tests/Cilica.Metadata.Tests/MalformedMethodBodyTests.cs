using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Cilica.Metadata.IL;
using Cilica.Metadata.Output;
using Xunit;

namespace Cilica.Metadata.Tests;

/// <summary>Malformed method bodies must produce stable diagnostics, never exceptions.</summary>
public sealed class MalformedMethodBodyTests
{
    private const string FlowType = "Cilica.Fixtures.ControlFlow.Flow";

    [Fact]
    public void Unknown_opcode_reports_CIL1010()
    {
        byte[] image = Fixture.Bytes(Fixture.ControlFlow);
        Body body = Body.Locate(image, "Checked");
        image[body.CodeStart] = 0x24;

        ILReport report = Inspect(image, "Checked");

        Diagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(DiagnosticIds.MalformedMethodBody, diagnostic.Id);
        Assert.Equal("Unknown opcode 0x24 at IL_0000.", diagnostic.Message);
        Assert.Empty(Assert.Single(report.Methods).Blocks);
    }

    [Fact]
    public void Branch_into_the_middle_of_an_instruction_reports_CIL1011()
    {
        byte[] image = Fixture.Bytes(Fixture.ControlFlow);
        Body body = Body.Locate(image, "Loop");
        ImmutableArray<InstructionReport> instructions = Instructions("Loop");
        InstructionReport branch = instructions.First(i => i.OpCode == "br.s");
        // Point the branch one byte into the first multi-byte instruction after it.
        int target = Enumerable.Range(0, instructions.Length - 1)
            .Where(index => instructions[index].Offset > branch.Offset && instructions[index + 1].Offset - instructions[index].Offset > 1)
            .Select(index => instructions[index].Offset + 1)
            .First();
        image[body.CodeStart + branch.Offset + 1] = (byte)(sbyte)(target - (branch.Offset + 2));

        ILReport report = Inspect(image, "Loop");

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InvalidControlTransfer && d.Message.Contains("not the start of an instruction", StringComparison.Ordinal));
    }

    [Fact]
    public void Stack_underflow_reports_CIL1013()
    {
        byte[] image = Fixture.Bytes(Fixture.ControlFlow);
        Body body = Body.Locate(image, "Checked");
        Assert.Equal("ldarg.0", Instructions("Checked")[0].OpCode);
        image[body.CodeStart] = 0x26; // pop

        ILReport report = Inspect(image, "Checked");

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InconsistentStack && d.Message == "pop needs 1 stack values but 0 are available.");
    }

    [Fact]
    public void Argument_index_out_of_range_reports_CIL1014()
    {
        byte[] image = Fixture.Bytes(Fixture.ControlFlow);
        Body body = Body.Locate(image, "Checked");
        image[body.CodeStart] = 0x05; // ldarg.3

        ILReport report = Inspect(image, "Checked");

        Diagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(DiagnosticIds.InvalidOperand, diagnostic.Id);
        Assert.Equal("ldarg.3 refers to argument 3, but the method has 2 arguments.", diagnostic.Message);
    }

    [Fact]
    public void Token_from_the_wrong_table_reports_CIL1014()
    {
        byte[] image = Fixture.Bytes(Fixture.ControlFlow);
        Body body = Body.Locate(image, "Rethrow");
        InstructionReport call = Instructions("Rethrow").First(i => i.OpCode == "callvirt");
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(body.CodeStart + call.Offset + 1), 0x02000001);

        ILReport report = Inspect(image, "Rethrow");

        Diagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(DiagnosticIds.InvalidOperand, diagnostic.Id);
        Assert.Equal("<invalid 0x02000001>", Assert.Single(report.Methods).Instructions.Single(i => i.Offset == call.Offset).Operand);
    }

    [Fact]
    public void Exception_region_past_the_end_reports_CIL1012()
    {
        byte[] image = Fixture.Bytes(Fixture.ControlFlow);
        Body body = Body.Locate(image, "Rethrow");
        int section = body.ExceptionSectionStart;
        Assert.Equal(0x01, image[section] & 0x3F); // small exception handling table
        image[section + 4 + 4] = 0xFF; // try length of the first clause

        ILReport report = Inspect(image, "Rethrow");

        Assert.Contains(report.Diagnostics, d => d.Id == DiagnosticIds.InvalidExceptionRegion
            && d.Message == "Exception region 0: a protected block or handler extends past the end of the method body.");
    }

    [Fact]
    public void Random_method_body_corruption_never_throws()
    {
        byte[] original = Fixture.Bytes(Fixture.ControlFlow);
        Body[] bodies = [.. MethodBodyInspector.Inspect(original, "fixture.dll").Methods
            .Where(m => m.Name.StartsWith(FlowType + "::", StringComparison.Ordinal))
            .Select(m => Body.Locate(original, m.Name[(FlowType.Length + 2)..m.Name.IndexOfAny(['(', '<'])]))];

        var random = new Random(20260929);
        for (int iteration = 0; iteration < Fuzz.Iterations; iteration++)
        {
            byte[] image = (byte[])original.Clone();
            Body body = bodies[random.Next(bodies.Length)];
            int corruptions = random.Next(1, 6);
            for (int index = 0; index < corruptions; index++)
            {
                // Include the header and exception table as well as the code.
                image[body.HeaderStart + random.Next(body.TotalLength)] = (byte)random.Next(256);
            }

            ILReport report = MethodBodyInspector.Inspect(image, "fuzzed.dll");

            Assert.NotEmpty(ILTextWriter.Write(report));
            Assert.NotEmpty(ILJsonWriter.Write(report));
        }
    }

    private static ILReport Inspect(byte[] image, string method) =>
        MethodBodyInspector.Inspect(image, "patched.dll", name => name.StartsWith(FlowType + "::" + method + "(", StringComparison.Ordinal));

    private static ImmutableArray<InstructionReport> Instructions(string method) =>
        Assert.Single(Inspect(Fixture.Bytes(Fixture.ControlFlow), method).Methods).Instructions;

    /// <summary>File offsets of a method body in a fixture image.</summary>
    private sealed record Body(int HeaderStart, int CodeStart, int CodeSize, int ExceptionSectionStart, int TotalLength)
    {
        public static Body Locate(byte[] image, string method)
        {
            using var pe = new PEReader(ImmutableArray.Create(image));
            MetadataReader metadata = pe.GetMetadataReader();
            MethodDefinition definition = metadata.MethodDefinitions
                .Select(metadata.GetMethodDefinition)
                .Single(m => metadata.GetString(m.Name) == method
                    && metadata.GetString(metadata.GetTypeDefinition(m.GetDeclaringType()).Name) == "Flow");
            int rva = definition.RelativeVirtualAddress;
            SectionHeader section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.VirtualSize);
            int header = rva - section.VirtualAddress + section.PointerToRawData;
            MethodBodyBlock block = pe.GetMethodBody(rva);
            bool tiny = (image[header] & 0x3) == 0x2;
            int codeStart = header + (tiny ? 1 : 12);
            int codeSize = block.GetILReader().Length;
            int exceptions = (codeStart + codeSize + 3) & ~3;
            return new Body(header, codeStart, codeSize, exceptions, block.Size);
        }
    }
}
