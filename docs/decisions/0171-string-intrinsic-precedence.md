# ADR 0171: Match supported String intrinsics before ordinary IL fallback

Accepted 2026-10-06; milestone 6c.56.

Composed regression tests found that the ordinary static String-body fallback
added for IsNullOrWhiteSpace ran before recognized String intrinsics. Against the
local framework fixture, Concat and equality could therefore attempt unsupported
framework IL instead of the established explicitly authorized operations.

Match exact supported member signatures and perform their intrinsic authorization
first. Only unrecognized ordinary managed static bodies reach normal discovery.
No grant, selected-image check or signature restriction is relaxed. The ported
IsNullOrWhiteSpace body retains its ordinary execution path.

A reduced C# probe combines null/non-null Concat, Equals and inequality, checks that
no framework String body enters the closure, and compares native O0/O2 results to
.NET. Existing composed UTF-16/null/collection checks and exhaustive whitespace
checks also pass. Review confirms the fallback cannot bypass a denied grant.
