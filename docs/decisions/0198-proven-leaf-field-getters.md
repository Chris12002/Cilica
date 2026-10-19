# ADR 0198: Direct native paths for proven field getters

Status: accepted. Recorded 2026-10-20.

## Decision

Recognize a validated single-block method with one reference parameter, no locals
or exception regions, exactly a parameter load followed by an instance field load,
and a return of that loaded scalar/reference value. Match operations and value
identities, not method names or property attributes. Int32, Int64 and reference
results are eligible. Writes, calls, branches, borrows, static fields and expanded
multi-field value results remain on the existing path.

For a normal nested call (depth 1–63, no pending fault or exception), validate the
nonnull receiver, diagnostic internal membership and declared receiver/field-owner
types, then return the field through the existing native layout accessor. Preserve
unsigned storage bits using bit_cast for scalar returns. Equal receiver/owner
predicates need only one check because there is no intervening mutation/callback.

No managed call, allocation, safepoint or escaping borrow occurs on this path.
The caller already retains its receiver and publishes a reference result before
subsequent allocation. A separate callee root frame and execution arena reservation
therefore provide no additional lifetime protection. Depth and arena usage stay
unchanged. This path can succeed without spare execution storage; it does not
consume a frame simply to preserve a historical implementation limit.

Cold entry, depth exhaustion, pending faults/exceptions, null and invalid receivers
fall back to the original framed implementation. Existing preparation, resource
failure ordering, method/IL fault origins, exception materialization and catch
behavior remain there. The fallback is a separate noinline native function, while
the small normal wrapper remains available for native inlining. Public boundary
ownership checks and GC membership are unchanged.

Use the existing descriptor field accessor instead of casting a derived allocation
to a flattened base struct: that preserves C++ object provenance for inherited
fields. This first slice still pays the accessor/type-helper costs. General leaf
expressions, setters, direct layout specialization and cross-module inlining without
LTO are future work. No performance gain from them is assumed.

Native emission advances to v92 for the execution-path/budget change. Managed
object/instance layout, IR profiles, grants, owned core/library and public ABI are
unchanged. The original frame definitions and step bodies remain for fallback.

## Review and validation

Review traced every condition to the original entry/field checks, verified that
the fast path cannot allocate, and checked signed bit preservation, inherited-field
access, reference return publication and module linkage. Failure paths reuse their
original code. Pending state is not overwritten by the fast path. No outstanding
review findings.

Seven authored checks cover O0/O2 differential execution with forced GC and
ASan/UBSan, wide bits, inheritance, returned-reference survival, caught null faults,
zero-arena nested execution, depth limits, cold entry, exact fault origin,
wrong/foreign/forged receivers, preserved pending state and separate translation
units. Forty existing checks cover object execution, collection, diagnostic
references, execution frames, embedding handles, exceptions and modules. All 47
distinct checks pass. Initial test-only compile/name-lookup issues were corrected.

Fresh DiffPlex emission identifies 19 eligible getters. Full-source normalization
removes only the reviewed wrappers and restores fallback names/profile; the result
matches the independently emitted baseline byte for byte. Paired release medians
improve from 372.992 to 363.076 microseconds/op (1.0273x). All three process pairs
favor the candidate, with overlapping sample ranges; the benefit is modest.
Four full transcripts and two forced-GC operations pass. ARM64 caller disassembly
confirms wrapper inlining and immediate caller-root publication. See the
[performance record](../performance/leaf-getters-20261020.md).
