# ADR 0204: Stable borrow snapshots and closed native helper calls

Status: accepted. Native v97. Recorded 2026-10-20.

Extend ADR 0203's structural proof to reference/null locals, string length/code
unit/equality reads, scalar/reference loads through parameter borrows, and direct
calls to other proven kernels. The result remains Int32/Int64. Each body still
has at most four parameters, eight locals, sixteen blocks and 96 statements plus
block inputs. An acyclic callee proof permits at most eight nested kernels and
256 expanded statements/inputs. Recursion, indirect calls, unsupported operations,
heap/static/borrow writes, allocation, callbacks and handlers cannot acquire a
proof. Calls forwarding borrows, non-parameter locations and narrow borrows stay
on the existing path. This is an execution optimization, not new admitted CIL.

The typed entry checks each borrow with the existing exact kind/predicate,
readonly, lifetime and width checks, then loads its value once through the checked
location loader. Native locals hold that snapshot. No allocation, callback or
write can change the borrowed storage until the entire kernel chain returns;
reference/heap owners remain in the ordinary caller roots. References are checked
at typed entry. No unchecked native address is added to a Location. The native
kernel has no safepoint and never returns a reference or an address.

Direct kernel-to-kernel calls preserve the existing 64-call depth accounting
without duplicate roots or argument packets. Failed checks/arithmetic unwind the
kernel depth guards and retry the original framed method. Because the speculative
chain has no observable side effects, retry preserves managed fault order, source
origins and materialization. Invalid private carriers therefore continue to fail
through the original path; this is not the diagnostic validation-removal experiment.
Public handles, collection membership and reference-write checks are unchanged.

## Review and validation

Review checked the transitive effect proof, immutable block-edge copies, unsigned
integer lowering, reference pointer constness, native parameter declarations,
borrow snapshots across aliases, depth restoration and safe fault replay. Shared
module declarations and separate-unit calls use matching typed signatures.

Thirteen authored checks cover both native modes at O0/O2, scalar and reference
borrows, aliases, Int64, embedded zero/unpaired UTF-16, null/index/arithmetic faults,
mutation between calls, stale/foreign/readonly/wrong-width carriers, deep generated
calls on a constrained stack, and separate native translation units. Forced GC
and ASan/UBSan are used independently of release timing. The full focused set of
76 checks passes, including pinned numeric string hashes over lengths 0–64 and
UTF-16 boundary values, dictionary callbacks/mutations, runtime carrier tests,
managed exceptions, native modules and both stack-budget profiles.

Fresh candidate C++ matches all 523 DiffPlex values. The sequential paired release
series measures v96 169.551 us versus v97 142.711 us (1.1881x, 15.83% less time),
21 samples each. Largest O2 native frame remains 400 bytes. This does not eliminate
the remaining .NET gap. Final committed-source identity, full benchmark sanitizer
checks and overall baseline comparison are recorded in the performance report.

During this slice an unexplained writer restored ObjectCppEmitter.cs to committed
v96 while the already-built v97 compiler continued executing. The two intended
v97 lines were restored before commit. The initial benchmark's generated C++ is
v97, but its filesystem-source hash is not accepted as build identity evidence;
final regeneration from the committed source must match its C++ byte for byte.
No unrelated process was stopped or editor state overwritten to diagnose the writer.
