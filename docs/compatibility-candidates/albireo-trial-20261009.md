# Approved Albireo Decode trial — 2026-10-09

The approved isolated .NET/Cilica stage **passed** on frozen Cilica
`b8f462870351be165692401d90340c019fadc3ae`. Unity capture and diagnostic profiling also completed, but exact player equality
failed at two cases. A separately reviewed semantic-reference exception accepts source/diagnostic use
while preserving failed Unity equality and the passed .NET/Cilica result. This is selected Decode
coverage; the whole library and Encode are not claimed compatible.

## Inputs and approval

The unchanged `kappa7194/base32` source is pinned to
`1eabd2fcba515d0bd63881fd2a2879555c43648b`. Source/licence Git blobs, all five owned
recipe inputs and all 24 existing cached packages were verified before building.
Only the approved source and MIT licence were downloaded. No upstream project,
test, generator, analyzer or build script was used. The whole Base32.cs file was
compiled, including Encode, but execution roots are Decode and the unchanged
owned transcript probe. The actual imported DLL pin is:

```text
a7fefd9010e223e7e58809c7c34a946e3fb5d7294522ab3cc2ff20d2352d4760  Albireo.SelectedDecode.dll
```

[Human approval](albireo-trial-20261009/approval.txt) explicitly covers both the
isolated trial and the normal-account Mac Unity stage for this candidate only.
The [boundaries](albireo-trial-20261009/execution-boundaries.json) preserve their
distinction: Unity is not claimed to have container isolation. The common workload
fingerprint, including exact candidate DLL, cases and owned adapter inputs, is
`d79e8c4d539d8f1e486b50360ac2f6ec3196d1a851da29e20ec61e08554a87eb`.

## Isolated results

Normal .NET 10.0.12 produced **851 exact observations across all 32 scenarios**.
Cilica O0/O2 passed every observation three times: **5,106 repeated comparisons**,
plus 1,702 exact-budget successes and 1,702 one-short frame-budget checks. Full
bytes, exception status/message/parameter UTF-16 units and result identity were
preserved. No digest substituted for an observation.

Every native allocation forced a collection. ASan/UBSan, context/root/borrow
cleanup, stable post-collection survivors, and strict 2,048-byte native frame
warnings were enabled. All **14 native source/executable hash pairs** were retained.
The selected explicit test passed; this is not a full-suite result. One existing
CS0108 fixture warning remained, with no build errors.

The runtime recipe finished in **153.53 seconds**; its host supervisor exported
evidence and removed the container after about **158 seconds**. Inspected isolation
was nonroot, network-none, read-only root, no host mounts/credentials, capabilities
dropped, privilege escalation disabled, 2 CPUs, 8 GiB and 256 PIDs, under the
30-minute watchdog. No candidate code ran on the host in this stage.

- [Isolated result and exact commands](albireo-trial-20261009/isolated/result.json)
- [Full differential log](albireo-trial-20261009/isolated/differential.log)
- [Complete oracle triples](albireo-trial-20261009/isolated/oracle-observations.json)
- [Inspected container](albireo-trial-20261009/container-inspect.json)
- [Input provenance](albireo-trial-20261009/input-provenance.json)
- [Summary](albireo-trial-20261009/isolated-summary.json)

## Unity results and generated-code findings

Unity 6000.3.2f1 built the exact DLL with the frozen owned adapter in 43.37 seconds.
The final package lock contains only the approved built-in JSON module. Project
creation initially expanded the package manifest; a guard stopped before the
IL2CPP build and restored the frozen manifest. The attempt/state and correction
are retained. No upstream scripts, extra package installation or candidate rewrite.

The full comparison **failed exactly two cases**; the other **30 cases match**:

| Case | .NET / Cilica | Unity IL2CPP |
| --- | --- | --- |
| 0, null input | `Value cannot be null. (Parameter 'input')` | `Value cannot be null.\nParameter name: input` |
| 17, `ſA` (U+017F + A) | Success, byte 144 | ArgumentException, message `input` |

Both runtimes retain the same null exception type/parameter. The generated Unity
ArgumentException formatter inserts a newline. Its invariant casing branch uses
a table whose exact FieldRVA entry159 leaves U+017F unchanged; metadata-only
inspection confirms the bytes. Cilica's existing pinned invariant mapping converts
U+017F to S. No normalization or production change was applied to conceal either
mismatch. These are same-input runtime-library differences, not mismatched DLLs.

The retained capture contains **119 C++ files**, 337 files overall (114,031,578
bytes), with exact symbols and 14 reviewed source/hash/line locations. Useful
structural findings include delegate-cache publication, checked byte arithmetic,
null/fault order and array enumeration. Unity boxes its nonempty char-array
enumerator in this Any path; Cilica's owned array path directly indexes live
elements. Such source differences are guidance, not measured speed differences.
Cilica keeps its ownership/root/frame contracts and .NET semantics.

The diagnostic profile contains **three verified markers**, each covering 128
iterations over four selected cases (512 wrapper invocations / 1,024 Decode calls).
Every selected-case result matches the oracle. Each marker records 6,144 allocation
samples and 1,078,016 allocated bytes; this is cumulative allocation, not live heap,
RSS or a memory peak. Marker times 4.721/4.389/4.469 ms are Development-profile
observations only. Source construction and owned observation logic are included;
no pure-Decode cost, quiet-host measurement or competitive ratio is claimed.
The 901,042-byte raw capture, marker inspection and warning scan are retained.

**Architecture correction:** the frozen architecture value produced a universal
ARM64+x86-64 app and GameAssembly, not the plan's ARM64-only label. Both compiler
branches are retained. API readbacks confirm Release/OptimizeSpeed, Minimal
stripping and incremental GC; the process architecture was not independently
recorded. No architecture-specific profiling claim or rebuild.

- [Exact differences](albireo-trial-20261009/unity/differences.json)
- [Source findings](albireo-trial-20261009/unity/source-inspection.json)
- [Three raw profile markers](albireo-trial-20261009/unity/markers.json)
- [Actual architecture/compiler commands](albireo-trial-20261009/unity/actual-architecture-and-commands.json)
- [Final independent review](albireo-trial-20261009/final-review.json)

## Qualified completion and retention

The bounded selected Decode trial is **verified against .NET** at the frozen
compiler pin. The [explicit semantic-reference decision](albireo-trial-20261009/semantic-reference-review.json)
permits the same-input Unity capture as source guidance and diagnostic profiling,
while Unity correctness stays **failed**. This follows the parent's explicit
reference-policy direction; no cases, oracle bytes or failure labels changed.
The prior Unity lane's blocked-equivalence decision is retained unchanged as
historical evidence. The completion gate now distinguishes .NET conformance from
Unity comparability and forbids comparative performance under this exception.

No new production feature or runtime change was needed. Existing handcrafted GC,
exception, boxing, generic and dispatch tests remain. No full-suite, Encode,
other-library or latest-compiler execution claim is made. No observed Cilica
implementation gap remains in these 32 approved Decode cases. The parent selects
any follow-up batch; this result grants no further candidate execution.

The initial host staging failure was a Python tarfile API-version issue before
candidate download/build, corrected in a new validated staging directory. The
isolated task container/input image/recoverable staging were removed. Shared
caches/tooling stayed intact. Compact results, the raw profile and minimal source
excerpts are retained here; the larger capture, app and failed partial attempts
remain at the [recorded external locations](albireo-trial-20261009/unity/external-evidence.json).
No task Editor/player/container remains active, and user apps were not stopped.
