# ADR AC0076: Bounded enum membership queries

Accepted 2026-10-07 for AC5b.7.

Frozen PlayerState construction needs Enum.IsDefined(Type,Object). The owned
wrapper now preserves pinned .NET type-null, value-null, enum-kind, nominal enum,
ordinal name and exact underlying-integer checks. Admitted Int32 enums accept
aliases, empty tables, negative/limit values and explicitly declared flag values;
undeclared combinations remain false. Other admitted integer carriers and Char
produce underlying-type mismatch errors; Boolean and ordinary objects produce
InvalidOperationException. Exception messages and parameter names match the oracle.

Two independently granted bodyless private helpers produce membership/status or
one exact UTF-16 mismatch string. Discovery requires exact selected signatures and
an owned-library caller. Enum field constants are read as bounded metadata only
when a query is reachable. Selected Enum shape validation remains separate.
Logical value plans preserve an explicit enum marker and validated constant rows;
scalar lowering keeps this metadata and both validators reject missing or forged
facts, forward operands, wrong implementations/results and missing fault edges.
Field tokens share the existing image-scoped uniqueness registry. Constants count
against the existing combined 4,096-field bound, with 4,096-unit individual names
and 65,536 total constant-name units. Generated type-name text has independent
existing literal-sized limits, as existing exception metadata does. Type and
metadata-work limits remain unchanged.

Native tables use exact generated descriptors and distinct type-cache identities.
Queries authenticate both references before dereference, require a canonical
instance-owned Type, compare UTF-16 names exactly and read a proven UInt32 slot.
Mismatch messages copy four immutable compiler-owned spans in one allocation;
there are no intermediate managed strings or unrooted allocation inputs. Existing
fault edges, roots and collection bookkeeping cover both helper operations.

All 116 selected checks pass (ac5b7f-tests.log, 77.340 seconds), including 4,950
repeated new native/.NET comparisons at O0/O2 with forced collection, ASan/UBSan,
2,048-byte native frame checks and unchanged 30-second subprocess limits. Tests
cover malformed raw constant metadata, nine alterations at both IR stages, grants,
foreign callers, unsupported wide enums and foreign/fabricated native references.
Adjacent enum/value/producer tests and all public gap expectations pass. The
public baseline advances to 32 native controls and 15 specific diagnostic gaps.
An initial hostile MemberRef test had the wrong parameter signature; correcting
it proves the intended lexical rejection. No compiler access gate was relaxed.

Review checked constant/token budgets, nominal identity, metadata-only reads,
argument order, carrier checks, UTF-16 ownership, allocation and fault paths.
Integer-object CIL v20, object IR v28, scalar boxed IR v11, value-layout v4 and
native v54 record the new operation/metadata; native physical layout remains v21.
Upstream Enum.cs and RuntimeType.cs are pinned and hashed in library/provenance.json.
Generic IsDefined, wide enums, parsing and general reflection remain unsupported.

Frozen inputs are unchanged. Engine admission passes Enum.IsDefined and next
reaches 256 cached snapshots in UnitRoster.WithViews IL_000d via PrepareForfeit:
185 nominal declarations, 96 closed snapshots, 29 value/226 reference/8 vector
roots and 5,544 remaining metadata work units. State retains its 257/256 catalog
limit. Neither full engine execution nor saved-game replay is claimed.
Evidence: artifacts/alchemy-enum-defined/ac5b7f-*, engine-and-state.json and refresh.log.
Core: 0635fa260d7e6eb67f111c2272147fae1daae461207752f0f9c7dd3fd1bd14b4.
Manifest: 7639799097e274f534b5e5d520c3e11ca04be82af52a0cfa7532040af450db3b.
