# ADR 0226: Native scalar locals in direct control flow

Status: accepted. Recorded 2026-10-20.

## Context

Direct control flow (ADR 0199) renders a method's checked steps as native labels.
Every Int32/Int64 parameter, local, block input and IR value still lives in an
arena frame slot. The frame escapes into calls, and `uint32_t` slots may alias
`Context` fields, so the native compiler must keep every load and store.

REA/Ghidra shows `CalculateEditLength` with 892 loads/stores among 1,880
instructions; IL2CPP has 73 of 369 and NativeAOT 54 of 275. A retained-code
sizing experiment removed 5.7–7.4% of character and 5.8–9.0% of line time with
identical outputs; see [the experiment](../performance/scalar-locals-sizing-20261020.md).

## Decision

Native v117 keeps eligible scalar storage in native C++ locals inside the direct
rendering of each method:

- `ObjectFrameLayout` numbers scalar parameters/locals that are targets of
  `AddressParameter`/`AddressLocal` first. These **frame scalars** keep fixed
  `integers`/`wide_integers` indices, because managed locations, the local-output
  and local-character carriers and borrow registration address them by slot.
- All other Int32/Int64 storage is a **native scalar**. Step text names it with
  `CILICA_I(n)` / `CILICA_W(n)`. The step-function rendering defines these as
  frame slots; the direct rendering defines them as zero-initialized locals
  declared before the first branch. Both renderings use the same validated steps.
- `CILICA_NATIVE_SCALARS` defaults to 1 only when direct flow is enabled in an
  optimized build. Explicit 0 keeps direct scalars in the frame for diagnosis;
  explicit 1 without direct flow is a compile error. Unoptimized direct builds keep
  frame slots, because unoptimized locals do not share native stack storage.
- Under `CILICA_NATIVE_SCALARS`, a directly rendered method's frame struct holds
  only frame scalars; otherwise it holds every scalar. Each struct is emitted with
  its method once eligibility is known, and every prepared frame must be consumed
  exactly once.
- References remain in `roots`; GC precision, rooting, borrow registration,
  depth/arena accounting and public boundaries are unchanged. The dispatcher
  rendering (O0 default, local exception regions, dynamic exception terminators,
  more than 512 steps) is unchanged.

Integer slots are never GC roots. Methods with local handlers keep the dispatcher,
so no handler can observe a native scalar. Faults exit the method and abandon its
frame, and typed-kernel fallbacks replay from their arguments. No runtime,
tracing, exception or diagnostic path reads scalar frame slots except through
registered frame scalars.

## Consequences

Native stack frames of direct methods grow by spilled scalars, while arena frames
shrink. The ADR 0202 per-frame threshold and the constrained-stack checks remain
the guard; recursion is covered by the existing whole-call-chain tests.
Getter inlining, reference locals with safepoint spills and dispatch caches are
separate follow-on slices.

## Review and evidence

Review checked every consumer of frame layout strings and indices: steps,
`frame_location`, local-output/local-character carriers, exception flow and
borrow registration. Only address-taken parameters/locals are reached by slot.
Exception helpers and fault exits never read scalar slots. Locals are declared
before the first `goto`, are `[[maybe_unused]]` for `-Werror` builds, and are
zero-initialized to match frame semantics.

Review found two issues before acceptance. Forcing direct flow at O0 created very
large sanitizer frames, which led to the optimized-only default. A test helper
mis-sliced frame structs and was corrected. No findings remain.

Seven new checks and all 217 `Native*` tests pass with ASan/UBSan, forced GC,
exact arena peaks and shared modules. A full-suite comparison against a clean
baseline worktree finds no new failures: non-timeout failures predate this
change, and timeout-prone cases pass serially. See the performance report. Fresh v117
DiffPlex output matches all 523 character and 2,061 line observations. One
interleaved four-backend batch measures v116 → v117 at 22.481 → 20.829 µs
(−7.35%) for characters and 36.336 → 32.880 µs (−9.51%) for lines. Ghidra shows
`CalculateEditLength` loads/stores falling from 892 to 440. The largest native
frame stays 432 bytes. See [measurements](../performance/scalar-locals-20261020.md).
