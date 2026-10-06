# ADR AC0062: Closed generic interface method selection

Accepted 2026-10-07 for AC8.14.

Alchemy's ThenBy chain needs IOrderedEnumerable<T>.CreateOrderedEnumerable<TKey>.
Resolve an exact closed generic interface method before adding executable dispatch.
The closed interface target profile advances from v3 to v4. Ordinary and legacy
class/interface profiles retain their existing boundaries.

Selection matches open method signatures within closed declaring owners before
substituting method arguments. This prevents M<U>(U) and M<U>(Int32) from becoming
ambiguous merely because U is Int32. Authentication recomputes the requested
signature from the actual MethodDef with simultaneous owner/method substitution.
The declaration and selected implementation both validate closed constraints
against the same metadata work budget. Reader ownership remains checked before
semantic identity can conceal foreign inputs.

This slice admits public implicit and private explicit final newslot generic
implementations, inherited mappings, independent hidden slots and exact interface
reimplementation. Mapping, canonical slot and implementation must identify the
same closed method. Nonfinal generic implementations, reuse slots, abstract generic
class slots and generic class calls remain diagnostic. Body translation remains a
separate gate; metadata selection does not read or execute IL bodies.

135 selected local metadata checks pass, including 22 new cases. They cover
nongeneric and closed generic owners, distinct owner/method parameter spaces,
Int32/Int64/reference method arguments, explicit MemberRefs, inherited mappings,
structural overload selection, forged signatures/arity, open arguments, constraints,
foreign readers, legacy rejection and exact budget exhaustion. One test authored
against a nonexistent assembly Path property was corrected to retain its input
path; no production change was needed. Logs: artifacts/alchemy-generic-interface-methods/ac8n4-*.

Review checked authentication before selection, bounded constraints, simultaneous
substitution, final-slot enforcement and unchanged nongeneric mapping behavior.
No compiler IR/native profile or public probe baseline changes here. Native generic
interface calls, ordering operators and complete Alchemy replay remain unfinished.
