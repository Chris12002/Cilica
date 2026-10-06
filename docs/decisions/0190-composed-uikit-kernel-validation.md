# ADR 0190: Composed UIKit kernel validation

Accepted 2026-10-06; milestone 7d.5. Simulator composition checkpoint.

## Context

The real kernel has Swift worker transcript evidence, and the public session
fixture has real UIKit foreground/background evidence. Neither alone proves the
kernel composes with scene admission, retained snapshots and resumed state. The
UIKit test also embedded fixture-specific code directly in its app delegate,
making reuse likely to duplicate lifecycle handling.

## Decision

Separate the test app shell from a MainActor `SceneScenario` with start,
background, resume and finish hooks. The public session implementation stays in
the tests; the private real-kernel scenario uses the existing packaged closure and
the original .NET oracle. Game rules, compiler/runtime sources and native bytes
remain unchanged. This is a validation seam, not a new production command API.

The checker accepts additional Swift templates, substitutes module names and
copies each to a distinct generated filename. Receipts hash generated sources
and linked binaries. The scene runner checks those hashes before simulator work
and requests a rebuild for older receipts. This detects stale/mixed local
artifacts; it does not authenticate an untrusted receipt or package.

Repeat 1..10 explicitly numbered background/foreground cycles per collection mode,
defaulting to three. Each ready, paused and resumed marker identifies its cycle;
an earlier log entry cannot satisfy a later wait. A separate owned cover app
causes actual UIKit transitions. Timeout, assertion/failure output or missing
callbacks fails the run. Unique app IDs, bounded waits/subprocesses and finally
cleanup remain in place. Tests never stop shared simulator services.

## Evidence and review

Both scenario consumers link for arm64 device and simulator with Swift 6 warnings
as errors using existing O2 native packages. On the iOS 26.1 iPhone 17 Pro
simulator, the public fixture passes three cycles per collection mode: session
fields advance exactly once per resume, the original malformed UTF-16 snapshot
stays intact and retained exception type/message plus copied fault identity remain
available while paused. Existing synthetic ordering/failure/cleanup checks remain.

The real kernel passes ten cycles per mode, preserving one session across all ten
transcript steps. Its 1,792 scalar/text comparisons match the unchanged .NET oracle;
260 additional field comparisons confirm state immediately after resume. Background
advance attempts are rejected, retained text copies remain identical after resume,
and leases are released before final disconnect cleanup. Both harness runs remove
their owned apps. The shell/runner are shared with the public fixture.

Three focused Python regressions pass: modified source/binary rejection, old
receipt diagnostics and numbered-marker/failure-log handling. Review covers
lifecycle sequencing, oracle/data separation, marker identity, source/binary
provenance, retained ownership and bounded cleanup. No unresolved defect remains.
Private evidence is `artifacts/milestone7-composed/evidence-7d5.json`.

An initial preflight found that the previously used simulator was shut down; it
installed no apps. The existing device was booted once, without creating a device
or downloading a runtime, and validation then completed. It was left booted;
shared simulator services were not stopped. No physical iOS device was connected.
No signing, hosted work, push, source-kernel change or native rebuild ran.

The device/Android exit gates, application-specific command contract, actual OS
suspension/eviction/restoration and simultaneous-window evidence remain open.
