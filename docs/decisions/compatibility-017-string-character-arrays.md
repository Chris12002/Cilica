# Compatibility017: parameterless String.ToCharArray

## Scope and decision

The online-screened MIT Albireo.Base32 decoder at pinned commit
1eabd2fcba515d0bd63881fd2a2879555c43648b calls parameterless String.ToCharArray after
TrimEnd and ToUpperInvariant. The parent authorized this owned slice and minimal
predicate/byref checks, not upstream candidate execution. No new source download,
clone, upstream project/test/generator or binary execution occurred.

Implement the method in existing StringCharacters.cs. For an empty receiver return
Array.Empty<Char>(); otherwise allocate a fresh Char vector and copy every UTF-16
code unit with existing checked reads/stores. The immutable receiver stays rooted
across allocation. No normalization, surrogate pairing, aliasing or native memory
copy is introduced. A nonempty result is independent of its source and other results;
mutations cannot change the string. Null receivers retain normal call semantics.
The existing per-instance empty-array cache supplies identity and lifetime boundaries.

This follows the already retained MIT dotnet/runtime String.cs at commit
79d0c463f1b55624c874a11585f7e47731e8d675 without its Buffer.Memmove/pointer helpers.
It adds no grant, compiler/runtime operation, profile, quota, source-list entry or
extra temporary buffer. The range overload is deliberately a separate boundary:
its exact range/fault/actual-value semantics have not been implemented in this slice.

## Independent reductions and review

Owned probes compare full payloads, fresh-result independence, canonical empty cache
identity, receiver evaluation/faults and every UTF-16 code unit in32-unit groups.
A small readonly in-string parameter calls TrimEnd(Char[]) and then ToCharArray,
covering a live borrowed reference across allocation and copied results. The prior
ToCharArray negative becomes a positive regression only after its exact baseline
CIL3010 is retained. All native probes keep O0/O2, forced GC, sanitizers, exact
post-collection roots/borrows and exact/one-short managed frames plus2KiB native
frame warnings. Final counts and raw attempts are in batch015 evidence.

The independent Func<Char,Boolean>/Enumerable.Any probe reaches CIL2001 because
unsigned/narrow delegate parameters require later lowering. Keep that exact negative
rather than widening compiler admission or changing the candidate to an Int32 lambda.
The minimal readonly composition succeeds; this does not establish the full decoder.
A later Char delegate slice must preserve nominal widths, argument evaluation,
callbacks, faults and roots before proposing external execution.

Adjacent EmptyArrayLibraryTests contained a stale Array.Empty<UInt32> rejection from
before batch007's UInt32 support. Replace it with positive UInt32 cache identity,
independence and bounds behavior, while retaining an unsupported UInt64-vector
negative and the existing exact-grant denial check. This is a test correction, not
new array support. Historical failure evidence is retained. Peer Decimal/SequenceEqual
work remains unchanged; full repository tests and Alchemy replay are not claimed.
