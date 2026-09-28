using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cilica.Metadata.IL;

namespace Cilica.Metadata.Output;

/// <summary>Renders an <see cref="ILReport"/> as JSON with a fixed property order.</summary>
public static class ILJsonWriter
{
    public const string Format = "cilica.il";
    public const int FormatVersion = 1;

    public static string Write(ILReport report)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            json.WriteStartObject();
            json.WriteString("format", Format);
            json.WriteNumber("formatVersion", FormatVersion);
            json.WriteString("input", report.InputName);

            json.WriteStartArray("methods");
            foreach (MethodBodyReport method in report.Methods)
            {
                json.WriteStartObject();
                json.WriteString("name", method.Name);
                json.WriteNumber("codeSize", method.CodeSize);
                json.WriteNumber("maxStack", method.MaxStack);
                json.WriteBoolean("localsInitialized", method.LocalsInitialized);
                json.WriteStartArray("locals");
                foreach (string local in method.Locals)
                {
                    json.WriteStringValue(local);
                }
                json.WriteEndArray();

                json.WriteStartArray("exceptionRegions");
                foreach (ExceptionRegionReport region in method.Regions)
                {
                    json.WriteStartObject();
                    json.WriteString("kind", region.Kind);
                    json.WriteNumber("tryStart", region.TryStart);
                    json.WriteNumber("tryEnd", region.TryEnd);
                    json.WriteNumber("handlerStart", region.HandlerStart);
                    json.WriteNumber("handlerEnd", region.HandlerEnd);
                    if (region.FilterStart is int filter)
                    {
                        json.WriteNumber("filterStart", filter);
                    }
                    else
                    {
                        json.WriteNull("filterStart");
                    }
                    WriteNullable(json, "catchType", region.CatchType);
                    WriteNullable(json, "filterKind", region.FilterKind);
                    json.WriteEndObject();
                }
                json.WriteEndArray();

                json.WriteStartArray("blocks");
                foreach (BasicBlockReport block in method.Blocks)
                {
                    json.WriteStartObject();
                    json.WriteNumber("index", block.Index);
                    json.WriteNumber("start", block.Start);
                    json.WriteNumber("end", block.End);
                    if (block.StackOnEntry is int depth)
                    {
                        json.WriteNumber("stackOnEntry", depth);
                    }
                    else
                    {
                        json.WriteNull("stackOnEntry");
                    }
                    json.WriteStartArray("successors");
                    foreach (int successor in block.Successors)
                    {
                        json.WriteNumberValue(successor);
                    }
                    json.WriteEndArray();
                    json.WriteEndObject();
                }
                json.WriteEndArray();

                json.WriteStartArray("instructions");
                foreach (InstructionReport instruction in method.Instructions)
                {
                    json.WriteStartObject();
                    json.WriteNumber("offset", instruction.Offset);
                    json.WriteString("opcode", instruction.OpCode);
                    WriteNullable(json, "operand", instruction.Operand);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("diagnostics");
            foreach (Diagnostic diagnostic in report.Diagnostics)
            {
                json.WriteStartObject();
                json.WriteString("id", diagnostic.Id);
                json.WriteString("severity", diagnostic.Severity.ToDisplayString());
                json.WriteString("message", diagnostic.Message);
                WriteNullable(json, "location", diagnostic.Location);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteNullable(Utf8JsonWriter json, string name, string? value)
    {
        if (value is null)
        {
            json.WriteNull(name);
        }
        else
        {
            json.WriteString(name, value);
        }
    }
}
