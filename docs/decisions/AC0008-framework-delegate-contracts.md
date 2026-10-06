# ADR AC0008: Required framework delegate contracts

Accepted 2026-10-06 for bounded AC2.2.

## Decision

Add owned Action<T>, Func<T,TResult>, Func<T1,T2,TResult>, Predicate<T> and
Comparison<T> declarations beside Action<T1,T2>. Preserve the pinned netstandard
2.1 generic variance flags and exact constructor/Invoke mappings. Use existing
single-target construction and invocation; no new intrinsic grant or runtime
operation is introduced. Declared variance does not authorize variance conversions.

Admit Boolean delegate results in the integer-object CIL composition, so predicates
and Func<T,bool> can execute. Boolean results use the existing Int32 carrier and
ordinary method-return byte normalization. Parameters remain exact Int32 or an
admitted managed reference; other narrow, wide, inline-value and byref signatures
remain diagnostic. Non-Boolean narrow results also remain unsupported. The narrower
standalone delegate CIL profile retains its existing Int32/void bound.

Keep the closed Invoke metadata signature during discovery. At an adjacent
ldftn/newobj capture, compare declared scalar result and parameter identities with
the discovered target before lowering erases their distinctions. This prevents
Boolean/Int32 and signed/unsigned aliases from being accepted because they share
an IR carrier. Existing IR validation still checks receiver ownership, access,
static/instance binding, exact reference signatures and invocation results. Unpaired
or branch-entered construction remains rejected by the existing translator.

The composed CIL profile advances to integer-managed-objects-cil-v3. Object layout
v15, delegate value/scalar IR v2 and native v41 are unchanged. Core bytes and their
approved build manifest change; intrinsic requests and public embedding ABIs do
not. Binding plans explicitly select the new nominal declarations.

## Evidence and review

Fifteen new authored framework probes plus public FuncDelegate provide 4,704
repeated .NET/native comparisons at O0/O2 on macOS ARM64 with ASan/UBSan, forced
collection, frame-budget and root/heap cleanup checks. They cover Int32 and
reference arguments/results, captured receivers, reference identity, Action cycles,
Predicate/Func Boolean results, comparisons, target faults, argument-before-null
fault ordering and a Type-to-Func handler dictionary returning an object.

Twelve focused test cases pass, including pinned constructor/Invoke and generic
variance mapping, five unsupported C# fixtures, and four authored malformed IL
cases for scalar target mismatches. 112 distinct existing regression cases pass
across the targeted runs. The first regression run exposed the expected public
Where blocker transition: Func`2 now maps, and Enumerable (0x0200066a) remains
unmapped. Its precise expectation was updated and the gap checks rerun. No runtime
limits, process timeouts or frame limits were raised.

Review checked the closed signature source, scalar checks before carrier erasure,
unchanged reference/receiver checks, variance metadata without variance execution,
Boolean return normalization, owned-core admission and the diagnostic transition.
The ledger has 35 known gaps and 12 native controls; it retains historical private
observations separately from current public mapping evidence.

AC2.1 and AC2.2 are complete within these bounds. Actual private closed callback
signatures and the full command closure still need AC8 revalidation; wider/inline
callbacks remain unsupported. Full saved-game replay and LINQ remain incomplete.
No private root was rerun, and no hosted work, extra agents or services were used.
