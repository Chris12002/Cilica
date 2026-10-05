using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cilica.Metadata.Resolution;

namespace Cilica.LibraryBuild;

/// <summary>A build-host request to describe an exact output declaration, never permission to use it.</summary>
public sealed record LibraryIntrinsicRequest([property: JsonRequired] string Namespace, [property: JsonRequired] string Type,
    [property: JsonRequired] string? Member, [property: JsonRequired] int? ParameterCount,
    [property: JsonRequired] LibraryIntrinsicCapability Capability);

public static class LibraryIntrinsicRequests
{
    public static ImmutableArray<LibraryIntrinsicRequest> Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > AssemblyResolver.MaximumLibraryManifestBytes) throw new ArgumentException("Intrinsic requests exceed the byte bound.");
        byte[] bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new IOException("Intrinsic requests grew during reading.");
        using (var document = JsonDocument.Parse(bytes, new() { MaxDepth = 16 }))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Intrinsic requests must be an array.");
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw new JsonException("Intrinsic request must be an object.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in item.EnumerateObject())
                    if (!names.Add(property.Name)) throw new JsonException("Duplicate intrinsic request property.");
            }
        }
        var result = JsonSerializer.Deserialize<ImmutableArray<LibraryIntrinsicRequest>>(bytes, new JsonSerializerOptions {
            MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter<LibraryIntrinsicCapability>(allowIntegerValues: false) }
        });
        Validate(result); return result;
    }

    internal static void Validate(ImmutableArray<LibraryIntrinsicRequest> requests)
    {
        if (requests.IsDefault || requests.Length > 4096) throw new ArgumentException("Supply at most 4096 intrinsic requests.");
        foreach (var request in requests)
            if (request is null || request.Namespace is null || request.Namespace.Length > 1024
                || string.IsNullOrEmpty(request.Type) || request.Type.Length > 1024 || request.Member is { Length: > 1024 }
                || request.Member is "" || (request.Capability == LibraryIntrinsicCapability.StringEmpty
                    ? request.Member is null || request.ParameterCount is not null
                    : (request.Member is null) != (request.ParameterCount is null))
                || request.ParameterCount is < 0 or > 4096 || !Enum.IsDefined(request.Capability))
                throw new ArgumentException("Intrinsic requests need a bounded namespace/type and either a type declaration or a member name and parameter count.");
    }

    internal static ImmutableArray<LibraryIntrinsicGrant> Describe(AssemblyResolver resolver, ResolvedAssembly image,
        ImmutableArray<LibraryIntrinsicRequest> requests)
    {
        var result = ImmutableArray.CreateBuilder<LibraryIntrinsicGrant>();
        var unique = new HashSet<(int, LibraryIntrinsicCapability)>();
        var budget = new MemberResolutionBudget();
        foreach (var request in requests)
        {
            if (!budget.TryConsume(1)) throw new InvalidOperationException("Intrinsic selection exceeds its metadata budget.");
            var owner = resolver.ResolveType(image, request.Namespace, request.Type);
            if (!owner.Success) throw new InvalidOperationException("Intrinsic owner was not emitted: " + request.Namespace + "." + request.Type);
            if (owner.Value!.Assembly != image) throw new InvalidOperationException("Intrinsic owners must be emitted by this build, not forwarded to another image.");
            var described = request.Member is null
                ? resolver.DescribeLibraryIntrinsic(image, owner.Value!.Handle, request.Capability)
                : request.Capability == LibraryIntrinsicCapability.StringEmpty
                ? resolver.DescribeLibraryFieldIntrinsic(owner.Value!, request.Member, request.Capability, budget)
                : resolver.DescribeLibraryMethodIntrinsic(owner.Value!, request.Member, request.ParameterCount!.Value, request.Capability, budget);
            if (!described.Success) throw new InvalidOperationException("Invalid intrinsic request: " + string.Join("; ", described.Diagnostics));
            var grant = described.Value!;
            if (!unique.Add((grant.Token, grant.Capability))) throw new InvalidOperationException("Duplicate intrinsic declaration/capability request.");
            result.Add(grant);
        }
        return result.ToImmutable();
    }
}
