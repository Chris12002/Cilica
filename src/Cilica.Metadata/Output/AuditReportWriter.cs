using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Output;

public static class AuditReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Json(AuditReport report) => JsonSerializer.Serialize(new
    {
        format = "cilica.audit",
        formatVersion = 1,
        profile = AuditReport.Profile,
        scope = AuditReport.Scope,
        report.Input,
        report.Selection,
        report.Status,
        report.GeneralFilterStatus,
        report.Limits,
        report.Assemblies,
        report.Nodes,
        report.Roots,
        report.Edges,
        report.Features,
        report.Issues,
    }, JsonOptions) + "\n";

    public static string Text(AuditReport report)
    {
        var text = new StringBuilder();
        text.Append("cilica.audit v1: ").Append(Q(report.Input)).Append('\n')
            .Append("profile: ").Append(AuditReport.Profile).Append('\n')
            .Append("scope: ").Append(AuditReport.Scope).Append('\n')
            .Append("status: ").Append(report.Status).Append('\n')
            .Append("general filters: ").Append(report.GeneralFilterStatus).Append('\n')
            .Append("selection: ").AppendJoin(", ", System.Linq.Enumerable.Select(report.Selection, Q)).Append('\n')
            .Append("limits: ").Append(JsonSerializer.Serialize(report.Limits, JsonOptions)).Append('\n');
        foreach (AuditAssembly assembly in report.Assemblies)
            text.Append("assembly A").Append(N(assembly.Id)).Append(' ').Append(Q(assembly.Identity))
                .Append(" sha256=").Append(assembly.ContentHash).Append(assembly.IsReferenceAssembly ? " reference" : " implementation-image")
                .Append(assembly.IsInput ? " input" : " catalogue").Append('\n');
        foreach (AuditNode node in report.Nodes)
        {
            text.Append("node ").Append(N(node.Id)).Append(" A").Append(N(node.Assembly)).Append(' ').Append(node.Kind)
                .Append(' ').Append(node.Token).Append(' ').Append(Q(node.Name)).Append(' ').Append(Q(node.Instantiation))
                .Append(node.IsExternalMember ? " external-member" : "");
            if (node.FirstReason is int reason) text.Append(" via edge ").Append(N(reason));
            else text.Append(" root");
            text.Append('\n');
        }
        for (int i = 0; i < report.Edges.Length; i++)
        {
            ReachabilityEdge edge = report.Edges[i];
            text.Append("edge ").Append(N(i)).Append(": ").Append(N(edge.From)).Append(" -> ").Append(N(edge.To))
                .Append(' ').Append(edge.Kind).Append(Offset(edge.ILOffset));
            if (edge.ReceiverType is int receiver) text.Append(" receiver=").Append(N(receiver));
            text.Append('\n');
        }
        foreach (FeatureObservation feature in report.Features)
            text.Append("feature ").Append(feature.Feature).Append(" node=").Append(N(feature.Node))
                .Append(Offset(feature.ILOffset)).Append(' ').Append(Q(feature.Evidence)).Append('\n');
        foreach (ReachabilityIssue issue in report.Issues)
            text.Append(issue.Diagnostic.Id).Append(' ').Append(issue.Diagnostic.Severity.ToDisplayString())
                .Append(": ").Append(Q(issue.Diagnostic.Message))
                .Append(issue.Node is int node ? " node=" + N(node) : "")
                .Append(Offset(issue.ILOffset))
                .Append(issue.Diagnostic.Location is string location ? " at " + Q(location) : "").Append('\n');
        return text.ToString();
    }

    private static string Q(string value) => Invariant.Quote(value, '"');
    private static string N(int value) => Invariant.Format(value);
    private static string Offset(int? offset) => offset is int value ? " IL_" + value.ToString("x4", System.Globalization.CultureInfo.InvariantCulture) : "";
}
