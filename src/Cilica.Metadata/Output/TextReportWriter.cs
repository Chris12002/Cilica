using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cilica.Metadata.Output;

/// <summary>Renders an <see cref="AssemblyReport"/> as human-readable text with LF line endings.</summary>
public static class TextReportWriter
{
    public static string Write(AssemblyReport report)
    {
        var text = new Writer();
        text.Line($"input: {report.InputName}");
        text.Line($"assembly: {report.Identity?.DisplayName ?? "none"}");
        text.Line($"module: {report.ModuleName ?? "unknown"}");
        text.Line($"target framework: {report.TargetFramework ?? "unknown"}");
        text.Line($"image: {Join(report.ImageFlags, "none")}");
        text.Line($"entry point: {report.EntryPoint ?? "none"}");
        text.Section("attributes", report.Attributes, 0);
        text.Section("references", report.References.Select(r => r.DisplayName), 0, showEmpty: true);
        text.Section(
            "exported types",
            report.ExportedTypes.Select(e => e.Name + (e.IsForwarder ? " -> forwarded to " : " -> ") + e.Target),
            0,
            showEmpty: true);
        text.Line($"types: {report.Types.Length.ToString(CultureInfo.InvariantCulture)}");

        foreach (TypeReport type in report.Types)
        {
            text.Line("");
            WriteType(text, type);
        }

        text.Line("");
        text.Section(
            "diagnostics",
            report.Diagnostics.Select(d => $"{d.Id} {d.Severity.ToDisplayString()}: {d.Message}" + (d.Location is null ? "" : $" [{d.Location}]")),
            0,
            showEmpty: true);
        return text.ToString();
    }

    private static void WriteType(Writer text, TypeReport type)
    {
        text.Line(string.Join(" ", new[] { type.Visibility }.Concat(type.Flags).Append(type.Kind).Append(type.Name)));
        if (type.BaseType is not null)
        {
            text.Line($"  base: {type.BaseType}");
        }
        if (type.Layout is not null)
        {
            text.Line($"  layout: {type.Layout}");
        }
        text.Section("generic parameters", type.GenericParameters.Select(FormatGenericParameter), 1);
        text.Section("interfaces", type.Interfaces, 1);
        text.Section("attributes", type.Attributes, 1);

        if (!type.Fields.IsEmpty)
        {
            text.Line("  fields:");
            foreach (FieldReport field in type.Fields)
            {
                string value = field.Value is null ? "" : " = " + field.Value;
                string offset = field.Offset is int at ? " at offset " + at.ToString(CultureInfo.InvariantCulture) : "";
                text.Line("    " + Words(field.Visibility, field.Flags) + field.Type + " " + field.Name + value + offset);
                text.Items(field.Attributes, 3);
            }
        }

        if (!type.Methods.IsEmpty)
        {
            text.Line("  methods:");
            foreach (MethodReport method in type.Methods)
            {
                string import = method.Import is null ? "" : " import " + method.Import;
                string body = method.HasBody ? "" : " [no body]";
                text.Line("    " + Words(method.Visibility, method.Flags) + method.Signature + import + body);
                text.Items(method.Attributes, 3);
            }
        }

        if (!type.Properties.IsEmpty)
        {
            text.Line("  properties:");
            foreach (PropertyReport property in type.Properties)
            {
                text.Line("    " + property.Signature + " { " + string.Join("; ", property.Accessors) + " }");
                text.Items(property.Attributes, 3);
            }
        }

        if (!type.Events.IsEmpty)
        {
            text.Line("  events:");
            foreach (EventReport @event in type.Events)
            {
                text.Line("    " + @event.Type + " " + @event.Name + " { " + string.Join("; ", @event.Accessors) + " }");
                text.Items(@event.Attributes, 3);
            }
        }

        text.Section("overrides", type.Overrides, 1);
    }

    private static string FormatGenericParameter(GenericParameterReport parameter)
    {
        string variance = parameter.Variance is null ? "" : parameter.Variance + " ";
        string constraints = parameter.Constraints.IsEmpty ? "" : " : " + string.Join(", ", parameter.Constraints);
        return variance + parameter.Name + constraints;
    }

    private static string Words(string visibility, ImmutableArray<string> flags) =>
        string.Join(" ", new[] { visibility }.Concat(flags)) + " ";

    private static string Join(ImmutableArray<string> values, string empty) =>
        values.IsEmpty ? empty : string.Join(", ", values);

    private sealed class Writer
    {
        private readonly StringBuilder _builder = new();

        public void Line(string line) => _builder.Append(line).Append('\n');

        public void Items(IEnumerable<string> items, int indent)
        {
            string prefix = new(' ', indent * 2);
            foreach (string item in items)
            {
                Line(prefix + item);
            }
        }

        public void Section(string title, IEnumerable<string> items, int indent, bool showEmpty = false)
        {
            string prefix = new(' ', indent * 2);
            List<string> list = [.. items];
            if (list.Count == 0)
            {
                if (showEmpty)
                {
                    Line(prefix + title + ": none");
                }
                return;
            }
            Line(prefix + title + ":");
            Items(list, indent + 1);
        }

        public override string ToString() => _builder.ToString();
    }
}
