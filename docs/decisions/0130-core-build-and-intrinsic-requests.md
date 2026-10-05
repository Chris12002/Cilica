# ADR 0130: Core builds and explicit intrinsic requests

Status: Accepted

## Decision

Extend the build producer with an explicit zero-reference core profile. Retain
fixed safe C# 9 options and specify the CLI metadata version when there is no
platform CoreLib. The ordinary library profile continues to require explicit
references. Hash the selected profile and requested intrinsic declarations in
the effective build options before invoking the compiler.

Intrinsic requests name an emitted top-level type and either its layout or one
method selected by name and parameter count. Resolve against the owned output
snapshot with bounded metadata work, reject ambiguous/missing/forwarded members
and duplicate requests, and generate exact token/owner/signature-hash evidence.
Strict bounded JSON requires explicit fields and named capabilities, rejecting
unknown and duplicate properties. Failure cleans staging and publishes nothing.

The producer still never admits a resolver scope, approves a digest or edits the
trusted baseline. A request is evidence for independent review, not authority.

## Evidence and review

The combined library/core validation run passes 136 focused checks. Twelve new
producer/selector checks cover invalid requests, strict JSON, profile bounds,
cleanup and metadata budgets. Existing actual build, admission, mapping and
audit regressions pass. Review ensured owner inspection is budgeted and selected
declarations cannot come from a forwarded image. The core source/image and native
behavior are recorded separately in ADR 0131.
