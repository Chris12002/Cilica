# Alchemy native-build investigation

Recorded 2026-10-19 against Cilica `b97e2be30a68dd6b0cd614028185cfb7a1ce08a3`.
Investigation only: no compiler/library implementation, profile, grant, production
quota or Alchemy input changes. The next native blocker is a 2,672-byte generated
shallow-clone frame for `AlchemyGameEngine.PhysicalDamagePayload`, exceeding the
2,048-byte guard. A separate reference-adapter gap is
`String.Join(string,string[])`.

Follow-up: [AC8.106](../decisions/AC0250-bounded-clone-field-copies.md) implements
bounded clone-copy helpers and validates the owned regression. The sections
below preserve the original investigation checkpoint and its proposed slices;
the ADR and gap ledger record subsequent acceptance and engine compile evidence.

## Where the previous build stopped

The AC8.105 build directory is
`/Users/christian/Cilica/artifacts/alchemy-module-native-run/engine-O0-v2`.
Its manifest identifies 67 generated C++ sources, followed by a separate consumer.
The shared module manifest SHA256 is
`74c7820c9690d27d7f2074fdbf2bac23dc544f3d31c835978aefa828863ce880`.

The retained Linux Clang 18 build has complete object files `0000.o` through
`0020.o`. Its last progress line is `unit-0021.log`; that log is empty and
`0021.o` is absent. There is no `result.json`, cleanup receipt, linker log or
execution log. No surviving build process was found on resumption. The Docker
daemon is unavailable, so the original container's exit/OOM state cannot be read.
The artifacts establish an incomplete attempt, not whether it timed out, ran out
of memory or was interrupted. Do not label unit 21 a compiler defect from this
evidence alone.

All 68 generated files were checked against the module manifest before a separate
local macOS compile. Apple Clang 17 targets ARM64 and retains `-O0`, ASan/UBSan,
`-fno-sanitize-recover=all`, disabled C++ exceptions/RTTI, warnings as errors and
`-Wframe-larger-than=2048`. Unit 21 passes in 7.991 seconds. The subsequent
sequential scan has a 480-second aggregate ceiling and 60-second per-command
limits. It stops at the first diagnostic after 340.888 seconds: units 22–60
pass; unit 61 fails the native-frame guard. Including the separate unit 21 check,
40 generated units pass locally. Units 62–66 and the consumer were not attempted.
Successful scan objects are hashed and removed to bound disk use.

These are separate compilation observations, not a continuation using the Linux
PCH/objects, a linked application, native execution, optimized validation or a
performance comparison. Original evidence remains unchanged.

## Next native blocker: shallow-clone field copies

Unit 61 instantiates the generated clone helper for frozen Engine type token
`0x020002e3`, nested `AlchemyGameEngine.PhysicalDamagePayload`. The Engine DLL
SHA256 remains
`f42d9306229a76d833e1578a1a9bc924a5e1eeb4b56b7e326f493b68e2a9ccee`.
The helper at generated header line 30456 allocates through Cilica's existing
typed allocator, then copies 20 scalar/reference storage fields individually.
Apple Clang reports its stack frame as 2,672 bytes under O0/ASan/UBSan.

An owned sealed `WideClone` with ten Object references and ten Int32 fields,
copied through `MemberwiseClone`, reproduces the exact 2,672-byte failure. Its
root allocates a source, sets one reference and integer, clones, and is written
to check distinct clone identity plus shared reference identity. It has not run.
Metadata translation and
module emission succeed with the freshly built production compiler; compiling
its first native unit fails the unchanged stack guard. This reduction requires
no Alchemy code, expanded capacity or full-engine build.

A generated-output-only experiment splits those 20 assignments into noinline
helpers of at most eight assignments. The same reduced unit compiles with all
original warnings, sanitizers and the 2 KiB guard. This tests the proposed native
code shape; it does not validate clone execution, GC behavior, O2 or the complete
engine, and it changes no compiler source. The failure and experimental success
have separate command/hash receipts.

The matching conceptual Unity operation is visible in retained
`Alchemy.Engine__1.cpp:30261`: `PhysicalDamagePayload.Copy` calls
`Object_MemberwiseClone` at line 30274, casts the result and optionally updates
Sequence. Its source hash is
`c2e0f2fe04ff31b682816ba71ec41a4f8d8d4847224d3bff188cfa9ca48d9552`.
The version-matched installed `libil2cpp/vm/Object.cpp:135` allocates a new object,
copies the payload after the object header and applies Unity's write barrier;
SHA256 `707ce8d83c943bf0e797272f1d58dd47bbda3b2565860c0a6e22bbd40c6448d5`.
This is static architectural guidance, not evidence that the empty-board Unity
scenario executed physical damage or profiling of the failing Cilica path.

The proposed first implementation slice is bounded typed field-copy helpers in
[ObjectCppEmitter](../../src/Cilica.Compiler/ObjectCppEmitter.cs), preserving the
new allocation's header and the source's roots across allocation. Copy helpers
must not allocate or introduce callbacks/safepoints. Do not copy Unity's raw
payload-copy/write-barrier implementation into Cilica or raise the stack guard.
Add the owned wide-clone regression with O0/O2 native/.NET identity, inherited
fields, reference-bearing values, forced GC, allocation failure/recovery and
cleanup checks, then recompile the original failing unit. Complete native
execution and replay remain separate gates. This slice is proposed, not delivered.

## The existing consumer does not run a game turn

The frozen `GapProbes::Probe_EngineStep` constructs the engine and calls
`Step(null, null, null, null)`. Its native consumer expects the exact
`ArgumentNullException` type and UTF-16 text for parameter `state`, repeated with
normal and forced collection, plus frame/borrow cleanup. The unused integer
arguments do not create game state. Linking and passing that consumer would
establish an entry-fault smoke check, not command or saved-game parity.

