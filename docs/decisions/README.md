# Architecture decisions

Each record states its context, decision, consequences, rejected alternatives
and how it will be validated. A proposed decision is not an implemented
capability. It becomes accepted once its validation experiment has run.

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-execution-and-threading-model.md) | Execution, async and threading model | Proposed |
| [0002](0002-gc-root-tracking.md) | Garbage-collection root tracking | Proposed |
| [0003](0003-class-library-strategy.md) | Class library strategy | Proposed |
| [0004](0004-exception-handling.md) | Exception handling | Proposed |
| [0005](0005-explicit-assembly-resolution.md) | Explicit assembly resolution | Accepted for resolver foundation |
| [0006](0006-structural-member-binding.md) | Structural signatures and declaration binding | Accepted for bounded binding profile |
| [0007](0007-dependency-graph-foundation.md) | Bounded dependency graphs with inclusion reasons | Accepted for ordinary dependency expansion |
| [0008](0008-conservative-dispatch-expansion.md) | Conservative dispatch over included named types | Accepted for bounded dispatch profile |
| [0009](0009-audit-evidence-contract.md) | Bounded audit evidence and explicit uncertainty | Accepted for metadata audit profile |
| [0010](0010-constrained-instance-dispatch.md) | Constrained instance-call dependency expansion | Accepted for closed named constraints |
| [0011](0011-explicit-runtime-type-mapping.md) | Explicit primitive runtime type mapping | Accepted for selected-library metadata mapping |
| [0012](0012-interleaved-reachability-scheduling.md) | Interleave ordinary discovery and dispatch | Accepted for bounded scheduling |
| [0013](0013-structural-array-receivers.md) | Structural array receivers with selected-library inheritance | Accepted for bounded array analysis |
| [0014](0014-straight-line-int32-ir.md) | Typed IR for straight-line Int32 methods | Accepted for translation profile |
| [0015](0015-int32-cpp-emission.md) | First Int32 C++ emission and native evidence | Accepted for bounded native profile |
| [0016](0016-int32-control-flow.md) | Initialized Int32 locals and explicit control flow | Accepted for initialized Int32 profile |
| [0017](0017-bounded-static-call-closure.md) | Bounded direct static-call programs | Accepted for Int32 program profile |
| [0018](0018-int32-bitwise-and-shifts.md) | Int32 bitwise operations and defined shifts | Accepted for Int32 program profile |
| [0019](0019-primitive-pending-faults.md) | Primitive pending faults and Int32 division | Accepted for bounded primitive profile |
| [0020](0020-checked-int32-arithmetic.md) | Checked Int32 arithmetic | Accepted for bounded primitive profile |
| [0021](0021-int32-remainder.md) | Int32 remainder | Accepted for bounded primitive profile |
| [0022](0022-detached-object-layouts.md) | Detached object layouts and reference storage | Accepted for bounded logical model; native objects planned |
| [0023](0023-typed-reference-value-flow.md) | Typed reference value flow | Accepted for detached IR validation; CIL objects and native references planned |
| [0024](0024-straight-line-object-operations.md) | Straight-line object operations | Accepted for bounded CIL translation; native objects and roots planned |
| [0025](0025-native-object-ownership-and-roots.md) | Native object ownership and phase A roots | Accepted for bounded native objects |
| [0026](0026-reference-control-flow.md) | Typed reference branches and backedges | Accepted for bounded object control flow |
| [0027](0027-budgeted-execution-frames.md) | Budgeted execution frames and bounded native helpers | Accepted for bounded object backend |
| [0028](0028-packed-boundaries-and-arena-diagnostics.md) | Packed object boundaries and arena lifetime diagnostics | Accepted for bounded object backend |
| [0029](0029-measured-helper-batching.md) | Dispatch benchmarks and rejected larger batches | Retain two-statement baseline |
| [0030](0030-detached-class-hierarchies.md) | Detached class hierarchies and ancestor assignability | Accepted for layout and detached reference IR |
| [0031](0031-native-inherited-fields-and-constructors.md) | Native inherited fields and constructor chains | Accepted for bounded object execution |
| [0032](0032-class-casts-and-type-tests.md) | Class casts and type tests | Accepted for bounded object execution |
| [0033](0033-exact-class-virtual-targets.md) | Exact class virtual targets | Accepted for bounded metadata resolution |
| [0034](0034-native-class-virtual-dispatch.md) | Native class virtual dispatch | Accepted for bounded object execution |
| [0035](0035-exact-interface-method-targets.md) | Exact interface method targets | Accepted for bounded metadata resolution |
| [0036](0036-interface-catalogs-and-reference-flow.md) | Interface catalogs and reference flow | Accepted for bounded detached validation |
| [0037](0037-detached-interface-dispatch-plans.md) | Detached interface declarations and dispatch plans | Accepted for bounded dispatch preparation |
| [0038](0038-interface-call-translation.md) | Interface call translation and executable body discovery | Accepted for bounded CIL/IR |
| [0039](0039-native-interface-dispatch.md) | Native interface lookup and execution | Accepted for bounded retained-heap execution |
| [0040](0040-interface-casts-and-type-tests.md) | Interface casts and type tests | Accepted for bounded retained-heap execution |
| [0041](0041-mixed-class-interface-dispatch.md) | Mixed class/interface dispatch | Accepted for validated interface-mapping owners |
| [0042](0042-reference-join-inference.md) | Common reference types at stack joins | Accepted for bounded class/interface inference |
| [0043](0043-detached-array-contracts.md) | Detached vector identities and array operations | Accepted for catalogs, validation and root assignment |
| [0044](0044-array-metadata-and-cil.md) | Array metadata and bounded CIL translation | Accepted for bounded SZARRAY signatures and operations |
| [0045](0045-native-array-storage.md) | Native vector storage and checked element operations | Accepted for bounded retained-heap execution |
| [0046](0046-detached-string-contracts.md) | Detached immutable UTF-16 string contracts | Accepted for detached identity, operations, resource limits and roots |
| [0047](0047-string-metadata-and-cil.md) | Selected String metadata and bounded intrinsic translation | Accepted for metadata, signatures, literals and exact CIL recognition |
| [0048](0048-native-string-storage.md) | Native immutable UTF-16 strings | Accepted for retained storage, instance-local literals and bounded native operations |
| [0049](0049-precise-reference-visitation.md) | Precise reference visitation | Accepted for private root/object-edge enumeration before marking and reclamation |
| [0050](0050-bounded-native-marking.md) | Bounded native marking | Accepted for allocation-free marking and scoped reachability diagnostics; no reclamation |
| [0051](0051-native-reclamation.md) | Native reclamation and bounded allocation retry | Accepted |
| [0052](0052-collection-stress.md) | Collection stress and poisoned reclamation checks | Accepted |
| [0053](0053-instance-retained-handles.md) | Instance-owned retained handles | Accepted for private native helpers |
| [0054](0054-stage-four-completion.md) | Bounded collection and handles checkpoint | Milestone 4 complete for bounded native v14 |
| [0055](0055-detached-value-layouts.md) | Detached inline value layouts | Accepted for bounded copy/reference-path planning; no execution |
| [0056](0056-value-layout-metadata.md) | Metadata discovery of inline values | Accepted for bounded metadata-backed catalogs; no execution |
| [0057](0057-typed-value-copies.md) | Typed inline copies and root planning | Accepted for detached value flow and expanded phase A roots |
| [0058](0058-scalar-value-lowering.md) | Scalar lowering of pure value copies | Accepted for bounded authored IR and local O0/O2 native conformance |
| [0059](0059-managed-storage-locations.md) | Managed storage locations and synchronous borrows | Accepted for bounded detached IR and owner-root planning; native carriers/CIL remain planned |
| [0060](0060-native-scalar-location-carriers.md) | Native scalar location carriers | Accepted for private checked frame/heap helpers; executable location IR remains planned |
| [0061](0061-native-scalar-location-ir.md) | Native scalar location IR | Accepted for authored scalar borrows composed with allocating object operations; struct expansion/CIL remain planned |
| [0062](0062-mutable-value-scalar-expansion.md) | Mutable inline value expansion | Accepted for authored struct borrows, snapshots and scalar/void returns; CIL, allocating value source programs and struct returns remain planned |
| [0063](0063-caller-owned-value-return-packets.md) | Caller-owned struct return packets | Accepted for authored IR with rooted scalar output destinations; CIL and allocating value source programs remain planned |
| [0064](0064-value-cil-discovery.md) | Bounded value CIL discovery | Accepted for static closure signatures, operand identities and access checks; body translation remains separate |
| [0065](0065-bounded-value-cil-translation.md) | Bounded static value CIL translation | Accepted for compiler-library translation and local O0/O2 native conformance; allocating value source programs remain planned |
| [0066](0066-value-array-composition.md) | Value and vector operation composition | Accepted for authored IR validation, roots and scalar lowering; CIL/native composition evidence follows |
| [0067](0067-value-array-cil-and-collection.md) | Value/vector CIL and live-reference collection | Accepted for Int32 vector CIL and local O0/O2 collection/borrow/return stress; class allocation composition remains planned |
| [0068](0068-value-object-composition.md) | Value and class-construction IR composition | Accepted for authored IR using shared constructor/access rules and scalar lowering |
| [0069](0069-value-object-cil-and-collection.md) | Value/class CIL and live-reference collection | Accepted for bounded class construction composed with static values and local O0/O2 collection/failure probes |
| [0070](0070-detached-boxed-value-ir.md) | Detached boxed-value IR | Accepted for named-struct allocation/copy/borrow contracts, logical payload plans and source roots; native execution and CIL remain planned |
| [0071](0071-native-boxed-payload-helpers.md) | Native boxed-payload helpers | Accepted for private typed payload storage, copy/borrow helpers and local O0/O2 collection probes; boxed IR emission and CIL remain planned |
| [0072](0072-native-boxed-ir-emission.md) | Native boxed IR expansion and emission | Accepted for authored boxing IR, checked scalar packets, empty borrowed-owner roots and local O0/O2 fault/collection probes |
| [0073](0073-bounded-boxed-value-cil.md) | Bounded boxed-value CIL | Accepted for named-struct box/unbox/copy CIL, C# empty structs and local O0/O2 graph/mutation/fault probes |
| [0074](0074-closed-generic-method-specialization.md) | Closed generic method specialization | Accepted for unconstrained static methods, closed context substitution, distinct native identities and O0/O2 differential probes |
| [0075](0075-closed-generic-value-layouts.md) | Closed generic value layouts | Accepted for unconstrained struct specialization, closed static owners, exact boxed identities and O0/O2 collection/fault probes |
| [0076](0076-generic-special-constraints.md) | Scalar generic closures and special constraints | Accepted for empty inline catalogs, class/struct/default-constructor constraint admission, bounded metadata work and O0/O2 native probes |
| [0077](0077-closed-generic-reference-layouts.md) | Closed generic reference layouts and construction | Accepted for concrete closed class hierarchies, scalar/reference fields, constructors and O0/O2 collection/failure probes |
| [0078](0078-detached-delegate-ir.md) | Detached single-target delegate IR | Accepted for intrinsic delegate types, checked static/instance captures, Int32/void invocation contracts and source roots; native/CIL in ADRs 0079–0080 |
| [0079](0079-native-delegate-execution.md) | Checked native single-target delegates | Accepted for authored native execution and O0/O2 collection/ownership/fault probes |
| [0080](0080-bounded-delegate-cil.md) | Bounded delegate CIL binding | Accepted for public named delegate construction and invocation composed with generic values and boxing |
| [0081](0081-managed-exception-inputs.md) | Validated managed exception inputs | Accepted for bounded region snapshots and strengthened type-test filter classification; executable handlers remain next |
| [0082](0082-managed-exception-ir.md) | Managed exception region and root contracts | Accepted for detached throw/handler/cleanup/filter IR and bounded saved-identity roots; native/CIL remain next |
| [0083](0083-native-exception-storage.md) | Private rooted native exception storage | Accepted for v17 pending/emergency roots, message storage and O0/O2 helper probes; handler dispatch remains next |
| [0084](0084-native-exception-dispatch.md) | Rooted native exception dispatch | Accepted for authored handlers, cleanup and message execution |
| [0085](0085-managed-exception-cil.md) | Bounded managed exception CIL | Accepted for compiler-library handlers, type-test filters and composed native execution |
| [0086](0086-exception-message-identity.md) | Exception message identity and final semantic probes | Accepted for fresh formatted defaults, shared resources and composed edge cases |
| [0087](0087-stage-five-completion.md) | Bounded milestone 5 completion | Accepted; 1,855 local checks pass, compiler-library scope and exclusions recorded |
| [0088](0088-explicit-library-contract-inventory.md) | Explicit library boundaries for dependency inventories | Accepted for metadata planning; library implementation and filter closure remain unresolved |
| [0089](0089-library-type-binding-foundation.md) | Explicit reference-to-implementation type correspondence | Accepted for metadata type plans and pinned baseline; member/compiler binding and intrinsic admission remain ahead |
| [0090](0090-library-member-binding.md) | Structural library member correspondence | Accepted for exact projected declaration matching |
| [0091](0091-library-compilation-views.md) | Explicit mapped compilation views | Accepted for pinned compiler binding without intrinsic admission |
| [0092](0092-library-build-admission.md) | Independently approved library build attestations | Accepted for exact image and intrinsic grants; build producer remains ahead |
| [0093](0093-int64-scalar-ir.md) | Int64 scalar IR and native arithmetic | Accepted for typed transfers and O0/O2 arithmetic conformance |
| [0094](0094-integer-cil-closures.md) | Mixed-width integer CIL closures | Accepted for static scalar CIL and O0/O2 differential execution |
| [0095](0095-integer-conversion-semantics.md) | Checked and narrow integer conversions | Accepted for all integer conversion opcodes and O0/O2 boundary conformance |
| [0096](0096-narrow-scalar-storage.md) | Narrow primitive scalar boundaries | Accepted for parameters, locals, returns and Boolean/Char CIL |
| [0097](0097-wide-object-storage.md) | Int64 object, frame and borrow storage | Accepted for explicit wide catalogs and native collection/fault probes |
| [0098](0098-wide-inline-and-boxed-values.md) | Int64 inline and boxed values | Accepted for typed copy/return packets and nested mutable boxing |
| [0099](0099-wide-managed-object-cil.md) | Wide managed object CIL | Accepted for explicit integer-object composition |
| [0100](0100-composed-reference-array-cil.md) | Composed reference-array CIL | Accepted for closed reference vectors, covariance and array casts |
| [0101](0101-closed-interface-variance-declarations.md) | Closed interface variance declarations | Accepted for metadata snapshots; executable variance remains pending |
| [0102](0102-inline-value-constructors.md) | Inline value constructors | Accepted for checked receiver initialization and native constructor calls |
| [0103](0103-composed-narrow-scalar-boundaries.md) | Composed narrow scalar boundaries | Accepted for narrow calls/locals/returns and direct constructor receiver reloads |
| [0104](0104-composed-class-instance-calls.md) | Composed direct class instance calls | Accepted for nonvirtual value/borrow signatures and preserved callvirt checks |

