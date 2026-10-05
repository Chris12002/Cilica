# ADR 0143: Public dictionary contracts

Status: Accepted for the bounded member subset

## Decision

Promote the tested dictionary storage implementation to
System.Collections.Generic.Dictionary<TKey,TValue>. Preserve all declared generic,
nongeneric and serialization interfaces and exact mapped method flags. The bounded
executable subset is the default constructor, Count, TryAdd, TryGetValue,
ContainsKey and the indexer setter, plus collection flags and SyncRoot. Unsupported
members retain declarations but reach an explicit compiler rejection; they neither
return invented values nor throw a substitute runtime exception. Serialization
signature types do not establish serialization support.

Keep one storage implementation. The earlier TrialDictionary staging name is
removed. Ordinary application MethodRefs now pass through the reviewed binding
plan; no method-flag relaxation or source identifier substitution is used. The
existing exact key grants and integer/value-key limitations remain in force.

Nongeneric ICollection/IList array identities are necessary for correct shared
catalogs and complete interface dispatch. Extend the managed array recipes with
independently granted False, True and Identity helpers. Arrays report false for
nongeneric IsReadOnly and IsSynchronized, true for generic ICollection<T>.IsReadOnly
and nongeneric IsFixedSize, and themselves for SyncRoot. Count uses the existing
helper. Recipe validation is shared by metadata preparation and detached checking.
Object layout v8 and native v25 include these relationships. Other array operations
remain diagnostic.

## Evidence and review

The full local build passes. The final composed run passes 31 checks. Six public
mapped dictionary probes provide 1,764 repeated native O0/O2 sanitizer comparisons,
including generic/nongeneric aliases, value out-parameters and identity. Seven array
probes provide 2,058 comparisons, including flags, signed/unsigned covariance and
null receivers. Metadata tests compare the dictionary's full InterfaceImpl set and
every owned interface member with the pinned reference, plus each admitted public
method's flags/signature. Removing each of the five array helper grants fails.
Unsupported removal and reference-key operations retain build diagnostics.

Review checked complete array target coverage, constant-property null checks,
SyncRoot identity, reference provenance, and unchanged dictionary mutation/copy
semantics. No remaining issue was found in the admitted subset. The reviewed core
fixture records 209 inputs and 76 grants; its manifest is evidence, not production
approval. Local main remains an ancestor and no hosted work ran.

The private kernel gate now passes the missing Dictionary mapping and reports
System.Text.StringBuilder (netstandard token 0x020002c6) in its canonical writer.
This is progress through compilation, not a native kernel transcript or milestone
completion claim.