The Unity-generated `Alchemy.Engine.cpp:9612` region likewise checks `state`
before entering the command path. The reference adapter instead calls
`MatchFactory.Start`, computes a state hash, passes priority, executes a pass
action and hashes the resulting state. A future real-game adapter needs a
separately pinned root including construction, commands and observations; it
cannot reuse the null-entry consumer as execution coverage.

## Confirmed reference-adapter gap

The retained capture is
`/Users/christian/Documents/Codex/2026-10-08/task-2/unity-testing/evidence/alchemy-reference-20261008T2312Z`.
It pins Alchemy revision `445c192ca078519ca69feda41de2b5da1a6191a3` and Unity
6000.3.2f1 ARM64. Its retained Unity and .NET transcript files match exactly.
Its engine DLL differs from the frozen Cilica audit; this is a qualified source
reference, not matched-binary Cilica validation.

The adapter's final seven-field transcript calls `string.Join("|", string[])`.
In `generated/Library/Bee/artifacts/MacStandalonePlayerBuildProgram/il2cppOutput/cpp/`:

- `Assembly-CSharp.cpp:1239` declares the exact string-array overload;
  line 1843 calls `String_Join_m557B6B554B87C1742FA0B128500073B421ED0BFD`.
- `mscorlib.cpp:26079` implements that overload, checking the array for null
  before forwarding the whole range to its range overload.
- `Alchemy.Engine.cpp:9483` defines the referenced engine Step operation.

Fresh SHA256 checks match the retained adapter/engine hashes:

| File | SHA256 |
| --- | --- |
| Assembly-CSharp.cpp | c51c61caecc4d8aa6782db307f576365e704cb0e3bdbf8a4920ecf912eef2d06 |
| mscorlib.cpp | c06eacccee873ac8f15b4b38df4c8ca524139dad6db8207f6316051f767dd8a4 |
| Alchemy.Engine.cpp | f4762054c04bf5a9f7a2dc45b837171b4b7bd2ea82073975d56bd07b1215b10f |

An independently authored reduction isolates binding without Alchemy inputs:

```csharp
public static int Probe_JoinArray(int a, int b) =>
    string.Join("|", new[] { "alpha", "beta" }).Length;

public static int Probe_JoinEnumerable(int a, int b) =>
    string.Join("|", (System.Collections.Generic.IEnumerable<string>)
        new[] { "alpha", "beta" }).Length;
```

Both methods compile against the .NET Standard 2.1 reference assembly. A fresh,
unmodified production compiler build succeeds with zero warnings/errors.
Translation against the retained owned core gives `CIL3010` at `IL_001b` for
`Probe_JoinArray`: no implementation matches the mapped owner and declaration
signature `Join`. The enumerable control translates with no diagnostics. The
earlier diagnostic compiler gives the same result; expanded capacity is not
needed to reproduce the gap. Neither reduction was executed natively.

This agrees with [ADR AC0222](../decisions/AC0222-string-sequence-join.md): only
the enumerable overload was implemented. Its array shortcut does not supply a
separate string-array method declaration. The public 47-probe baseline remains
46 controls and one diagnostic gap; these are supplemental observations.

A separate owned .NET oracle records six cases: ordinary pair, null array,
empty array, null separator, null/empty slots and exact NUL/unpaired-surrogate
units. In particular, the array overload's null argument is named `value`, while
the existing enumerable overload uses `values`. Rewriting the adapter to select
the enumerable overload would conceal this missing API and could change faults.

## Subsequent library slice

Add only the exact `Join(string,string[])` member to the owned partial String
implementation. Explicitly reject a null array with parameter `value`, then
reuse the existing managed `StringConcatenation.JoinStorage` helper over its
whole length. Reuse Cilica's checked UTF-16 construction, roots and allocation
contracts; do not copy Unity's runtime pointers or add range/Char/object/span
overloads as incidental scope.

Before closing the gap, inspect the pinned .NET implementation and add focused
native/.NET coverage for the exact signature, faults, empty/singleton/multiple
identity, null slots/separators, UTF-16 and allocation cleanup at O0/O2 with
sanitizers/forced collection. Preserve the enumerable controls. The current
investigation has not implemented or accepted that slice, run a fresh Unity
profile, compiled the complete reference adapter with Cilica or established
gameplay parity. It therefore does not close a reference-workflow gate.

## Receipts and review

Local ignored receipts live under this worktree's
`artifacts/alchemy-native-gap-20261019/`:

- `unit-0021-command.json`, `unit-0021.log`, `unit-0021-receipt.json`;
- `scan_remaining.py`, `remaining-receipt.json` and per-unit logs;
- `JoinOverloadProbe.cs`, `JoinAudit.cs`, `join-audit-receipt.json`;
- `current-compiler-build.log` and `current-audit/receipt.json`;
- `current-audit/join-overload-results.json` and `current-audit/stage-3.log`;
- `historical-object-inspection.json` checks retained ELF section-table bounds;
- `unit-0061.log`, `clone-type.json`, and `clone-reduction/WideClone.cs`;
- `clone-reduction/generation-receipt.json`, `compile-receipt.json`,
  `compile-0.log`, `batched-compile-receipt.json` and `batched-compile.log`.

Review distinguishes historical Linux objects from fresh macOS compilation,
the clone-frame failure from the adapter's missing method, and the null-entry
smoke harness from actual gameplay. The reduced failure and generated-only
experiment support the proposed copy partition; runtime correctness is untested.
No semantic support is claimed from successful source emission or metadata
translation. No hosted
work, new agents, services, push or implementation changes were started.
