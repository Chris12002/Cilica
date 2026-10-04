# ADR 0079: Checked native single-target delegates

Status: accepted for authored IR. Recorded 2026-10-04.

`ValueScalarLowering.LowerDelegates` and `ObjectCppEmitter.EmitDelegates` opt into
`scalar-delegates-ir-v1`. A delegate object contains a traced receiver reference
and an index into an immutable, signature-checked target table. It contains no
managed-flow function pointer. Static and closed nonvirtual instance targets have
separate integer/void function pointer fields matching their generated signatures.

Creation validates the receiver before allocating fresh identity. Invocation checks
instance ownership, exact descriptor membership, table bounds, signature identity
and receiver compatibility before payload use. The private status 12 denotes null
closed-instance binding (.NET ArgumentException); status 4 remains null invocation.
Calls preserve target fault provenance and packed output on failure. Captured
receivers remain live through delegate graph tracing even after the creating frame
has exited. Argument packets include hidden receiver capacity and live in bounded
execution-arena storage. Earlier native profiles continue to reject delegates.

Local O0/O2 ASan/UBSan probes compare integer/void static/instance behavior with
.NET, retain a sole captured receiver through collection pressure, reject forged,
foreign and corrupt payloads, and check fresh identity, null/fault provenance,
allocation recovery, exact arena bounds and complete frame/root/borrow cleanup.
Review also checked scalar operand remapping and aggregate argument/root limits.
All 19 selected local checks pass. CIL binding, multicast, variance, open-instance
and closed-static binding remain separate gates. No public delegate ABI is added.
