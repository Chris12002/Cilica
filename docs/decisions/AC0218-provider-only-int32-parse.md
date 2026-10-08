# ADR AC0218: Provider-only Int32 Parse

Recorded 2026-10-08 for AC8.74 after AC8.73.

The frozen ActivationCountDamage operation reloads a saved integer with
Int32.Parse(String,IFormatProvider), directly supplying InvariantCulture. Reuse the
owned ParseInt32Integer implementation; do not change application IL or parsing rules.
The pinned Int32.cs overload selects NumberStyles.Integer. The owned method guards
its provider and calls the same helper directly rather than forwarding an unproven
provider alias into another granted public overload.

Add one exact InvariantIntegerParsing grant. Discovery admits the two-argument Int32
signature alongside the original three-argument form. Translator and both logical and
scalar validators locate the provider through the selected signature, not untrusted
call operands. Keep Int64 explicit-style Parse rejected. Crucially, implicit style
no longer implies an Int64 result: validate result width from the primitive identity.
CIL104/object IR63; native79/interface plan19 and all production limits unchanged.
This slice combines with the other chat's exact Char delegate support from 4d4e4e6.

## Validation and review

Initial isolated 97ae9c4 plus parsing: 50 focused/adjacent checks pass in 59.411s.
A strengthened exact-grant check passes separately in 0.873s; it removes just the
new declaration and proves the old explicit-style declaration remains usable.
The combined Char-delegate/parsing snapshot passes all 90 checks in 119.944s.
No build runs concurrently against those native test inputs. Existing fixture warning
CandidateHashAlgorithmOwner.State hides HashAlgorithm.State is unchanged.

Four new native roots cover the shared input catalog, overflow/format precedence,
round trips and individual malformed UTF-16 suffixes. 192+96+49+30 operand pairs,
three repeats and O0/O2 give 2,202 new comparisons. Tests also exercise every UTF-16
unit around valid and overflowing text through the managed helper, null exception
parameter/message, signs, ASCII whitespace and trailing NULs. Malformed declarations,
unproven provider IR, removed grants, null/aliased providers and adjacent unsupported
styles/defaults stay negative. ASan/UBSan, forced collection, exact roots/borrows,
exact and one-short frames and the native frame cap remain enabled.

Review checks short-circuit bounds before indexing two/three/four-argument signatures,
exact static flags and primitive identity, matching return width, overload-specific
manifest selection and unchanged parser arithmetic/rooting. No new native operation,
collection layout, exception implementation, broad grant or production quota change.
The manifest test initially used a nonexistent ParameterCount property; corrected to
read the exact metadata signature of the granted token before final validation.

## Frozen audit

Six original assembly hashes match inputs-verified.json; no application rebuild.
Initial parsing snapshot core fa751c97f38eddb9dfdc0afca19837a5d9e7851d141f00c691750c85b0325358,
manifest 9a3ea38010fd2108527c333e728f964d6d3f902ad7e82a97675d891c7e49fc54.
With unchanged AC8.73 diagnostic allowances, EngineStep passes Parse and reaches
unmapped System.Collections.Generic.SortedDictionary`2 (netstandard token0x020009b9),
requested by DispelOperation.Run token0x06000b35. 3,198,499 charged units/17,911ms.
StateLookup validates 15 methods, 11,700 units/447ms. Both Emission fields are null.
The production-limit refresh rejects both roots on existing type limits before its
optional emission branch. The diagnostic copy removes that branch entirely.

Evidence: artifacts/alchemy-int32-provider-parse, -merged, -refresh and -engine.
This probe uses 97ae9c4 plus parsing; the combined tests include committed Char delegates.
Full closure admission, native engine execution, canonical event/fingerprint equality
and saved-game replay remain unfinished. AC8.73 initially misread the harness emitter
branch as actual StateLookup emission; saved rows disprove that. Correct the earlier
notes and receipts without changing compiler/test results.
