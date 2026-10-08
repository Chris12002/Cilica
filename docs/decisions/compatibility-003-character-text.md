# Character text through exact narrow scalar receivers

Recorded 2026-10-08 for compatibility batch 003.

DiffPlex's pinned CharacterChunker requires parameterless Char.ToString. Existing
AC0166 already preserves UInt16 local/parameter borrows. Extend primitive receiver
facts with the exact selected Char identity and an explicit UInt16 receiver type;
do not model Char as an empty inline value or an unconstrained Int32 pointer.
Discovery admits only the parameterless public virtual ToString override. Detached
object IR validation independently checks its selected owner, name, flags, result,
arity and exact narrow receiver. Integer formatting/provider grants remain separate.
Static Char.ToString(Char) is an ordinary managed method, not an intrinsic.

Reuse String(Char[]) and its existing exact construction grants. Its managed
copy path roots the temporary arrays and preserves every UTF-16 code unit, including
NUL and unpaired surrogates. Following the pinned Char.ToString/CreateFromChar
contract, each call returns a fresh one-character string. This simple implementation
uses transient arrays rather than adding a cache or native allocator operation.
All allocations stay within existing object/frame quotas; no callbacks or mutable
character views survive the call.

PrimitiveReceiver.ReceiverType centralizes receiver storage: Char uses an Int32
carrier with UInt16 width; existing Int32/UInt32/Int64/UInt64 receivers keep their
full-width borrows. Scalar lowering erases declaration-only primitive facts while
preserving the narrow location type, using unchanged native width checks.
Integer CIL v81 and object IR v52 record the new admission; native algorithms and
resource limits are unchanged.

Provider overloads, boxed virtual formatting, constrained character dispatch and
character field/array borrows remain outside this slice. Existing field/array
address diagnostics are intentional; local, by-value parameter and synchronous ref
receivers are tested. No claim of complete character or candidate compatibility.

Review checked normalization before parameter/local storage, receiver copies versus
aliases, fresh-string identity, roots through the two managed copy allocations,
exception cleanup and exact width admission. All 19 focused and 52 adjacent checks
pass; see [batch results](../compatibility-candidates/batch-003-results.md).
