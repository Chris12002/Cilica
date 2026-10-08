# DiffPlex retry after confined array-length truth support

**Confined `ldlen` truth branches now work. The unchanged candidate reaches a
new scalar-array element receiver rejection during translation. Zero of the
64 candidate oracle/native scenarios have run.**

Implementation commit: `e5faa49d1ce77736183f0c958dac083111272efc`, integer CIL v90.
The [design decision](../decisions/compatibility-010-array-length-truth.md) records
same-block adjacency, native-width zero/nonzero semantics, the preserved null
fault and rejection boundaries. It reuses existing IR/runtime operations without
new grants, layouts or resource limits.

## Validation

- **20 new checks pass**, covering four C# roots, all four raw truth-branch forms,
  older-profile rejection and ten native-length escape/separation cases.
- Eight semantic roots supply **2,352 repeated O0/O2 native/.NET comparisons**,
  including short-circuit/null/finally order and multiple array element kinds.
- **81 adjacent checks pass**, including arrays, checked indices, earlier contracts,
  default core and handcrafted GC, exceptions, boxing, generics and dispatch.
- **25/25 focused checks pass in the offline Linux container** on the exact exported
  commit: the 20 length tests plus five earlier-contract tests. Native checks use
  Clang 18, forced GC and ASan/UBSan. Host checks use .NET 10.0.11; guest uses 10.0.12.
  No actual 32-bit ABI execution is claimed.
- **Two additional independent diagnostic checks pass** against an e5faa49 export
  with only their authored source added. They reproduce the next scalar-array
  receiver gap; one also records receiver-after-argument mutation semantics.
- **One explicit candidate test fails** at the diagnostic below. No candidate
  C++ emission or semantic comparison was reached; full upstream tests remain unrun.

[New feature checks](diffplex-ldlen-20261008/tests.log),
[adjacent checks](diffplex-ldlen-20261008/regressions.log),
[offline focused checks](diffplex-ldlen-20261008/focused-regressions.log), and
[new gap reductions](diffplex-ldlen-20261008/reduction-tests.log) retain actual results.
The explicit candidate driver now logs its stage and completed oracle/native
observations, without changing the source recipe, adapter or comparison logic.

## Actual candidate progress

| Trial baseline | First blocking diagnostic | Candidate scenarios |
| --- | --- | --- |
| `268024e` | CIL3003: exact netstandard 2.0 reference absent | 0/64 |
| `ae04d32` | CIL2001: ldlen requires adjacent conv.i4 | 0/64 |
| `e5faa49` | CIL2001: scalar-array element address unsupported | 0/64 |

The original [contract failure](diffplex-execution-20261008.md) and subsequent
[length failure](diffplex-retry-20261008.md) remain preserved. The
[intervening commits](diffplex-ldlen-20261008/baseline-changes.txt) identify the exact
current baseline, including previously committed HashSet union and protected
virtual support. Review milestone 6's active ordinal-ordering edits were excluded.

All three trials use the same 14 unchanged source files at
`mmanela/diffplex@f500e73f28e28813f942f1f16dec5a34b40e48e3`, Apache-2.0,
netstandard2.0. All 24 reviewed file blobs were reverified before staging.
Candidate assembly SHA256 is still
`514ff0d67c5d10e0be5532d3ddd01a73fa705d3e28402e6c117fd14987b9e621`.
The selected source builds with zero warnings/errors. Cilica's full guest build
succeeds with one existing CS0108 warning in CandidateHashAlgorithmOwner.State.

Actual diagnostic from the [stage log](diffplex-ldlen-20261008/differential.log):

```text
Stage: translating selected candidate root; no oracle invocation yet.
CIL2001: Element addresses currently require a closed inline value or reference array.
DiffPlex.Differ::BuildModificationData token 0x06000010 IL_001e
```

The pinned source compares Int32 array elements with `Int32.Equals`, taking an
address for the instance receiver. `CandidateScalarArrayReceiverProbe.EqualsElement`
independently reproduces the rejection. `EqualsAfterArgumentMutation` exposes an
important constraint on any future fix: the receiver's storage is read after
argument evaluation, which can mutate that same array element and allocate.
Loading a scalar copy early would change behavior. This is an unsupported storage
operation, not an observed candidate semantic mismatch.

## Isolation, retained image and next action

The approved execution controls are unchanged: offline networking; UID/GID 10001;
read-only root; no host mounts, credentials or sockets; dropped capabilities and
no-new-privileges; 2 CPUs, 8 GiB RAM/no extra swap, 256 processes; 10 GiB scratch and
1 GiB output; separate host-enforced 30-minute deadline. No setup or security prompts.
No tooling was reinstalled. The retained verified image was reused by immutable ID.
The runner took 56.01 seconds, and evidence export/container removal finished at
56.6 seconds. Setup source acquisition occurred before the execution deadline.

[Exact commands/result](diffplex-ldlen-20261008/result.json),
[container configuration](diffplex-ldlen-20261008/container-inspect.json),
[inputs and package hashes](diffplex-ldlen-20261008/input-provenance.json), and
[cleanup receipt](diffplex-ldlen-20261008/cleanup.json) are retained. Full owned scripts,
licence, image metadata and hashes remain in `artifacts/diffplex-ldlen-20261008`.
Host build logs remain in `artifacts/compatibility-ldlen`.

Removed task container, candidate/output image and its untagged candidate layers,
source staging/export and disposable reduction snapshot after retaining evidence.
Kept all tests, all earlier results and the verified 470,855,666-byte tooling image:
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`.
It contains no candidate source. No peer implementation was changed or deleted.

Next: coordinate scalar-array receiver/borrow support with Review milestone 6;
promote both independent reductions to O0/O2 semantic controls and cover rooting
across allocations, receiver reads after argument mutation, null/bounds order and
location lifetime. Keep other scalar widths and escaping borrows explicitly
bounded. Then retry the same pinned candidate and all 64 transcript scenarios.
