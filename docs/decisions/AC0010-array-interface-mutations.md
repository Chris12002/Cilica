# ADR AC0010: Exact array-interface mutation helpers

Accepted 2026-10-06 for bounded AC3.2.

## Decision

Extend the existing runtime-selected, ordinary managed ArrayInterface<T> bodies
with Insert, InsertObject, RemoveAt, ClearReadOnly and Clear. Each requires its own
exact approved build-manifest grant. Append capability enum members without
changing existing ordinals. The selected owner, method, signature, managed body
and receiver view remain checked before admission; this is no general intrinsic
or private-method permission. Input assemblies remain metadata-only.

Pinned SZArrayHelper.Insert<T>/RemoveAt<T> and nongeneric Array.IList.Insert/
RemoveAt always throw NotSupportedException with the fixed-size message. Do not
check the index or cast the supplied item first. InsertObject takes an object
argument independently of the vector element type, preserving this fault order.
Generic Insert uses the interface element view, as existing Add/Set do, so a
covariant array receives the exact interface argument packet. Only Set attempts a
store and needs the existing runtime covariance check.

Pinned ICollection<T>.Clear throws the read-only exception, including empty
arrays. Nongeneric IList.Clear instead resets every element. Keep separate
ClearReadOnly and Clear helpers. The latter uses a typed managed loop assigning
default(T), preserving length/identity and clearing all reference slots in nested
values. Existing value lowering and GC handle those stores. Interface callvirt
continues evaluating arguments before checking null and entering the helper.

Interface dispatch plan v4 recognizes the new exact recipes. Both logical and
scalar signatures are validated: Insert passes ordered value leaves after the
receiver/index, including zero leaves for empty values; InsertObject always keeps
its object argument. Native v41, integer-object CIL v3, object layout v15,
value/delegate lowering profiles, manifest runtime profile v1 and embedding ABIs
are unchanged. Library bytes and approved manifest hashes change.

## Evidence and review

Thirteen authored array probes plus the two formerly blocked list-interface
probes provide 4,410 repeated .NET/native comparisons at O0/O2 on macOS ARM64 with
ASan/UBSan, collection before allocation, frame budgets and cleanup assertions.
They cover exact messages, invalid indices, wrong covariant element values,
argument-before-null faults, empty arrays/values, identity and aliases, reference
and nested value clearing, and mixed list/array receivers.

Four additional native GC checks show that clearing reference and
reference-bearing value arrays releases their contained objects while the array
stays rooted. Five independent grant removals reject admission. Logical/scalar
plan mutations reject wrong operations, extra/missing arguments, index/element
and result types, including attempts to interchange the two Clear meanings.
125 existing collection, interface-plan, admission and public-baseline regression
checks pass. No native limits/timeouts were increased.

Review checked exception ordering, exact generic/nongeneric helper signatures,
empty-value flattening, covariance, typed reference clearing and root lifetime.
All generated behavior uses the existing native operations and ownership path;
no new C++ runtime code or boundary is introduced. The public ledger stays at 33
gaps and 14 controls; its supplemental ListInterfaceMutation blocker advances to
native-validated. Historical private observations remain unchanged and no private
replay root was rerun. Full AC3 and saved-game replay remain incomplete.

CopyTo, equality-based search/removal, bulk APIs, remaining Dictionary/HashSet
members and enumeration remain subsequent work. No hosted work, services or
additional agents were used.
