# ADR 0152: Field-triggered CIL type initialization

Status: Accepted for BeforeFieldInit Int32/reference static fields

## Decision

Admit initialized classes marked BeforeFieldInit in the integer-object profile.
Resolve the exact closed, parameterless .cctor as metadata, validate its managed
body and private constructor flags, and lower each reachable ldsfld/stsfld trigger
to InitializeType before the field operation. No initializer executes in the
compiler process. Bodies use the normal compiler pipeline and runtime instance.

The chosen permitted BeforeFieldInit timing is first static field access. Every
field trigger includes accesses within the initializer; runtime reentry suppresses
repeated execution. Readonly writes require the exact owning initializer. Discovery
retains all reachable initializer bodies, normal call budgets, closed owner identity
and the existing static storage limits. Wrapper construction requires the exact
selected library constructor grant and validated signature. Public-only declaration
selection avoids the private serialization constructor's matching parameter count.

Initialization failure displays the definition name, with nested separators rendered
as '+'. Generic closed arguments remain part of the state key but are omitted from
the exception's TypeName, matching the pinned runtime. This distinction was found
by the generic-failure differential regression.

Strict types without BeforeFieldInit, module initializers, initialized value types,
static addresses and narrow/wide/inline static storage remain diagnostic. Strict
entry checks must be outside the callee's own exception regions; this slice does not
approximate that behavior by inserting a statement inside its first try block.
Private nested owner access remains a separate authenticated-access requirement.

## Evidence and review

Four probes supply 1,176 repeated native/.NET O0/O2 results with ASan/UBSan,
collection before allocation and bounded execution storage. They cover field
ordering/reentry, strings, readonly assignments, nested allocation, cached wrapper
and inner identity, generic names and independent closed generic object caches.
The harness explicitly permits persistent roots for these probes and checks stable
retained counts after collection; the separate runtime tests inspect root release.

Exact grant denial, strict-type rejection, distinct closed identity and legacy
profile boundaries pass. The full solution and 33 composed local tests pass.
Review covered metadata-only discovery, trigger placement, access checks, argument
evaluation before stsfld and definition names versus closed runtime identities.

The rebuilt core contains 212 inputs and 84 grants. The unchanged concrete kernel
now reaches the next gate: access to the compiler-generated private nested delegate
cache owner at ResolutionState.WriteCanonical IL_0002. It has not reached native
transcript comparison.
