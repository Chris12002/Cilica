# ADR 0140: Throw discards the remaining evaluation stack

Status: Accepted

The value CIL translator now consumes the exception object and discards any
remaining evaluation-stack entries at throw. Optimized Roslyn output encountered
in the dictionary key callbacks keeps an unrelated scalar on that stack. The
metadata control-flow verifier already accepts this CLI behavior; requiring an
empty remainder in body translation was an unnecessary rejection.

Exception type checks, handler entries, rethrow, endfilter and return validation
remain in place. Discarded values produce no outgoing stack edge. Existing root
and exception machinery retain conservative frame ownership and release it on
completion; no new native operation or detached IR shape is needed.

A reduced authored IL regression leaves both a scalar and a string reference
below the thrown exception. It translates in both exception profiles and passes
294 repeated native O0/O2 checks with collection and frame cleanup. The composed
dictionary/throw/exception translation run passes 26 checks. Review found no
remaining actionable issue in this correction.
