# ADR 0202: Release stack budgets and measured fast paths

Status: accepted for tested macOS ARM64. Recorded 2026-10-20.

The 2 KiB per-function warning was a prototype screening rule, not a managed
semantic guarantee. A profile with 64 active managed calls needs a complete
native call-chain budget, including helpers, host adapters and runtime calls.
Per-function sizes alone do not supply that guarantee. Historical O0 sanitizer
spill failures remain useful diagnostics, not a permanent production design.

The performance path now has a separate target check: O2 without instrumentation,
a requested 512 KiB pthread stack (actual macOS stack constrained to 512–576 KiB),
and a 4 KiB per-function review threshold. Exceeding the threshold requires a
measured design decision; staying below it does not replace full-chain execution.
The existing O0/O2 sanitizer and uninstrumented 256–320 KiB recursion checks remain
as coverage of the conservative arena fallback. Neither budget is a universal
host requirement or a validated phone/WebAssembly stack contract.

Future fast kernels use native scalar locals and typed arguments only where the
compiler proves a bounded body and no allocation, callback, externally visible
write or escaping address. Cold/fault paths and large bodies keep arena storage.
Explicit fast-path sanitizer tests can have a separate review threshold, but must
execute on a constrained stack and retain lifetime, fault and cleanup assertions.
Do not disable sanitizers or silently increase managed call-depth/arena quotas.

## Reconciliation and validation

The uncommitted emitter change was exactly the older v92 behavior: it removed
native edges and direct character-string emission. Its complete patch is saved in
`artifacts/typed-internal-20261020/emitter-rollback.patch`; the file was restored
from the committed v95 source. No intentional new emitter changes were present.
The cause of the repeated local rollback is unknown; verify the file before builds.

Fresh emission is byte-identical to the accepted v95 C++ (SHA-256
`99464f8a098b918b119aa947b0f794496487a894eae45fa4e63cbe8193ea4a98`).
Fourteen focused stack, native-edge and character-construction checks pass,
including the added release-budget recursion test. All 523 DiffPlex values match;
21 retained release samples measure 169.460 us median, range 166.055–183.178 us.
This reconciliation restores existing performance; it is not a new speedup.
Review confirmed the fallback tests retain their old bounds and the release check
runs both exact/exhausted depth and arena cases on the requested thread stack.
