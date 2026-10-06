# ADR AC0037: Two-box composite formatting

Accepted 2026-10-07 for bounded AC7b.4.

The selected core adds the exact String.Format(string, object, object) declaration
and its own BoxedStringFormat grant. Discovery selects one or two candidate named
value box types; translation must independently prove each actual argument is the
matching BoxValue in that same control-flow block. The last argument still requires
an immediately preceding box, and intervening unrelated boxes may prevent admission.
Candidate scanning alone is never flow proof. Aliases, joins, providers, scalar/enum
formatting and IFormattable remain diagnostic. The scalar gap now has a direct error.

One- and two-argument entry helpers share the existing pinned composite parser.
Each callback borrows the original box payload, preserving repeated mutations,
argument ordering, null ToString results, alignment, UTF-16, and parse/callback
failure order. No object array or general virtual boxed dispatch is introduced.
Provenance records the adaptation. Native v42/layout v17/object IR v16/CIL v4 remain.

Five new probes pass 1,470 repeated O0/O2 native/oracle comparisons with ASan/UBSan,
forced collection, exact cleanup and frame quotas. Five previous formatter probes
also pass 1,470 comparisons through the shared parser. Tests cover distinct/same
value types, generic and returned values, reference-bearing mutating callbacks and
fault ordering. Both argument alias positions, joins and both IFormattable positions
are rejected. With only the two-argument grant removed, one-argument formatting
still works; removing the render grant rejects the pair. 71 selected formatting,
manifest and public baseline checks pass. Review checked each argument's SSA box
identity, allocation/callback order, exact grants and unchanged native ownership.

Authored fixtures were isolated from the existing managed-location join restriction;
no game inputs changed. The private-helper regression now expects the earlier source
lexical rejection introduced by AC8.2. Ledger review also corrected a pre-existing
AC6.3 label that duplicated AC5b.1, and loading the ledger now rejects duplicate keys.

Frozen GetPlayer now resolves the two-object overload and stops at its following
KeyNotFoundException construction (IL_0025), whose contract type is unmapped. This
is declaration/closure progress, not actual engine execution. The state probe still
reaches the unchanged catalog limit. Private commands, core manifest and logs live
in artifacts/alchemy-pair-formatting. Public counts remain 18 gaps/29 controls:
the public StringFormatTwo probe uses Int32 arguments, which remain unsupported.
