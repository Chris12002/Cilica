# DiffPlex retry after explicit earlier-contract selection

**The CIL3003 contract blocker is fixed. The unchanged candidate now reaches a
separate CIL2001 array-length branch gap. Zero of 64 candidate scenarios have run.**

The repair is committed as `ae04d324484a349839863c1789328825b61e2dd4` and described
in [the contract-selection decision](../decisions/compatibility-009-earlier-reference-contract.md).
Production assembly resolution is unchanged: exact versions, keys, cultures,
forwarding, type headers and member signatures still apply. The harness supplies
the actual pinned 2.0 contract alongside 2.1 and selects supported declarations
explicitly. HashCode/ITuple are absent in 2.0; its invariant IOrderedEnumerable<T>
is deliberately unmapped because the owned 2.1 declaration is covariant.

## Compared trials

| Observation | Original trial | Repaired retry |
| --- | --- | --- |
| Cilica commit | `268024e94879acdb004228afa360e1222e2068b8` | `ae04d324484a349839863c1789328825b61e2dd4` |
| Upstream pin | `f500e73f28e28813f942f1f16dec5a34b40e48e3` | Same |
| Source / framework | 14 unchanged files, authored adapter, netstandard2.0 | Same |
| Candidate build | Passed, zero warnings/errors | Passed, zero warnings/errors |
| Candidate assembly SHA256 | `514ff0d67c5d10e0be5532d3ddd01a73fa705d3e28402e6c117fd14987b9e621` | Identical |
| Cilica build | Passed | Passed; one existing CS0108 fixture warning |
| Explicit candidate test | Failed at CIL3003 | Failed at CIL2001 |
| Candidate oracle / native scenarios | 0 / 0 | 0 / 0 |

The retry is a current committed baseline, not an attribution claim that the
contract helper is its only difference from 268024e. The exact intervening commits
are in [baseline-changes.txt](diffplex-retry-20261008/baseline-changes.txt): GUID
TryParse, checked native indices, Int64 parsing, constrained enum text, diagnostic
documentation and this contract repair. Review milestone 6's then-uncommitted
HashSet work and later protected-virtual edits were excluded from the export.
The original [failed report](diffplex-execution-20261008.md) and its logs remain.

Actual new diagnostic:

```text
CIL2001: Array length requires an adjacent conv.i4 in the same block;
native integer flow requires later lowering.
DiffPlex.Differ::CalculateEditLength token 0x0600000e IL_001d
```

The pinned source tests whether both input arrays have zero length. The retained
independent `CandidateArrayLengthBranchProbe.EmptyPair` reproduces the same
translator diagnostic without any upstream code. Its diagnostic regression passes
against an isolated ae04d32 copy with only the owned reduction and reference-notice
copy items added. This is a newly measured unsupported IL shape, not a semantic
output failure. The explicit driver stops before invoking the candidate oracle,
emitting C++ or running O0/O2 candidate checks.

## Validation and isolation

- **84 selected host checks pass**, including all five new contract tests and
  existing binding/member/scope/constraint/default-core checks.
- **5/5 contract tests pass inside the pinned offline Linux container**. The
  independent compiled 2.0 List probe supplies 294 repeated O0/O2 native/.NET
  comparisons per platform, with forced GC and ASan/UBSan.
- **1/1 new array-length diagnostic regression passes** on the isolated host
  copy. This adds one distinct check to the original 84; the five Linux checks
  repeat the same contract suite.
- **1 explicit candidate test fails** with the diagnostic above. No candidate
  semantic comparisons, full upstream test run or full repository suite is claimed.
- The full retry takes 34.36 seconds in the runner and 35.9 seconds through evidence
  collection/removal. Setup downloads were outside the separate 30-minute deadline.
- The original approved controls remain: no network, numeric non-root user,
  read-only root, dropped capabilities/no-new-privileges, no host mounts or
  credentials, 2 CPUs, 8 GiB RAM/no extra swap, 256 processes, 10 GiB scratch,
  1 GiB evidence and a host-enforced execution deadline. No security/access prompts.
- Same official SDK 10.0.401/runtime 10.0.12/Clang 18 toolchain and previously
  approved package sources. All candidate blobs/package hashes reverified.

[Test log](diffplex-retry-20261008/differential.log),
[offline regressions](diffplex-retry-20261008/contract-regressions.log),
[host gate](diffplex-retry-20261008/tests-final.log),
[reduced diagnostic check](diffplex-retry-20261008/reduction-test.log),
[reference metadata difference](diffplex-retry-20261008/contract-shapes.log),
[exact commands/result](diffplex-retry-20261008/result.json), and
[isolation configuration](diffplex-retry-20261008/container-inspect.json) are retained.
Complete setup scripts, recipes, hashes, licence notices and logs remain under
`artifacts/diffplex-retry-20261008`; host build logs remain under
`artifacts/compatibility-netstandard20`.

## Resume and cleanup

Removed the task execution container, candidate input image and its untagged
candidate layers, upstream source staging, exported input tree, and temporary
owned reduction snapshot after checking retained evidence. No live Cilica source,
unique test, original failure evidence or peer edits were removed.

The verified reusable toolchain image contains no candidate source and is retained:
`sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`
(470,855,666 bytes). Use this immutable ID; Docker listed its tag but tag inspection
failed, while ID inspection/build/run succeeded. Retained official SDK cache and
small tooling recipe/reference staging also contain no candidate inputs.
See [cleanup receipt](diffplex-retry-20261008/cleanup.json).

Next slice: coordinate `ValueTranslator` ownership with Review milestone 6, then
admit only proven confined `ldlen` truth-branch consumers if their semantics can
reuse bounded array-length lowering. Preserve null/fault order, native-width
meaning and rejection of escaping native integer values. Promote the independent
reduction to positive O0/O2 tests with negative boundaries, then retry the same
pinned sources and all 64 transcript scenarios. Do not change upstream source,
retarget its framework or loosen exact resolution to manufacture a pass.
