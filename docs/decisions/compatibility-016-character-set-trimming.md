# Compatibility016: explicit-character and directional string trimming

## Context and decision

Online screening found the small MIT Albireo.Base32 decoder at pinned commit
1eabd2fcba515d0bd63881fd2a2879555c43648b. Its netstandard2.0 asset calls
String.TrimEnd through the character-array params signature. Independent owned
fixtures fail CIL3010 for the missing character overload before implementation.
Candidate execution is not authorized and has not occurred.

Implement Trim/TrimStart/TrimEnd(Char), their Char[] overloads, and the parameterless
TrimStart/TrimEnd members as ordinary managed compositions in StringWhitespace.cs.
Reuse existing UTF-16 reads, UnicodeWhitespace and Substring; no native operation,
intrinsic grant, profile, resource quota or source-list change. Existing Trim()
shares the directional whitespace helper. This is general library support, not a
candidate rewrite. The design was reported before implementation.

## Semantic contract

Directional bounds are half-open [start,end); head scanning cannot pass the tail.
Single-character comparisons and set membership compare exact UTF-16 code units,
including NUL and isolated surrogates, without normalization or case folding.
Null/empty sets select Unicode whitespace. A nonempty set containing whitespace
removes only its own members. Duplicate members have no effect. The set is read
fresh on each call and is neither modified nor retained. There are no callbacks
or allocations while scanning; the result uses the existing checked Substring.
An unchanged result retains receiver identity; fully trimmed results use canonical
Empty. No temporary set allocation is added to the Char path.

Normal call evaluation preserves receiver/argument side effects and exceptions
before the receiver null check. Existing translated string calls enforce null
receivers; the helper does not turn them into empty strings. Params is a caller
lowering convention: the owned implementation declares Char[] without requiring
ParamArrayAttribute, preserving the exact declaration signature.

The upstream reference is the already retained MIT dotnet/runtime
79d0c463f1b55624c874a11585f7e47731e8d675 String.Manipulation.cs Trim methods and
CreateTrimmedString; the new code is an owned managed composition, without pointer
helpers. Existing source provenance and licence notices remain applicable.

## Validation and boundaries

Owned probes cover all nine exposed shapes, mixed/all/empty edge sets, full payload
and identity observations, NUL/surrogates/FFFF, null and throwing receiver/argument
ordering, set mutation between calls, later allocations and existing exhaustive
whitespace classification. Native/.NET comparison uses O0/O2, three repeated calls,
forced collection, ASan/UBSan, exact post-collection roots, managed frame budgets
and strict native frames. Final results are in batch014 evidence.

String.ToCharArray is separately diagnosed. Span overloads are outside this slice;
the fixture's earlier framework lacks the newer span-trimming member, so the initial
attempt to use it as a negative probe was removed with its failed build preserved.
Albireo's full decoder still needs independent ToCharArray, Char-predicate and readonly
input-reference mapping; Encode additionally needs Decimal.Ceiling beyond the recent
bounded Decimal slice. No candidate compatibility claim follows from these tests.
Wiry.Base32 unsafe kernels, LruCacheNet locks/events and SimpleBase's current
net8/9/10-only targets were screened out. See batch-014.json for pinned evidence.
