# Bounded public vector Array.Copy

CircularBuffer constructors need Copy(Array,Array,Int32), and ToArray needs the
five-argument Int32 overload. Independent batch009 reductions diagnosed both.
This slice admits exact selected-core declarations with separate mapping/helper
grants and routes them to ordinary public ArraySections<T> managed bodies.
The shared bounded vector-argument scan proves both operands in one block; actual
call translation independently verifies assignability and normal access rules.
Both vectors must have the same declared element identity. Literal null can take
the other operand's type; two literal nulls use Int32 because source null faults
first. Arbitrary Array aliases, joins/unsupported producer shapes, cross-element
widening/boxing and Int64-length overloads remain diagnostic.

No ownership, access or type check is bypassed. Helpers are ordinary safe public
managed code. Existing private CopyCompatible and CopyStore intrinsics still
require their exact grants and selected-core caller checks. Runtime covariance
is distinct from the same-static-element requirement: object[] views can hold a
string[] destination and preserve partial writes before InvalidCastException.
Incompatible sibling runtime arrays still fail before copying, including length 0.

Pinned Array.cs supplies source-null, destination-null, negative-length,
source-index/range, destination-index/range, then type-compatibility ordering.
Subtraction validates ranges without signed overflow. A valid self-overlap with
destination inside a later source position copies backwards; other copies move
forwards. Each checked store uses existing value-copy/reference rules. There is
no temporary copy array or bulk native operation. Argument expressions evaluate
in original IL order and retain captured arrays when later arguments replace fields.

Initial Copy validation used CIL96/IR59. Peer callback-contract commit 7b7e0bf then
landed CIL97/IR60; final combined Copy validation advances to CIL98/IR60. Native79,
descriptor layouts, allocation/GC limits and
runtime ownership are unchanged. New capability enum entries are appended after
existing entries. Clear's producer scan is factored without changing its 128-step
budget, metadata charging, block-boundary restrictions or independent stack check.

Ten authored semantic roots cover full/section/overlap copies, extreme indices,
null and exception order/messages, UInt32/Int64/Byte/Char/Boolean, covariant partial
faults, incompatible runtime siblings, inline values and argument replacement.
A separate generic constructor copies into a T[] field then checks source/destination
independence. Four native lifetime tests prove overwritten roots die, copied roots
survive source clearing, and both reference/value arrays release all payloads.
Existing Clear controls rerun after factoring the scanner. Source and executable
hashes are captured before disposable C++ files are deleted. See batch011 evidence
for final counts and the retained initial test-expectation/empty-filter mistakes.

Review confirms no early type/range reordering, allocation inside the successful
copy loop, raw buffer move or new runtime intrinsic. Valid ranges bound index sums
and backward-loop decrement; stores preserve ordinary roots and copy-cast failures.
Actual CircularBuffer code is not yet compiled/executed, and ArraySegment continues
to block its segments/ToArray/enumeration surface. Earlier CRC output hashes remain
missing historically; the new collector does not retroactively fill that gap.