## Milestone 6 completion records

| ADR | Title | Status |
| --- | --- | --- |
| [0173](0173-value-array-interface-dispatch.md) | Invariant value-array interface dispatch | Accepted; exact value helpers and native conformance |
| [0174](0174-bounded-switch-cil.md) | Bounded CIL switch lowering | Accepted; source-order branches, unwind and budget checks |
| [0175](0175-constructor-entry-prefix-flow.md) | Constructor body control flow | Accepted; stable receivers and validated base initialization |
| [0176](0176-generated-cast-type-names.md) | Generated cast type names | Accepted; complete native/.NET message comparison |
| [0177](0177-stage-six-completion.md) | Bounded milestone 6 completion | Accepted; real native transcripts, compiled closure and final regression review |

## Milestone 7 embedding records

| ADR | Title | Status |
| --- | --- | --- |
| [0178](0178-persistent-scalar-c-abi.md) | Persistent scalar C ABI foundation | Accepted; bounded Int32 C consumer/lifecycle contract; multi-export and device gates remain |
| [0179](0179-shared-export-module.md) | Shared scalar export module | Accepted; explicit IDs/manifests, shared state and bounded quotas in one compiled closure |
| [0180](0180-public-retained-object-handles.md) | Public retained object handles | Accepted; typed v3 exports, explicit leases, quota preflight and guarded release |
| [0181](0181-host-owned-utf16-copying.md) | Host-owned UTF-16 copying | Accepted; optional v3 import/copy capability with exact code units and retained string handles |
| [0182](0182-exact-integer64-embedding.md) | Exact Integer64 embedding | Accepted; all-bit wide transfer, typed signatures and capability negotiation |
| [0183](0183-retained-exception-capture.md) | Retained exception capture | Accepted; optional v3 capture, bounded detail copying and preserved fault reports |

## Saved-game compiler follow-ons

Compatibility decisions use an AC-prefixed sequence so parallel milestone work
can continue allocating its numeric sequence. AC0001 was originally recorded as
0180 on the compatibility branch and renamed during AC0 integration; its decision
and historical evidence are unchanged.

| ADR | Title | Status |
| --- | --- | --- |
| [AC0001](AC0001-runtime-type-identity.md) | Bounded runtime type identity | Accepted; canonical Type/handles and Type keys; next engine gap is Nullable<T> |
| [AC0002](AC0002-nullable-value-storage.md) | Bounded nullable value storage | Accepted; managed members and traced inline payloads; special boxing/key semantics remain gated |
