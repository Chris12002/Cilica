# ADR 0185: Persistent kernel ABI composition

Accepted 2026-10-06; milestone 7c.6. Local macOS ARM64 evidence only.

## Context

The individual v3 features have focused tests, but milestone 6 executed the real
kernel through a private native harness. Before phone packaging, the public C
boundary must demonstrate equivalent behavior across persistent calls, retaining
the object graph and copying observations without exposing managed addresses.

## Decision

Use the existing ABI without expanding its supported shapes. A private scripted
transcript adapter has four exports: open a session, advance one scripted command,
read a 13-field numeric observation and obtain canonical text. The session owns
the kernel, context and current/previous state. The host owns a retained session
lease, receives copied numeric observations and copies returned string leases
into its own UTF-16 storage. Record output capacity is known from the manifest.

This adapter executes the same ten-command probe as milestone 6; it does not
offer a general application command API. Keep its application-specific code and
private dependencies outside compiler/runtime packages. Reuse the hash-pinned
kernel/primitives images and reviewed core-library fixture. An Int32 discovery
root includes all exports in one validated closure; no independent-root discovery
or cross-program merging is implied.

Reference-bearing records are unnecessary for this integration: an opaque session
handle plus numeric/text copies expresses the measured host operations. Such
records remain unsupported, and can be considered when an application contract
requires them. This is a bounded integration checkpoint, not completion of mobile
packaging, a full SDK or all possible structured-value transfers.

Add a public regression for the same ownership pattern without private sources.
It composes imported UTF-16, a retained mutable session with collection storage,
numeric record results, exact wide values, retained immutable snapshots and
captured exceptions inspected after subsequent calls. Independent runtimes and
repeated teardown validate isolation and token invalidation. The test uses the
existing supported invariant integer formatter and two-string concatenation.

## Evidence and review

The new .NET session adapter reproduces every milestone 6 observation and canonical
text, excluding the changed harness entry token. Separate C11 consumers link the
emitted shared library and perform 10,752 exact scalar/UTF-16 comparisons across
O0/O2, normal/forced collection and three runtime recreation cycles per mode.
Short buffers do not advance state. Cloned session leases survive releasing the
original; early canonical snapshots survive later commands. Foreign/released/
destroyed tokens fail; independent sessions remain untouched, and host copies
survive teardown. The closure contains 363 closed methods.

Both public test cases pass with the same O0/O2 sanitizers, collection modes and
three repetitions. Each session performs twelve successful mutations plus an
escaping managed failure, then continues using the same handle. All native builds
use ASan/UBSan, disabled C++ exceptions/RTTI and the unchanged 2 KiB frame gate.
Private evidence and its exact hashes are in
`artifacts/milestone7-kernel/evidence-7c6.json`; no private source is committed.

Review checked pinned image provenance, transcript equivalence, export closure,
host-buffer sizes, preflight/no-side-effect behavior, lease ownership, collection
roots, independent state and cleanup. No production change or new ABI capability
was necessary. The adapter expresses conditional observation calculations in
scalar locals before constructing its record, consistent with the existing
restriction on managed locations across control-flow joins. This does not change
the kernel or conceal a new compatibility claim.

No unresolved defect remains in this bounded checkpoint. A broader full-suite
rerun was unnecessary for test/fixture/documentation changes. Physical-device,
Apple/Android packaging, native host adapter and mobile lifecycle gates remain
open; hosted work remains disabled.
