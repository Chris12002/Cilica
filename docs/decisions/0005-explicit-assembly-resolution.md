# ADR 0005: Explicit assembly resolution

Status: accepted for the slice 1c resolver foundation. Recorded 2026-09-29.
This does not establish executable compatibility or the later framework mapping.

## Context

Reachability is only useful when every member belongs to the intended assembly.
Implicit runtime probing, version substitution or choosing the first matching
filename would make audit results depend on the developer's machine. Reference
and implementation assemblies can share an identity while having different
bodies; silently choosing either would undermine the general-filter audit.

## Decision

- `AssemblyResolver.Create` snapshots the immediate `.dll` and `.exe` files in
  explicitly supplied directories. Extensions are matched without case. There
  is no recursive search, dependency restoration, current-directory search or
  installed-runtime fallback. Filesystem links follow normal file-read
  behaviour; the resolver is not a filesystem sandbox.
- Bind by the metadata manifest, not the filename. Assembly names and cultures
  compare ordinally without case; all four version components and public-key
  tokens must match. Empty culture and `neutral` mean the same identity. Types
  retain case-sensitive namespace and nested-name segments, scoped to an image.
- Full public keys in definitions/references are reduced to tokens using the
  CLI identity algorithm. A token is not authentication; it grants no trusted
  library privileges. Retargetable and Windows Runtime identities require a
  future explicit profile and currently produce an unsupported diagnostic.
- Byte-identical copies are interchangeable. Distinct SHA-256 content hashes
  under the same identity produce an ambiguity error when requested. This
  includes reference/implementation pairs. Directory order never selects a
  winner. Exact requested versions can coexist in the catalogue.
- `OpenInput` opens an explicitly named root image but does not add it or its
  parent directory to the reference catalogue. Current-module type references
  resolve within that image. Reopening a path returns its original snapshot;
  create a new session after changing files.
- Unreadable, malformed or unsupported candidates prevent constructing a
  partial catalogue. Use directories containing the intended managed profile,
  rather than mixed build outputs. An expected failure returns diagnostics;
  null arguments, foreign-session images and use after disposal remain caller
  errors. Existing input diagnostics are retained where applicable.
- Resolve type definitions and references, following top-level assembly
  forwarders through facades. Nested types resolve through their outer type
  and then the implementation's nested definitions. Cycles and paths deeper
  than 256 steps are diagnosed without recursive host calls. Duplicate type
  definitions/exports are errors rather than first-match choices.
- TypeSpecs, member/signature binding, nil/module-reference scopes,
  multi-module exports and framework retargeting are outside this block.
  Unsupported resolution requests are explicit failures, not guessed bindings.
- One disposable, single-threaded resolver owns all image readers and caches.
  Public results contain immutable identity, hash, reference-marker and handle
  information; internal metadata readers cannot outlive the resolver. No
  assembly code or attribute constructors execute.
- Reference-assembly attributes are conservatively recorded as markers,
  including markers from unfamiliar assembly scopes. This is not proof of
  authenticity or of implementation availability. An unmarked image is not
  automatically a complete executable implementation.

Diagnostics `CIL3001` through `CIL3008` cover invalid reference directories,
unindexable metadata, missing/conflicting assemblies, missing/conflicting types,
resolution cycles/limits and unsupported resolution operations. Messages use
identities, basenames and hashes, not machine-specific absolute paths.

## Consequences and alternatives

Strict matching is reproducible and exposes profile mistakes early, but it is
not the .NET runtime's general binding policy. ADR 0003's generated mapping
between reference identities and Cilica implementation types remains separate
work. Applications targeting different framework generations may need that
mapping; the resolver must not silently invent it.

Indexing whole explicit directories supports renamed files and exposes damaged
candidates, at the cost of reading unused images. Filename-only probing would
be cheaper but could hide identity conflicts. First-directory-wins and implicit
version roll-forward were rejected because they make an audit's inputs unclear.

## Validation

Independent metadata graphs cover identity components, full-key/token matching,
duplicates/conflicts, explicit-only probing, reference markers, nested/chained
forwarders, cycles and depth limits, missing targets, duplicate definitions and
session ownership. Compiled fixture forwarders exercise real compiler output.
Seeded metadata corruption also exercises catalogue creation and lazy type
indexing. The compiler's banned-API checks remain in force.

The public command remains `inspect`/`il` until the remaining 1c work adds
`audit`. This foundation does not compute reachability or answer whether Alchemy
requires stage B exception handling.
