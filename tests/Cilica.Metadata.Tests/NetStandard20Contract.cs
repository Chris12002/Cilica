using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using Cilica.Metadata.Resolution;
using Xunit;

namespace Cilica.Metadata.Tests;

/// <summary>Explicit metadata contract selection; never an assembly identity redirect.</summary>
internal static class NetStandard20Contract
{
    internal static string ReferencePath => Path.Combine(AppContext.BaseDirectory, "ReferenceContracts", "netstandard2.0", "netstandard.dll");
    internal static readonly AssemblyIdentity Identity = new("netstandard", new Version(2, 0, 0, 0), "neutral", "cc7b13ffcd2ddd51");
    // Official NETStandard.Library 2.0.3 build/netstandard2.0/ref/netstandard.dll.
    internal const string ContentHash = "1799854d01a89d141fecc4c4548b01e51fa57118c9b18eac66a752f3bbe98c02";

    internal static LibraryBindingPlan Include(AssemblyResolver resolver, LibraryBindingPlan existing, ResolvedAssembly implementation)
    {
        var contract = Require(resolver.OpenInput(ReferencePath));
        Assert.Equal(Identity, contract.Identity);
        Assert.Equal(ContentHash, contract.ContentHash);
        Assert.True(contract.IsReferenceAssembly);
        var mappings = new List<LibraryTypeMapping>();
        foreach (var binding in existing.Bindings)
        {
            var originalImage = Require(resolver.ResolveAssembly(binding.Contract.Assembly));
            mappings.Add(new(Require(resolver.ResolveType(originalImage, MetadataTokens.EntityHandle(binding.Contract.Token))),
                Require(resolver.ResolveType(implementation, MetadataTokens.EntityHandle(binding.Implementation.Token)))));
            // HashCode and ITuple are absent from this pinned 2.0 contract. This is not a blanket ignore
            // for unresolved declarations, forwarders or other identity failures.
            if ((binding.Namespace == "System" && binding.Names.SequenceEqual(new[] { "HashCode" }))
                || (binding.Namespace == "System.Runtime.CompilerServices" && binding.Names.SequenceEqual(new[] { "ITuple" })))
            {
                var absent = resolver.ResolveType(contract, binding.Namespace, binding.Names.ToArray());
                Assert.False(absent.Success);
                Assert.Equal(DiagnosticIds.TypeNotFound, Assert.Single(absent.Diagnostics).Id);
                continue;
            }
            // The pinned 2.0 IOrderedEnumerable<T> is invariant; the owned 2.1
            // declaration is covariant. Preserve the exact-header rejection.
            if (binding.Namespace == "System.Linq" && binding.Names.SequenceEqual(new[] { "IOrderedEnumerable`1" })) continue;
            var source = Require(resolver.ResolveType(contract, binding.Namespace, binding.Names.ToArray()));
            var target = Require(resolver.ResolveType(implementation, MetadataTokens.EntityHandle(binding.Implementation.Token)));
            mappings.Add(new(source, target));
        }
        return Require(resolver.CreateLibraryBindingPlan(mappings));
    }

    private static T Require<T>(ResolutionResult<T> result) where T : class
    {
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }
}
