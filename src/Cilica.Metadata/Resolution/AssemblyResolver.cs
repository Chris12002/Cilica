using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Cilica.Metadata.Resolution;

/// <summary>
/// A disposable snapshot of explicitly supplied reference directories. Never probes the
/// input directory, current directory or installed runtime implicitly, and never runs input.
/// Single-threaded: readers and caches belong to this session and are disposed together.
/// </summary>
public sealed partial class AssemblyResolver : IDisposable
{
    private readonly Dictionary<string, ResolvedAssembly> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<AssemblyIdentity, List<ResolvedAssembly>> _candidates = [];
    private bool _disposed;

    private AssemblyResolver() { }

    /// <summary>
    /// Indexes the immediate .dll/.exe files in each supplied directory by manifest identity,
    /// not filename. An unreadable or invalid candidate prevents creating a partial catalogue.
    /// The caller owns a successful result and must dispose it. No framework redirects apply.
    /// </summary>
    public static ResolutionResult<AssemblyResolver> Create(IEnumerable<string> referenceDirectories)
    {
        ArgumentNullException.ThrowIfNull(referenceDirectories);
        var resolver = new AssemblyResolver();
        bool success = false;
        try
        {
            var failures = new List<Diagnostic>();
            var directories = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string directory in referenceDirectories)
            {
                ArgumentNullException.ThrowIfNull(directory);
                try { directories.Add(Path.GetFullPath(directory)); }
                catch (Exception exception) when (IsFileFailure(exception))
                {
                    failures.Add(new Diagnostic(DiagnosticIds.ReferenceDirectoryInvalid, DiagnosticSeverity.Error,
                        "An explicit reference directory has an invalid path."));
                }
            }
            foreach (string directory in directories)
            {
                string[] files;
                try
                {
                    files = Directory.EnumerateFiles(directory)
                        .Where(p => Path.GetExtension(p).Equals(".dll", StringComparison.OrdinalIgnoreCase)
                            || Path.GetExtension(p).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        .Order(StringComparer.Ordinal).ToArray();
                }
                catch (Exception exception) when (IsFileFailure(exception))
                {
                    failures.Add(new Diagnostic(DiagnosticIds.ReferenceDirectoryInvalid, DiagnosticSeverity.Error,
                        $"Cannot enumerate reference directory '{Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))}'; verify it exists and is readable."));
                    continue;
                }
                foreach (string path in files)
                {
                    ResolutionResult<ResolvedAssembly> loaded = resolver.OpenInput(path);
                    if (loaded.Value is not ResolvedAssembly image)
                    {
                        failures.AddRange(loaded.Diagnostics);
                        continue;
                    }
                    if (!resolver._candidates.TryGetValue(image.Identity, out var candidates))
                        resolver._candidates.Add(image.Identity, candidates = []);
                    if (!candidates.Contains(image)) candidates.Add(image);
                }
            }
            if (failures.Count > 0)
                return ResolutionResult<AssemblyResolver>.Failed(failures
                    .OrderBy(d => d.Location, StringComparer.Ordinal).ThenBy(d => d.Id, StringComparer.Ordinal)
                    .ThenBy(d => d.Message, StringComparer.Ordinal));
            success = true;
            return ResolutionResult<AssemblyResolver>.Resolved(resolver);
        }
        finally
        {
            if (!success) resolver.Dispose();
        }
    }

    /// <summary>
    /// Opens an explicit input as data. It does not add the input or its directory to the
    /// reference catalogue. The session owns the image, including when it is also a reference.
    /// Reopening a path returns the original snapshot; create a new session to see file changes.
    /// </summary>
    public ResolutionResult<ResolvedAssembly> OpenInput(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            return ResolutionResult<ResolvedAssembly>.Failed(DiagnosticIds.ReferenceImageInvalid, "An explicit assembly path is invalid.");
        }
        if (_images.TryGetValue(fullPath, out ResolvedAssembly? existing))
            return ResolutionResult<ResolvedAssembly>.Resolved(existing);
        byte[]? bytes = AssemblyImage.ReadFile(fullPath, out Diagnostic? failure);
        if (bytes is null) return ResolutionResult<ResolvedAssembly>.Failed([failure!]);
        AssemblyImage? image = AssemblyImage.Open(bytes, Path.GetFileName(fullPath), out failure);
        if (image is null) return ResolutionResult<ResolvedAssembly>.Failed([failure!]);
        bool owned = false;
        try
        {
            if (!image.Reader.IsAssembly)
                return ResolutionResult<ResolvedAssembly>.Failed(DiagnosticIds.ResolutionUnsupported,
                    "Multi-module assemblies are outside the resolver profile.", image.InputName);
            var resolved = new ResolvedAssembly(image, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            _images.Add(fullPath, resolved);
            owned = true;
            return ResolutionResult<ResolvedAssembly>.Resolved(resolved);
        }
        catch (Exception exception) when (MalformedInput.IsMalformed(exception) || exception is UnsupportedBindingException)
        {
            return ResolutionResult<ResolvedAssembly>.Failed(
                exception is UnsupportedBindingException ? DiagnosticIds.ResolutionUnsupported : DiagnosticIds.ReferenceImageInvalid,
                $"Cannot index assembly metadata: {exception.Message}", image.InputName);
        }
        finally
        {
            if (!owned) image.Dispose();
        }
    }

    /// <summary>
    /// Matches all identity components. Byte-identical duplicates are interchangeable;
    /// different contents with the same identity are an error, including reference/implementation pairs.
    /// </summary>
    public ResolutionResult<ResolvedAssembly> ResolveAssembly(AssemblyIdentity identity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(identity);
        if (!_candidates.TryGetValue(identity, out var matches))
        {
            string[] alternatives = _candidates.Keys.Where(k => StringComparer.OrdinalIgnoreCase.Equals(k.Name, identity.Name))
                .Select(k => k.DisplayName).Order(StringComparer.Ordinal).ToArray();
            return ResolutionResult<ResolvedAssembly>.Failed(DiagnosticIds.AssemblyNotFound,
                $"Cannot resolve '{identity.DisplayName}' from the explicit reference directories. "
                + (alternatives.Length == 0 ? "Supply the directory containing that identity."
                    : "Available identities: " + string.Join("; ", alternatives) + ". No version or key substitution is applied."));
        }
        string[] hashes = matches.Select(m => m.ContentHash).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (hashes.Length != 1)
            return ResolutionResult<ResolvedAssembly>.Failed(DiagnosticIds.AssemblyConflict,
                $"Conflicting images for '{identity.DisplayName}' (SHA-256: {string.Join(", ", hashes)}). "
                + "Supply one implementation/reference profile; directory order does not select a winner.");
        return ResolutionResult<ResolvedAssembly>.Resolved(matches.OrderBy(m => m.InputName, StringComparer.Ordinal).First());
    }

    private void CheckOwner(ResolvedAssembly assembly)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(assembly);
        if (!_images.Values.Contains(assembly))
            throw new ArgumentException("The image belongs to a different resolver session.", nameof(assembly));
    }

    private static bool IsFileFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (ResolvedAssembly image in _images.Values) image.Image.Dispose();
        _images.Clear();
        _candidates.Clear();
    }
}
