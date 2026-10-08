# Explicit .NET Standard 2.0 test-contract selection

The approved DiffPlex trial at Cilica 268024e built its unchanged netstandard2.0
sources but failed with CIL3003: the harness catalogued only the 2.1 contract.
The production resolver already supports explicit mappings from multiple exact
reference images. This slice fixes harness configuration, not version resolution.

`BuildAndMap` remains the default 2.1 helper. `BuildAndMapContracts(..., true)`
additionally catalogues the official NETStandard.Library 2.0.3 reference image,
checking its full assembly identity and SHA256 before selecting mappings. The
metadata test project copies that one reference DLL through a private package
reference with all build/analyzer/runtime assets excluded. Existing header,
constraint, nested-owner and structural member checks validate the combined plan.
The original 2.0 declaration must resolve before any owned implementation match;
no lower-version, public-key or culture substitution is added. The DiffPlex source
recipe and adapter are unchanged.

Only types already selected by the owned 2.1 profile are considered. HashCode and
ITuple are explicitly absent in the pinned 2.0 image. Its IOrderedEnumerable<T>
is invariant, unlike the covariant 2.1/owned declaration, and stays unmapped.
A regression checks the unchanged header rejection. Other missing or incompatible
selected types fail the helper rather than being silently dropped. This is bounded
contract integration, not a claim that every 2.0 API is supported.

Five new tests cover a separately compiled 2.0 List<int> probe at O0/O2, omission
of the earlier contract (CIL3003), version/key/culture mismatches, forwarding,
missing/mismatched members, absent or unmapped types, changed image hashes and
conflicting identity snapshots in both directory orders. The list probe contributes
294 repeated native/.NET comparisons with forced GC and ASan/UBSan. All 84 selected
checks pass on the current host, including existing binding, member, scope,
constraint and default-core tests. Exact earlier identity and hash are retained
in the combined plan. Neither grants nor production compiler/runtime profiles or
resource limits change.

The external retry must export a committed current baseline, preserve the original
failed result, retain exact 14-source pins and use the approved offline container
with 2 CPUs, 8 GiB RAM, 256 processes, 10 GiB scratch, 1 GiB evidence and a 30-minute
host deadline. Actual candidate results are recorded separately after execution.
