# ADR AC0251: Exact string-array joining and the next reference-adapter gap

Recorded 2026-10-19 for AC8.107 after `c9361c7`.

## Decision and review

Add only `String.Join(string,string[])` to the owned StringConcatenation source.
Reject a null array with `ArgumentNullException("value")`, then call the existing
managed JoinStorage helper with the complete array length. Do not redirect the
adapter to its enumerable overload: that is a different method declaration and
uses null parameter name `values`. Input C# compilers already lower params calls
to an array; the implementation declaration needs no params metadata attribute.

The pinned .NET String.Manipulation.cs at runtime commit
`79d0c463f1b55624c874a11585f7e47731e8d675` validates the array before joining its
span. The local upstream file matches provenance SHA256
`1956723302dabe05550580baaf682491569dc3f825502d330d38978864d4679d`.
Its lines 744–751 and 1035 onward establish the null-array, empty/singleton,
separator, sizing, identity and copy behavior. Cilica reuses managed UTF-16
buffers and its existing checked copy/snapshot helper instead of native spans.

Review confirms array validation precedes Length, with normal caller argument
evaluation preserved. Zero-length and singleton arrays retain canonical-empty
or original-string identity. Multiple positions preserve separators even around
null/empty entries; nonempty multi-item results are independent strings. The
shared helper retains Int64 sizing, length limits and checked code-unit copies.
No ownership, rooting, exception, library-grant, compiler-profile or production
capacity changes. Native profile remains v90. Concurrent source mutation remains
unvalidated. No remaining implementation findings in this slice.

Char, range, object, generic and span overloads are not added. No application
rules or reference-adapter source changes are required for the array overload.

## Validation

The new ExactArray fixture initially reproduces CIL3010 at IL_0029. Four new
roots exercise exact array binding, null/empty entries and separators, NUL and
unpaired-surrogate units, identity and independent results, argument/fault order,
larger arrays and results retained across further allocation. They supply 3,072
additional repeated native/.NET comparisons: four roots × 128 pairs × three
repeats × O0/O2. This counts executions, not unique inputs. Existing enumerable
Join, fixed-string Concat and array Concat coverage remains passing.

The first selected run passes 18 checks; two new quota checks fail because their
expected permanent-root count omitted newly materialized string literals. Warm
the input and join paths, release the retained array and collect before measuring
that baseline. With the implementation unchanged, the focused quota rerun passes
both O0/O2 checks. Thus 20 distinct selected checks pass across those runs.

The quota harness exhausts object limits both before the temporary code-unit
buffer and before the final string, then tests byte exhaustion and recovery.
It verifies the retained input, exact post-collection object counts and cleared
frames/borrows. ASan/UBSan, forced collection, C++ exceptions/RTTI disabled,
warnings as errors and the 2 KiB native-frame guard remain enabled. Normal
differential harnesses also check exact/one-short execution quotas. New Char,
generic, range and object overload controls remain diagnostic. The separate
47-probe public baseline is unchanged and was not rerun.

Receipts: `artifacts/alchemy-string-array-join/baseline-tests.log`, `tests.log`,
`quota-tests.log`, `native-evidence/` and `quota-native-evidence/`.
No full suite, hosted work, Unity rebuild or comparative timing run was started.

## Next Alchemy compilation gap

Compile the retained AlchemyProbe.cs unchanged, with an Int32 entry wrapper that
returns `AlchemyProbe.Run().Length`. The wrapper is compile-only; transcript
length is not an equivalence observation. Use the five exact captured DLLs from
the 2026-10-08 Unity reference at Alchemy revision
`445c192ca078519ca69feda41de2b5da1a6191a3`. Every DLL hash matches the capture
report; the adapter source SHA256 is
`60b75611c4e1b4fe786beaa653eb5ca20c4ea24266957ec35b064774daaf5145`.
These inputs differ from the older frozen EngineStep audit. Do not merge their
coverage or relabel this new wrapper as the Unity-built adapter assembly.

The fresh owned-core build uses the same 70-source recipe as the frozen core,
with only StringConcatenation.cs changed. Core SHA256 is
`10a99465bbb826b4dda27ab513e4e156da93ea25030d80f2cefed778b24bb5c3`;
build manifest SHA256 is
`5f056d6b3607b7c82b431b2061d6dc563937942809a9486b91d710e123481fe8`.
The initial audit reuses the unchanged diagnostic compiler/capacity settings from
AC8.106. A second audit with the unmodified production compiler gives the same
diagnostic before reaching any capacity limit. No production limit is raised.
An initial adapter-build preflight omitted DLLs in a
nested input directory and failed before Cilica translation; preserve that log.
The corrected attempt verifies all five hashes and compiles the source normally.

The first Cilica diagnostic is CIL2001 in AlchemyProbe.Run, IL_013b, reached from
the wrapper at IL_0000:

> Default Object.ToString requires a proven ordinary class receiver; Object
> aliases, vectors and boxes require later formatting dispatch.

This is the rejection-message expression
`"Priority pass was rejected: " + result.Rejection`. The captured Rejection is
an ordinary sealed reference class. The generated Unity Assembly-CSharp.cpp
around lines 1620–1647 loads the property, null-checks a duplicated stack value,
then invokes virtual slot 3 at label IL_013b. Its verified source hash remains
`c51c61caecc4d8aa6782db307f576365e704cb0e3bdbf8a4920ecf912eef2d06`.
That rejection branch was not exercised by the retained successful transcript;
this is static reference evidence only.

An owned reduction reproduces CIL2001 at IL_0023 using the current production
compiler without expanded allowances:

```csharp
public sealed class RejectionValue { }
public sealed class RejectionResult
{
    public RejectionValue Rejection { get; }
    public RejectionResult(RejectionValue value) { Rejection = value; }
}
// Inside an ordinary static Int32 entry:
var result = new RejectionResult(a == 0 ? null : new RejectionValue());
return ("rejected: " + result.Rejection).Length;
```

Two controls translate successfully: calling ToString directly on a new value,
and storing the getter result in a typed local before a null check and ToString.
The controls were not executed natively. The implicit concatenation leaves its
receiver on the evaluation stack across a branch. NarrowVirtualReceiver currently
proves receivers within one bounded block, so it cannot provide the ordinary-class
bound at that branch target; ValueProgramDiscovery then correctly retains its
guard. This is a receiver-proof gap, not proof that ordinary class text or all
polymorphic formatting is absent.

The next proposed slice is bounded type/provenance propagation across this
null-check stack edge, independently checked by typed translation. Preserve
single getter evaluation, virtual override dispatch, null behavior, fault order,
roots and the exclusions for unknown Object aliases, vectors and boxes. Do not
rewrite Alchemy's error message, skip the rejection path or relax the guard.
That compiler change is not implemented here.

Receipts are under `artifacts/alchemy-array-join-reference-audit/`: input/tool
pins, the core build command, unchanged source and wrapper, audit commands/logs,
`result.json`, plus `reduction/ConditionalObjectText.cs`, `results.json` and
`receipt.json`. `production-audit/` retains its separately built runner and the
matching production diagnostic; its first runner preflight referenced a
diagnostic-only WorkAudit API, which was removed before that confirmation.
The original pinned two-overload reduction now translates both array and
enumerable calls against the fresh core (`join-binding-results.json`).
The full adapter audit stops in discovery before scalar lowering,
final array Join, C++ emission or execution. No full Alchemy acceptance is claimed.
