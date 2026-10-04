# ADR 0065: Bounded static value CIL translation

Status: accepted for local CIL-to-native conformance. Recorded 2026-10-04.

## Decision

`ValueTranslator.Translate` consumes metadata through ADR 0064 and returns
`ManagedLocationIrProgram` plus static method bindings. It does not accept a
caller-authored discovery result as an access proof. Native execution composes
`ValueScalarLowering.LowerLocations` and `ObjectCppEmitter.EmitLocations`; no new
native representation or struct ABI is introduced.

The `value-cil-v1` profile supports static nongeneric Int32/value signatures, void
returns and synchronous Int32/value byref parameters. Entry remains Int32-only.
Supported bodies include local/argument loads, local stores, local/by-value
argument addresses, Int32 arithmetic/comparisons/branches, exact typed stack joins,
value field reads and location-based field addresses/writes, `initobj`, `ldobj`,
`stobj`, `cpobj`, Int32/reference indirect loads/stores, direct calls and returns.
Whole-object opcodes currently require named struct tokens. `conv.i4` accepts an
already-Int32 operand; it is not general conversion support. Reference values may
flow through embedded fields with exact storage types or null, but standalone
reference signatures, reference comparisons and reference join widening are not
part of this profile.

The existing control-flow partitioner and operation vocabulary are shared with
other translators. A bounded work queue assigns exact incoming stack types once
per block. Mismatched joins and borrowed locations across joins are rejected;
there is no speculative reference inference. Unreachable IL blocks are rejected.
Every operation checks operand kind, storage identity, stack depth and variable
indices. Expansion adds to method/program IR budgets before root planning.

Field addresses preserve parent and field readonly restrictions. `cpobj` loads a
whole-value snapshot before storing, so overlap cannot destroy unread fields.
Calls and memory operations retain explicit method-exit fault edges and original
IL locations. Return packets reuse ADR 0063's rooted caller-owned storage.

Discovery also checks directly used value types in signatures, locals, type
operands and accessed value fields. Same-owner use or full external visibility is
required; broader CLI access rules remain unsupported. This closes the case where
`initobj` uses a hidden type without accessing any field.

## Scope and evidence

The new API is a compiler-library path; existing CLI commands are unchanged.
Allocations in value source programs, instance struct methods/constructors,
modifiers/prefixes, byref locals/returns/joins, heap inline values, arrays of values,
boxing, generics and managed handlers remain separate gates. Embedded reference
storage is precise, but this slice's CIL programs do not create live objects; it
does not establish allocation/collection composition for CIL values.

Manual review covered definition/access identity, readonly propagation, stack and
resource bounds, copy aliasing, ordered faults and reuse of native ownership/root
contracts. It found the hidden-type operand access gap and strengthened the loop
oracle so its output observes loop work. Reduced regressions cover invalid IL,
readonly indirect writes, hidden-type use and expanded statement limits.

Real C# return, borrow and branch/loop fixtures match .NET in local O0/O2 C++20
ASan/UBSan probes with the 2 KiB native stack-warning gate. A reduced `cpobj` probe
checks self-copy, disjoint copy and source clearing. Division failures preserve
method/IL provenance and the packed caller output. Validation totals are recorded
in the milestone tracker; no broader platform or runtime compatibility is claimed.
