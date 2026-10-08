# ADR AC0187: Throwing invariant Int32 parsing

Recorded 2026-10-08 for AC5a.9.

Frozen TargetOptions.ParseCell uses Int32.Parse(String,NumberStyles,IFormatProvider).
Admit only that exact declaration with directly proven Integer and InvariantCulture,
through the existing exact-signature InvariantIntegerParsing capability. A detached
ThrowingIntegerParser flag distinguishes its three parameters/Int32 result from
four-parameter Boolean TryParse signatures; both IR stages and CIL operand checks
reject missing identities, mode/width/owner changes and unproven styles/providers.
Int64.Parse, other overloads and ambient culture remain unsupported.

Refactor the owned Int32 grammar into shared success/format/overflow status. Stop
accumulating after overflow but consume remaining digits and validate trailing
whitespace/NULs: malformed suffixes take precedence. TryParse still returns false
with zero output. Parse throws ArgumentNullException(s), exact Overflow_Int32, or
Format_InvalidStringWithValue preserving the entire UTF-16 input. Style/provider
admission and the wrapper guard precede null handling. No native operation changes.

Four probes provide 2,202 repeated O0/O2 native/.NET comparisons covering all prior
input cases, overflow-plus-malformed suffixes, exact messages, arbitrary UTF-16 and
roundtrips. Eleven new checks include five declaration mutations, two operand
mutations, exact grant denial and unsupported overloads. All 62 neighboring integer
parse/argument/primitive/Char/public-baseline checks pass (73 distinct). The separate
final managed oracle passes 262,752 Parse AND TryParse cases, comparing values,
exception types, full messages, parameter names and outputs, including every UTF-16
prefix/suffix around ordinary and overflowing numbers. Native checks retain forced
GC, ASan/UBSan, exact cleanup and existing frame/time bounds.

Initial native discovery rejected the owned helper's ternary output assignment
because it retained an out reference across a stack join. Explicit if/else writes
use the existing supported shape; the full managed oracle and focused native checks
were rerun after this change. Application code and rules remain unchanged.

Review checked accumulation bounds, failure precedence, output writes, message roots,
selected declaration/grant identity and typed mode/call proofs. CIL v82/object IR v53
follow the separately committed Char slice 0a40556 (v81/v52). Its 19 focused checks
are included in the 62 passing regressions above. Native v75 and production limits
remain unchanged. Main 48ecf14 remains an ancestor. Evidence:
artifacts/alchemy-int32-parse-throwing{,-refresh,-engine}.
Core SHA256: bdd10fb274bb9f8dcff3c82073aeae0a105751341b2be30b5d88dccc4fd59381.
Manifest: de07cf1b5619b28848ca0c4dda6fdfbaec30d70d237a41257d7b1141a9de8853.

Frozen discovery now exhausts the copied instruction allowance through
ContinueBorrowedShield IL_048e: 1,049,093 metadata units/4,725 ms. StateLookup remains
15 methods/11,584 units (652 ms). The captured copy is CIL v82/IR v53; no engine code
is emitted/executed. Full engine, unchanged fingerprint and saved-game replay remain
unfinished; exact instruction-budget measurement follows.
