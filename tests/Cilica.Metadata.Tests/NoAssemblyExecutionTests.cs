using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Cilica.Metadata.Tests;

/// <summary>
/// Backs up the banned-API analyzer at the binary level: compiler assemblies
/// must not reference APIs that load or execute other assemblies.
/// </summary>
public sealed class NoAssemblyExecutionTests
{
    private static readonly (string Type, string[] Members)[] Banned =
    [
        ("System.Reflection.Assembly", ["Load", "LoadFile", "LoadFrom", "UnsafeLoadFrom"]),
        ("System.AppDomain", ["Load", "ExecuteAssembly"]),
        ("System.Reflection.MethodBase", ["Invoke"]),
        ("System.Runtime.Loader.AssemblyLoadContext", []),
        ("System.Activator", []),
    ];

    [Theory]
    [InlineData("Cilica.Metadata.dll")]
    [InlineData("cilica.dll")]
    public void Compiler_assemblies_do_not_reference_loading_or_invocation_APIs(string assembly)
    {
        using var reader = new PEReader(ImmutableArray.Create(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, assembly))));
        MetadataReader metadata = reader.GetMetadataReader();

        var referencedTypes = metadata.TypeReferences
            .Select(metadata.GetTypeReference)
            .Select(t => metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name))
            .ToHashSet(StringComparer.Ordinal);
        var referencedMembers = metadata.MemberReferences
            .Select(metadata.GetMemberReference)
            .Where(m => m.Parent.Kind == HandleKind.TypeReference)
            .Select(m =>
            {
                TypeReference parent = metadata.GetTypeReference((TypeReferenceHandle)m.Parent);
                return (Type: metadata.GetString(parent.Namespace) + "." + metadata.GetString(parent.Name), Member: metadata.GetString(m.Name));
            })
            .ToList();

        foreach ((string type, string[] members) in Banned)
        {
            if (members.Length == 0)
            {
                Assert.DoesNotContain(type, referencedTypes);
            }
            else
            {
                Assert.DoesNotContain(referencedMembers, r => r.Type == type && members.Contains(r.Member));
            }
        }
    }
}
