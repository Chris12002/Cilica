# ADR 0156: Direct boxed single-argument formatting

Status: Accepted for directly proven named value boxes

## Decision

Add two exact intrinsic grants: the selected String.Format(String,Object) declaration
and the private core BoxedText<T>.Render(Object) declaration. Formatting discovery
requires an adjacent box instruction, authenticates its exact closed named value,
and excludes IFormattable through its inherited interface closure. Body translation
then independently proves that the actual argument is that box in the same block;
a lexical predecessor alone is insufficient across control-flow joins.

Redirect the public call to the closed managed BoxedFormatting<T>.Format helper.
Its public metadata permits ordinary cross-assembly binding; it is an implementation
helper, not a general framework API. The private Render operation becomes exact
UnboxLocation plus the resolved ordinary ToString override call. The original box
allocation stays in place, with its own identity, copy and allocation fault point.
Repeated placeholders invoke the same boxed payload, preserving mutable callbacks;
the caller's original value stays unchanged. Existing borrow roots and call/exception
lowering handle lifetime and faults. No new native operation is introduced.

Adapt the pinned ValueStringBuilder parser using the bounded managed StringBuilder:
braces, ASCII index/alignment, spaces, ignored format text and position-specific
English resources retain their order. Share nongeneric parsing helpers across closed
value types. This changes buffering/storage and does not promise .NET allocation
counts or numeric memory thresholds. Unsupported primitive/reference arguments,
aliases, IFormattable, providers and overloads remain compile errors. General boxed
virtual/interface dispatch is still outside this scope.

## Evidence and review

Four probes provide 1,176 repeated O0/O2 native/.NET comparisons with sanitizers and
collection before allocation. They cover mutable and readonly generic receivers,
repeated placeholders, UTF-16/NUL text, null returns, escapes/alignment, callback
failure before later parse failure, and exact malformed-format/argument messages.
Negative tests reject missing grants, external private-helper callers, IFormattable,
object aliases and a handcrafted join that bypasses the lexically adjacent box.

All 17 distinct composed checks pass, with the corrected proof test verified
separately; three formatting checks pass again after parser helper sharing. The
full solution builds without warnings. Review checked provenance, exact declaration
shapes, actual operand proof, callback mutation, roots, arithmetic bounds and error
ordering. Original pinned source and MIT notice are retained with SHA-256 evidence.

The unchanged kernel advances to its StringComparison dependency. This is compilation
closure progress; native transcripts and the final implementation/filter audit remain
required for milestone completion.
