using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;

namespace Cilica.Metadata.Resolution;

/// <summary>
/// Binding identity, independent of filenames and display formatting. Name and culture
/// compare ordinally without case; all four version components and the key token must match.
/// A public-key token identifies a key; it does not authenticate or grant trust to an image.
/// </summary>
public sealed class AssemblyIdentity : IEquatable<AssemblyIdentity>
{
    public AssemblyIdentity(string name, Version version, string culture = "neutral", string? publicKeyToken = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(culture);
        if (version.Build < 0 || version.Revision < 0)
            throw new ArgumentException("Assembly versions require four components.", nameof(version));
        if (publicKeyToken is not null && (publicKeyToken.Length != 16 || !IsHex(publicKeyToken)))
            throw new ArgumentException("A public-key token must contain 16 hexadecimal digits.", nameof(publicKeyToken));
        Name = name;
        Version = version;
        Culture = culture.Length == 0 ? "neutral" : culture;
        PublicKeyToken = publicKeyToken?.ToLowerInvariant();
    }

    public string Name { get; }
    public Version Version { get; }
    public string Culture { get; }
    public string? PublicKeyToken { get; }
    public string DisplayName => $"{Name}, Version={Version}, Culture={Culture}, PublicKeyToken={PublicKeyToken ?? "null"}";

    public bool Equals(AssemblyIdentity? other) => other is not null
        && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name)
        && Version.Equals(other.Version)
        && StringComparer.OrdinalIgnoreCase.Equals(Culture, other.Culture)
        && PublicKeyToken == other.PublicKeyToken;

    public override bool Equals(object? obj) => obj is AssemblyIdentity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name),
        Version, StringComparer.OrdinalIgnoreCase.GetHashCode(Culture), PublicKeyToken);
    public override string ToString() => DisplayName;

    internal static AssemblyIdentity ReadDefinition(MetadataReader reader)
    {
        AssemblyDefinition definition = reader.GetAssemblyDefinition();
        ValidateFlags(definition.Flags);
        return Read(reader, definition.Name, definition.Version, definition.Culture, definition.PublicKey, fullKey: true);
    }

    internal static AssemblyIdentity ReadReference(MetadataReader reader, AssemblyReferenceHandle handle)
    {
        AssemblyReference reference = reader.GetAssemblyReference(handle);
        ValidateFlags(reference.Flags);
        return Read(reader, reference.Name, reference.Version, reference.Culture, reference.PublicKeyOrToken,
            fullKey: (reference.Flags & AssemblyFlags.PublicKey) != 0);
    }

    private static void ValidateFlags(AssemblyFlags flags)
    {
        // Retargeting and Windows Runtime binding require a separate explicit profile.
        if ((flags & (AssemblyFlags.Retargetable | AssemblyFlags.WindowsRuntime)) != 0)
            throw new UnsupportedBindingException("Retargetable and Windows Runtime identities are outside the strict binding profile.");
    }

    private static AssemblyIdentity Read(MetadataReader reader, StringHandle name, Version version,
        StringHandle culture, BlobHandle key, bool fullKey)
    {
        string assemblyName = reader.GetString(name);
        if (assemblyName.Length == 0) throw new BadImageFormatException("Assembly identity has an empty name.");
        byte[] bytes = reader.GetBlobBytes(key);
        string? token = null;
        if (bytes.Length > 0)
        {
            if (fullKey)
            {
                // ECMA-335 II.6.2.1.3: reverse the last eight bytes of the public key's SHA-1 hash.
#pragma warning disable CA5350 // Required identity format, not a security or trust decision.
                bytes = SHA1.HashData(bytes)[^8..];
#pragma warning restore CA5350
                Array.Reverse(bytes);
            }
            else if (bytes.Length != 8)
            {
                throw new BadImageFormatException("Assembly reference public-key token must contain eight bytes.");
            }
            token = Convert.ToHexStringLower(bytes);
        }
        return new AssemblyIdentity(assemblyName, version, reader.GetString(culture), token);
    }

    private static bool IsHex(string text)
    {
        foreach (char character in text)
            if (!Uri.IsHexDigit(character)) return false;
        return true;
    }
}

internal sealed class UnsupportedBindingException(string message) : Exception(message);
