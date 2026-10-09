# Approved Albireo Decode trial — 2026-10-09

The approved isolated .NET/Cilica stage **passed** on frozen Cilica
`b8f462870351be165692401d90340c019fadc3ae`. Unity reference, correctness and diagnostic
profile stages are pending in the coordinated Unity lane. This is selected Decode
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

## Handoff and remaining gates

The Unity lane independently verifies the exported DLL/source/licence/oracle and
frozen adapter identities before import. It owns the approved Mac build, generated
source capture, exact transcript comparison and bounded diagnostic profile. No
competitive cross-runtime timing or quiet-host claim is made. User apps remain
untouched.

The candidate reference manifest records the passed .NET/Cilica receipts but keeps
the gap open until actual Unity evidence and review arrive. All previously owned
GC, exception, boxing, generics and dispatch tests remain; no production runtime
or compiler change was necessary for this selected closure. Earlier owned
trimming, character predicates/enumeration and invariant uppercase work supplies
its required support; see batch 018 and its preceding slices.

The failed initial host preparation attempt was a Python tarfile API-version
issue before candidate download/build; its script and diagnostic are retained.
Corrected staging used a fresh directory and validated each archive entry.
The task container, input image and reproducible staging directories were removed;
shared tooling/cache stayed intact. The verified handoff and licence remain outside
Git for the authorized Unity stage; no unique evidence was deleted.
