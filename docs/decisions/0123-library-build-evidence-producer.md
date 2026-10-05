# ADR 0123: Actual library build evidence

Status: Accepted

## Decision

Produce library build evidence with a separate local build-host executable under
`tools/Cilica.LibraryBuild`. Invoke an explicitly selected Roslyn compiler with
fixed options and explicit copied sources/references. Keep subprocess execution
out of the metadata frontend and IL-to-C++ compiler. Do not execute emitted
assemblies or infer intrinsic permission from a successful compilation.

Capture the effective arguments, source/reference/compiler snapshots, producer
and installed host/runtime hashes, environment and output identity/hash. Pin the
compiler runtime to the producer's installed runtime with roll-forward disabled.
Clear inherited environment so compiler startup hooks and additional probing
settings are not inherited. Verify recorded inputs again after compilation.

Publish into a new directory only after successful compilation and metadata
inspection. Bound input counts/bytes, arguments, process duration and output;
clean owned staging on failure. Existing output directories are never replaced.
The process assumes a trusted host/compiler and does not claim hermetic isolation.

Emit the existing version-1 admission schema with no intrinsic grants. Its digest
is review evidence, not self-approval. An independent host policy still controls
trusted admission. Source/runtime licensing and compatibility are separate from
the fact that an invocation produced an image.

## Evidence and review

The formatter builds through this tool with 201 captured inputs and zero grants.
Tests inspect every recorded input hash, verify actual output metadata, simulate
independent approval for an authored fixture, reject a wrong digest, and obtain
identical output bytes across separate builds. Invalid source, excessive compiler
output, option injection and existing-output preservation exercise failure paths.
All 39 distinct producer/admission checks pass. Review retained the explicit
host-trust boundary and corrected direct compiler options; no remaining actionable
issue was found. This does not complete framework mapping or kernel execution.
