# ADR 0149: Instance-owned uninitialized static storage

Status: Accepted for Int32 and admitted references without initializers

## Decision

Add explicit typed static field declarations and load/store IR to the integer-
object profile. Ordinary reachable Int32/reference static fields are admitted only
without type/module initialization, explicit storage, unsupported modifiers or
literal flags. Access remains same-owner or public on an externally visible type.
Readonly writes and static addresses remain rejected. Existing default readonly
folding is unchanged. Narrow/wide/inline static values remain diagnostic.

Field identity includes the selected image, exact closed owner and FieldDef token.
Different generic instantiations have distinct slots. Catalog validation bounds
combined instance/static fields, rejects duplicate field identities and instance
collisions, checks storage/access and requires every static declaration to be used.

Each native runtime instance lazily owns a zero-initialized slot buffer. Allocate it
outside the native stack, charge it to the instance quota, and publish only on
success. Reference slots are precise roots; stores validate reference ownership.
Load/store allocation safepoints keep operands rooted while collection runs.
Collector accounting includes the permanent buffer separately from managed objects;
instance disposal releases both. Values persist across contexts sharing an instance
but never across separate instances. No public embedding ABI is introduced.

Object layout v10, typed reference IR v8 and native v30 encode the new state.
Initialized types remain build errors until initialization semantics are provided.

## Evidence and review

Three probes compare 882 repeated native/.NET results at O0/O2 with collection
before allocations, covering reference/delegate/vector fields, generic identity,
mutation and exception order. Native sanitizer checks cover repeated calls,
separate contexts/instances, root retention/release, corrupt references, marking,
quota failure and exact-size retry. Six detached tests reject readonly writes,
missing declarations, wrong types/fault edges, duplicate identities and instance
collisions. Existing readonly/default-static checks retain their boundaries.

The full solution builds without warnings. A 29-test composed static, tracing,
collection, exception and delegate run passes; six focused validation cases pass
after adding two collision regressions (31 distinct checks). Review checked
publication, heap accounting, field identity, access, roots and native stack bounds.
The kernel's initialized callback receiver/cache remains the next gate.
