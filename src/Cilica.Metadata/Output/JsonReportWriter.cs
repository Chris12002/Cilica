using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Cilica.Metadata.Output;

/// <summary>
/// Renders an <see cref="AssemblyReport"/> as JSON. Properties are written in a
/// fixed order and every collection is always present, so the schema is stable.
/// </summary>
public static class JsonReportWriter
{
    public const string Format = "cilica.inspect";
    public const int FormatVersion = 1;

    public static string Write(AssemblyReport report)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            // Output is not embedded in HTML, so readable generic names matter more than HTML-safe escaping.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            json.WriteStartObject();
            json.WriteString("format", Format);
            json.WriteNumber("formatVersion", FormatVersion);
            json.WriteString("input", report.InputName);
            if (report.Identity is null)
            {
                json.WriteNull("assembly");
            }
            else
            {
                json.WritePropertyName("assembly");
                WriteIdentity(json, report.Identity);
            }
            WriteNullable(json, "module", report.ModuleName);
            WriteNullable(json, "targetFramework", report.TargetFramework);
            WriteStrings(json, "imageFlags", report.ImageFlags);
            WriteNullable(json, "entryPoint", report.EntryPoint);
            WriteStrings(json, "attributes", report.Attributes);

            json.WriteStartArray("references");
            foreach (AssemblyIdentityReport reference in report.References)
            {
                WriteIdentity(json, reference);
            }
            json.WriteEndArray();

            json.WriteStartArray("exportedTypes");
            foreach (ExportedTypeReport exported in report.ExportedTypes)
            {
                json.WriteStartObject();
                json.WriteString("name", exported.Name);
                json.WriteString("target", exported.Target);
                json.WriteBoolean("forwarder", exported.IsForwarder);
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartArray("types");
            foreach (TypeReport type in report.Types)
            {
                WriteType(json, type);
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

    private static void WriteType(Utf8JsonWriter json, TypeReport type)
    {
        json.WriteStartObject();
        json.WriteString("name", type.Name);
        json.WriteString("kind", type.Kind);
        json.WriteString("visibility", type.Visibility);
        WriteStrings(json, "flags", type.Flags);
        WriteNullable(json, "layout", type.Layout);
        WriteNullable(json, "baseType", type.BaseType);
        WriteStrings(json, "interfaces", type.Interfaces);

        json.WriteStartArray("genericParameters");
        foreach (GenericParameterReport parameter in type.GenericParameters)
        {
            json.WriteStartObject();
            json.WriteString("name", parameter.Name);
            WriteNullable(json, "variance", parameter.Variance);
            WriteStrings(json, "constraints", parameter.Constraints);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        WriteStrings(json, "attributes", type.Attributes);

        json.WriteStartArray("fields");
        foreach (FieldReport field in type.Fields)
        {
            json.WriteStartObject();
            json.WriteString("name", field.Name);
            json.WriteString("type", field.Type);
            json.WriteString("visibility", field.Visibility);
            WriteStrings(json, "flags", field.Flags);
            WriteNullable(json, "value", field.Value);
            if (field.Offset is int offset)
            {
                json.WriteNumber("offset", offset);
            }
            else
            {
                json.WriteNull("offset");
            }
            WriteStrings(json, "attributes", field.Attributes);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartArray("methods");
        foreach (MethodReport method in type.Methods)
        {
            json.WriteStartObject();
            json.WriteString("name", method.Name);
            json.WriteString("signature", method.Signature);
            json.WriteString("visibility", method.Visibility);
            WriteStrings(json, "flags", method.Flags);
            WriteNullable(json, "import", method.Import);
            json.WriteBoolean("hasBody", method.HasBody);
            WriteStrings(json, "attributes", method.Attributes);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartArray("properties");
        foreach (PropertyReport property in type.Properties)
        {
            json.WriteStartObject();
            json.WriteString("name", property.Name);
            json.WriteString("signature", property.Signature);
            WriteStrings(json, "accessors", property.Accessors);
            WriteStrings(json, "attributes", property.Attributes);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartArray("events");
        foreach (EventReport @event in type.Events)
        {
            json.WriteStartObject();
            json.WriteString("name", @event.Name);
            json.WriteString("type", @event.Type);
            WriteStrings(json, "accessors", @event.Accessors);
            WriteStrings(json, "attributes", @event.Attributes);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        WriteStrings(json, "overrides", type.Overrides);
        json.WriteEndObject();
    }

    private static void WriteIdentity(Utf8JsonWriter json, AssemblyIdentityReport identity)
    {
        json.WriteStartObject();
        json.WriteString("name", identity.Name);
        json.WriteString("version", identity.Version);
        json.WriteString("culture", identity.Culture);
        WriteNullable(json, "publicKeyToken", identity.PublicKeyToken);
        json.WriteEndObject();
    }

    private static void WriteStrings(Utf8JsonWriter json, string name, ImmutableArray<string> values)
    {
        json.WriteStartArray(name);
        foreach (string value in values)
        {
            json.WriteStringValue(value);
        }
        json.WriteEndArray();
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
