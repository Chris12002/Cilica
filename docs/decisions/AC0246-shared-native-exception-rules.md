# ADR AC0246: Reuse immutable exception facts during native emission

Recorded 2026-10-09 for AC8.102 after 1408a1a.

The first complete frozen Alchemy C++ emission entered the emitter after successful
logical/scalar validation, then exceeded the bounded 300-second probe allowance.
Inspection found AppendMethod constructing ManagedExceptionRules for every one of
15,254 methods. Each construction rebuilds and checks the entire nominal/interface
assignment graph. These are closure-wide facts, not method-local handler state.

## Decision and review

Construct the exception rules once inside EmitValidated and capture that instance
while emitting each method. The object remains local to one emission; no global
cache or cross-program state is introduced. Review confirms its fields are readonly
catalog/identity/relationship facts, and its emitter-facing CatchAt/CleanupAt/
FilterAt queries derive scope information solely from their supplied method/block.
Handler regions, origin tracking, cleanup ordering, root publication and native
execution remain unchanged. Existing validation still runs before emission.

No compatibility profile changes are needed: generated native text is unchanged.
The four comparison roots produce byte-identical C++ with different verified
baseline/candidate compiler binaries and the same pinned fixture DLL. Composed
uses seven methods, NestedCleanup one, RethrowIdentity five and WideRootProbe.Run
five. This covers multiple handler methods and both legacy/integer profiles.
43 selected NativeExceptionTranslation, ManagedExceptionIr, NativeExceptionIr,
ExceptionValidation and TypeInitializationException checks pass (12.328s + 11.853s).
Their native consumers retain O0/O2, ASan/UBSan, forced collection, frame-budget and
cleanup checks. No new timing assertion or test that mirrors the implementation
is added. No outstanding findings within this slice.
Receipts: artifacts/alchemy-shared-emission-rules, including compare-binaries.json
and matching compare-baseline/compare-current source receipts.

## Full-closure emission

The follow-up completes with the same 300-second bound. Engine translation,
scalar/root validation and emission take 228,045ms under shared host load. The
emitter records 12,229ms by method 14,336 (of 15,254), then completes all methods.
Its independent C++ contains 467,018,483 bytes / 5,495,963 lines; SHA256
`a6f0c1bb1388f198333f10346c9a017f24da7c4dccbb5ca79f376e0579bbd5e7`.
StateLookup emits 1,424,162 bytes in a 995ms end-to-end probe; SHA256
`cbb5b8ac8cd609667e870cdfcc1fd6985c447d4891549785ca13721e9a7ef3b9`.

This is the first complete native source for the frozen EngineStep closure.
The compiler copy still has explicit diagnostic aggregate allowances; the 2,048
integer root cap and runtime checks are the production AC8.101 implementation.
Native machine-code compilation/execution, production aggregate-capacity selection,
and saved-game replay remain unverified. Frozen input/core/manifest pins remain
unchanged. Prior timed-out source/logs are retained separately. Receipts:
artifacts/alchemy-first-native-emission and artifacts/alchemy-shared-rule-native-emission.
