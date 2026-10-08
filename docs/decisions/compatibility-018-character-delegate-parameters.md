# Character delegate parameters

2026-10-08. Bounded owned follow-up to batch 015.

## Decision

Admit exact Char parameters in the integer-object CIL profile. Char uses the
existing Int32 carrier; ordinary target argument loads/stores and address loads
retain existing UInt16 normalization. Authenticate Char target parameter identity
before lowering erases primitive distinctions. Char/UInt16/Int32 substitution must
fail even though the carrier is shared. Boolean result handling is unchanged.

This general parameter support also composes with existing Action, Predicate,
Comparison and multiple-parameter declarations. It does not admit other narrow
parameters, Char results, byref delegate signatures, variance conversions or
multicast. The standalone delegate profile retains its prior bound.

Integer CIL advances from v102 to v103. Delegate/object/scalar IR, native runtime,
function pointer signatures, GC tracing, root ownership, quotas and grants do not
change. There is no boxing fallback or upstream source execution.

## Validation plan

Compare owned static/captured predicates, all UTF-16 values, two arguments, live
callback mutation, null/fault ordering, allocation and receiver cycles with .NET at
O0/O2, forced GC, ASan/UBSan, strict frames and exact root/borrow cleanup. Reject
forged Char scalar aliases before lowering, unsupported adjacent signatures and
legacy profile use. Retain initial failures and native source/binary hashes.

## Result and review

All 51 distinct checks pass, including14,808 new repeated native/.NET comparisons,
all 65,536 UTF-16 units and sole-receiver/cycle GC retention. Three negative-only
rechecks isolate Byte/UInt16 parameter gates. Five forged Char scalar aliases fail
before lowering; legacy delegate profile remains rejected.

Initial proposed Char-array Any tests exposed the independent narrow-vector
interface gate. Retain both exact negatives and defer that capability to a separate
design; independently authored loop semantics do not establish LINQ compatibility.
No native/runtime/IR edits or new ownership paths. Reviewed normalization, exact
identity, receiver lifetime and exception cleanup; no outstanding finding.
See [batch 016](../compatibility-candidates/batch-016-results.md) for evidence and
explicit fixture/hash limitations.
