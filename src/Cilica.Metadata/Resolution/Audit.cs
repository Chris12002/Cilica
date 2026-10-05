using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Analysis;
using Cilica.Metadata.Formatting;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    /// <summary>
    /// Audits explicit definition tokens/types, or the public surface. Type names use
    /// metadata spelling (namespace.Type/Nested`arity). No implicit entry-point selection.
    /// Selecting contract images produces an inventory, not implementation-closure evidence.
    /// </summary>
    public AuditReport Audit(ResolvedAssembly assembly, IEnumerable<EntityHandle> roots,
        IEnumerable<string> rootTypes, bool publicSurface = false, ReachabilityLimits? limits = null, ResolvedAssembly? runtimeLibrary = null,
        IEnumerable<ResolvedAssembly>? contractAssemblies = null)
    {
        CheckAlive(); CheckStrictAnalysis();
        return AuditCore(assembly, roots, rootTypes, publicSurface, limits, runtimeLibrary, contractAssemblies, mapped: false);
    }

    /// <summary>Explicit mapped implementation audit. No contract deferral; ordinary audits stay strict.</summary>
    public AuditReport AuditLibraryImplementation(ResolvedAssembly assembly, IEnumerable<EntityHandle> roots,
        ReachabilityLimits? limits = null, ResolvedAssembly? runtimeLibrary = null)
    {
        CheckAlive();
        if (_libraryPlan is null) throw new InvalidOperationException("Implementation audits require an explicit library compilation scope.");
        if (runtimeLibrary is not null) CheckOwner(runtimeLibrary);
        return AuditCore(assembly, roots, [], false, limits, runtimeLibrary, null, mapped: true);
    }

    private AuditReport AuditCore(ResolvedAssembly assembly, IEnumerable<EntityHandle> roots,
        IEnumerable<string> rootTypes, bool publicSurface, ReachabilityLimits? limits, ResolvedAssembly? runtimeLibrary,
        IEnumerable<ResolvedAssembly>? contractAssemblies, bool mapped)
    {
        CheckOwner(assembly);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(rootTypes);
        limits ??= new();
        var selected = roots.Distinct().OrderBy(MetadataTokens.GetToken).ToList();
        string[] types = rootTypes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (publicSurface && (selected.Count != 0 || types.Length != 0))
            throw new ArgumentException("Public-surface and explicit roots cannot be combined.");
        var selection = selected.Select(h => "token:" + Token(h)).Concat(types.Select(t => "type:" + t)).ToList();
        if (publicSurface) selection.Add("public-surface");
        var issues = new List<ReachabilityIssue>();
        var names = new NameFormatter(assembly.Image.Reader);
        if (types.Length != 0)
        {
            var matches = types.ToDictionary(t => t, _ => new List<EntityHandle>(), StringComparer.Ordinal);
            foreach (var handle in assembly.Image.Reader.TypeDefinitions)
            {
                try
                {
                    if (matches.TryGetValue(names.TypeDefinitionName(handle), out var found)) found.Add(handle);
                }
                catch (Exception exception) when (MalformedInput.IsMalformed(exception))
                {
                    issues.Add(new(null, null, new(DiagnosticIds.MalformedEntity, DiagnosticSeverity.Error,
                        exception.Message, Token(handle))));
                }
            }
            foreach (string type in types)
            {
                if (matches[type].Count == 1) selected.Add(matches[type][0]);
                else issues.Add(new(null, null, new(matches[type].Count == 0 ? DiagnosticIds.TypeNotFound : DiagnosticIds.TypeConflict,
                    DiagnosticSeverity.Error, "Root type must match exactly one input TypeDef: " + type)));
            }
        }
        ReachabilityReport graph = mapped ? new ReachabilityWalker(this, limits, runtimeLibrary, []).Run(assembly, selected, publicSurface: false)
            : publicSurface ? AnalyzePublicSurface(assembly, limits, runtimeLibrary, contractAssemblies)
            : AnalyzeReachability(assembly, selected, limits, runtimeLibrary, contractAssemblies);
        issues.AddRange(graph.Issues);
        // Include the complete explicit catalogue, including facades that have no graph
        // nodes. This records the binding environment without retaining filesystem paths.
        var images = _images.Values.DistinctBy(a => (a.Identity, a.ContentHash))
            .OrderBy(a => a.Identity.DisplayName, StringComparer.Ordinal).ThenBy(a => a.ContentHash, StringComparer.Ordinal).ToArray();
        var ids = images.Select((a, id) => (a, id)).ToDictionary(p => (p.a.Identity, p.a.ContentHash), p => p.id);
        int ImageId(ResolvedAssembly image) => ids[(image.Identity, image.ContentHash)];
        var formatter = new AuditNameFormatter(ImageId);
        var nodes = new List<AuditNode>();
        foreach (ReachabilityNode node in graph.Nodes)
        {
            ConstructedType owner = node.Entity switch
            {
                ReachableEntity.Type type => type.Definition,
                ReachableEntity.ArrayReceiver array => array.BaseType,
                ReachableEntity.Method method => method.Definition.DeclaringType,
                ReachableEntity.Field field => field.Definition.DeclaringType,
                _ => throw new InvalidOperationException("Unknown reachable entity."),
            };
            ResolvedAssembly image = owner.Definition.Assembly;
            EntityHandle token = node.Entity switch
            {
                ReachableEntity.Method m => m.Definition.Handle,
                ReachableEntity.Field f => f.Definition.Handle,
                _ => owner.Definition.Handle,
            };
            string name = "<malformed>";
            string instance = "<malformed>";
            try
            {
                name = new NameFormatter(image.Image.Reader).TypeDefinitionName(owner.Definition.Handle);
                instance = formatter.Owner(owner);
                if (node.Entity is ReachableEntity.ArrayReceiver array)
                {
                    name = "array " + formatter.Type(array.Shape);
                    instance = "array=" + formatter.Type(array.Shape) + ";base=" + formatter.Owner(owner);
                }
                else if (node.Entity is ReachableEntity.Method method)
                {
                    name += "::" + image.Image.Reader.GetString(image.Image.Reader.GetMethodDefinition(method.Definition.Handle).Name);
                    instance += formatter.Arguments(method.Definition.MethodArguments) + " " + formatter.Method(method.Definition.Signature);
                }
                else if (node.Entity is ReachableEntity.Field field)
                {
                    name += "::" + image.Image.Reader.GetString(image.Image.Reader.GetFieldDefinition(field.Definition.Handle).Name);
                    instance += " " + formatter.Type(field.Definition.Signature);
                }
            }
            catch (Exception exception) when (MalformedInput.IsMalformed(exception))
            {
                issues.Add(new(node.Id, null, new(DiagnosticIds.MalformedEntity, DiagnosticSeverity.Error, exception.Message, Token(token))));
            }
            bool external = !image.Identity.Equals(assembly.Identity) || image.ContentHash != assembly.ContentHash;
            nodes.Add(new(node.Id, ImageId(image), node.Entity is ReachableEntity.Method ? "method" : node.Entity is ReachableEntity.Field ? "field" : node.Entity is ReachableEntity.ArrayReceiver ? "array-receiver" : "type",
                Token(token), name, instance, node.FirstReason, external && node.Entity is (ReachableEntity.Method or ReachableEntity.Field)));
        }
        return new(assembly.InputName, [.. selection], limits,
            [.. images.Select((a, id) => new AuditAssembly(id, a.Identity.DisplayName, a.ContentHash, a.IsReferenceAssembly,
                a.Identity.Equals(assembly.Identity) && a.ContentHash == assembly.ContentHash))],
            [.. nodes], graph.Roots, graph.Edges, graph.Features, [.. issues])
        {
            ContractAssemblies = [.. graph.ContractAssemblies.Select(ImageId).Order()],
            DeferredNodes = graph.DeferredNodes,
            RuntimeLibrary = runtimeLibrary is null ? null : ImageId(runtimeLibrary),
            RuntimeMappings = [.. graph.RuntimeMappings.Select(m => new AuditRuntimeType(m.Primitive.ToString(), ImageId(m.Definition.Assembly), Token(m.Definition.Handle)))],
            LibraryTypes = !mapped ? [] : [.. _libraryPlan!.Bindings.Select(b => new AuditLibraryType(
                ids[(b.Contract.Assembly, b.Contract.ContentHash)], Token(MetadataTokens.EntityHandle(b.Contract.Token)),
                ids[(b.Implementation.Assembly, b.Implementation.ContentHash)], Token(MetadataTokens.EntityHandle(b.Implementation.Token))))],
            LibraryMembers = [.. graph.LibraryMembers.Select(m => new AuditLibraryMember(
                m.Contract is ReachableEntity.Method ? "method" : "field", ImageId(m.UseAssembly), Token(m.UseToken),
                Declaration(m.Contract), Declaration(m.Implementation)))],
            ApprovedLibraryManifestSha256 = mapped ? ApprovedLibraryManifestSha256 : null,
        };

        AuditMemberDeclaration Declaration(ReachableEntity entity) => entity switch
        {
            ReachableEntity.Method m => new(ImageId(m.Definition.DeclaringType.Definition.Assembly), Token(m.Definition.Handle),
                formatter.Owner(m.Definition.DeclaringType) + formatter.Arguments(m.Definition.MethodArguments) + " " + formatter.Method(m.Definition.Signature)),
            ReachableEntity.Field f => new(ImageId(f.Definition.DeclaringType.Definition.Assembly), Token(f.Definition.Handle),
                formatter.Owner(f.Definition.DeclaringType) + " " + formatter.Type(f.Definition.Signature)),
            _ => throw new InvalidOperationException("Unknown library member projection."),
        };
    }

    private static string Token(EntityHandle handle) => "0x" + MetadataTokens.GetToken(handle).ToString("x8", CultureInfo.InvariantCulture);
}

