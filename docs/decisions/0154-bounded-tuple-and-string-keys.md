# ADR 0154: Bounded tuple and string keys

Status: Accepted for exact admitted component operations

## Decision

Adapt the pinned pair ValueTuple implementation into ordinary managed code, retaining
its public fields and six nominal reference contracts. Typed equality copies each
component before invoking the exact private key helper, preserving comparer receiver
semantics and short circuiting. GetHashCode instead passes the original fields by
reference, in left-to-right order, preserving mutating value callbacks.

The existing exact private key grants now select ordinary managed string equality
and hashing for the exact String primitive. No public String.GetHashCode intrinsic
or general reference fallback is added. String hashing adapts the pinned two-state
UTF-16 algorithm with checked pairs instead of unsafe word reads. A private pair
combiner adapts pinned xxHash32 with a fixed zero seed. Numeric hashes are private,
implementation-specific values, not portable serialized data. Source originals,
SHA-256 records and MIT/xxHash BSD notices are retained.

Managed sequential inline storage replaces upstream auto layout; no native layout
compatibility is advertised. Object equality, comparison, structural operations,
tuple indexing and formatting have explicit unsupported markers. Boxing values
with interfaces remains outside this slice. The test fixture materializes constructor
arguments before taking value addresses, since value locations across branch joins
remain outside the existing profile; no application code was changed.

## Evidence and review

Four independent probes provide 1,176 repeated native/.NET comparisons at O0/O2,
with ASan/UBSan, collection before allocations and ownership/frame cleanup checks.
They cover value copies, default/null components, separately allocated equal string
keys, embedded NUL/surrogates, dictionary growth and mutating equality/hash callbacks.
The latter regression exposed and corrected an erroneous hash receiver copy.
Metadata tests compare nominal interfaces and field/member mappings. Missing exact
key grants and unsupported members fail compilation. All nine composed tuple,
dictionary and owned-core checks pass; the full solution builds without warnings.
Review checked signed arithmetic, UTF-16 boundaries, allocation roots, nominal
contracts and private grant selection. No new native operation or format is added.

The unchanged kernel advances to String.Format in DriveAutomatic. Native transcript
comparison and full implementation/filter closure remain milestone exit gates.
