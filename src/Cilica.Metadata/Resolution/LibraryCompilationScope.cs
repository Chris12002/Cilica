using System;

namespace Cilica.Metadata.Resolution;

public sealed partial class AssemblyResolver
{
    private readonly AssemblyResolver? _librarySource;
    private readonly LibraryBindingPlan? _libraryPlan;

    private AssemblyResolver(AssemblyResolver source, LibraryBindingPlan plan)
    {
        _librarySource = source;
        _libraryPlan = plan;
        _images = source._images;
        _candidates = source._candidates;
    }

    /// <summary>
    /// Creates an explicit compilation view over this strict session's pinned snapshots.
    /// Dispose the view before its source. Disposing a view never disposes source images.
    /// Type/signature/member binding uses the supplied plan; the source remains strict.
    /// No intrinsic or unsafe authority is granted. Inherited-owner member lookup remains unsupported.
    /// </summary>
    public AssemblyResolver CreateLibraryCompilationScope(LibraryBindingPlan plan)
    {
        CheckAlive();
        if (_librarySource is not null) throw new InvalidOperationException("Library compilation scopes cannot be nested.");
        CheckLibraryPlan(plan);
        return new(this, plan);
    }

    public bool IsLibraryCompilationScope => _librarySource is not null;

    private void CheckAlive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_librarySource is not null) ObjectDisposedException.ThrowIf(_librarySource._disposed, _librarySource);
    }

    private void CheckStrictAnalysis()
    {
        if (_librarySource is not null)
            throw new InvalidOperationException("Use AuditLibraryImplementation on this mapped scope, or the strict source session for ordinary contract analysis.");
    }

    private ResolutionResult<ResolvedType> MapLibraryScopeType(ResolvedAssembly input, ResolutionResult<ResolvedType> result)
    {
        try
        {
            CheckLibraryImage(_libraryPlan!, input);
            return result.Value is { } type ? ResolutionResult<ResolvedType>.Resolved(CanonicalLibraryType(_libraryPlan!, type)) : result;
        }
        catch (BindingFailureException failure) { return ResolutionResult<ResolvedType>.Failed(failure.Diagnostics); }
    }
}