// Render the already bounded structural signature, never decode an unbounded raw
// signature merely to print a name. Assembly IDs plus tokens retain scope.
internal sealed class AuditNameFormatter(Func<ResolvedAssembly, int> imageId)
{
    internal string Owner(ConstructedType owner) => Named(owner.Definition) + Arguments(owner.Arguments);
    private string Named(ResolvedType type) => "A" + Invariant.Format(imageId(type.Assembly)) + ":0x"
        + MetadataTokens.GetToken(type.Handle).ToString("x8", CultureInfo.InvariantCulture);
    internal string Arguments(SignatureList<TypeSignature> arguments) => "<" + string.Join(",", arguments.Select(Type)) + ">";
    internal string Method(MethodSignature signature) => "header=" + Invariant.Format(signature.Header)
        + ";arity=" + Invariant.Format(signature.GenericArity) + ";" + Type(signature.ReturnType)
        + "(" + string.Join(",", signature.Parameters.Select(Type)) + ")";
    internal string Type(TypeSignature type) => type switch
    {
        TypeSignature.Primitive p => p.Code.ToString(),
        TypeSignature.Named n => n.Kind + " " + Named(n.Definition),
        TypeSignature.Variable v => (v.IsMethod ? "!!" : "!") + Invariant.Format(v.Index),
        TypeSignature.Generic g => Type(g.Definition) + Arguments(g.Arguments),
        TypeSignature.Vector v => Type(v.Element) + "[]",
        TypeSignature.Array a => Type(a.Element) + "[rank=" + Invariant.Format(a.Rank)
            + ";sizes=" + string.Join(",", a.Sizes.Select(Invariant.Format))
            + ";lower=" + string.Join(",", a.LowerBounds.Select(Invariant.Format)) + "]",
        TypeSignature.Pointer p => Type(p.Element) + "*",
        TypeSignature.ByReference r => Type(r.Element) + "&",
        TypeSignature.Pinned p => "pinned " + Type(p.Element),
        TypeSignature.Modified m => (m.Required ? "modreq(" : "modopt(") + Type(m.Modifier) + ")" + Type(m.Element),
        TypeSignature.FunctionPointer f => "fnptr " + Method(f.Signature),
        _ => throw new InvalidOperationException("Unknown signature type."),
    };
}
