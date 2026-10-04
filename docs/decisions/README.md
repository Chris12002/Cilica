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
