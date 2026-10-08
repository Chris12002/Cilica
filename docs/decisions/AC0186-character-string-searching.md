# ADR AC0186: Character string searching

Recorded 2026-10-08 for AC7b.21.

Frozen TargetOptions.ParseCell locates a comma with String.IndexOf(Char). Add that
member and its start/count overloads using the pinned String.Searching.cs contract.
The no-range overload searches the whole string. The start overload passes an
unchecked Length-startIndex into the shared implementation, preserving validation
order at integer extremes. Unsigned bounds checks reject start first, then count;
after validation start+count cannot overflow. Managed code-unit comparisons replace
unsafe/span/vectorized searching. Successful search allocates no result object.

Seven probes supply 5,640 repeated O0/O2 native/.NET comparisons for NUL, BMP edges,
unpaired surrogates, empty strings, dynamic character strings, first occurrence,
empty/boundary ranges and integer extremes. Full exception Message/ParamName hashes
check exact resources and precedence; argument failures precede null receivers.
String-needle and comparison-mode overloads remain diagnostic. All 15 new and 54
adjacent string/range/metadata checks pass (69) with ASan/UBSan, forced collection
and exact cleanup. No full-suite claim; the public baseline is not rerun for this
managed-only addition. All 71 pinned source hashes verify.

Review checked UTF-16 equality, range arithmetic, null and argument order, no unsafe
accesses, existing String call permissions and unchanged profiles/grants/limits.
Main 48ecf14 remains an ancestor. Evidence: artifacts/alchemy-character-search{,
-refresh,-engine}. Core SHA256:
3683eacfa48a357d07f9a5a45a96af1b054b13f73929ffc169ed31e26b123d24.
Manifest: ab996a3d6fcb5470b748d1627be9bb0c067993272506a8b8c9f8e9c000384b76.

Frozen discovery uses the AC8.58 diagnostic-only 6144 field allowance and now stops
at Int32.Parse(String,NumberStyles,IFormatProvider), ParseCell IL_0031, after
1,051,158 metadata units in 4,395 ms. StateLookup remains 15 methods/11,584 units
(475 ms). No engine code is emitted/executed; full engine, unchanged fingerprint and
saved-game replay remain unfinished. The next slice requires throwing integer parsing.
