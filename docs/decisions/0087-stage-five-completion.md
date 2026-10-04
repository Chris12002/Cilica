# ADR 0087: Bounded milestone 5 completion

Status: accepted. Recorded 2026-10-05.

Milestone 5 closes after the reviewed slices recorded in ADRs 0055–0086. It supplies
an independent metadata → typed IR → scalar lowering → C++ → native path for bounded
struct values/borrows/returns, named-struct boxing, closed generic specializations,
single-target delegates and managed exceptions with stage-A type-test filters.
Compiler input assemblies remain metadata; only authored test oracles execute .NET.

The final local sequential command was:

```sh
dotnet tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll -noColor -parallel none
```

Result: **1,855 total; 0 errors; 0 failed; 0 skipped; 0 not run**, 199.949 seconds.
The suite was built with .NET SDK 10.0.400 and ran on .NET 10.0.11, macOS ARM64,
Apple Clang 17.0.0 (`clang-1700.4.4.1`). Native semantic harnesses retain O0/O2,
ASan/UBSan, no C++ exceptions/RTTI and the 2 KiB stack-frame gate. Earlier primitive,
object, collection and private-handle regressions pass alongside milestone 5 tests.

Review covered expansion/resource limits, closed identities, visibility, failed
writes and outputs, ownership and borrows, collection across allocating calls,
handler/cleanup/filter ordering, exception identity/origin and message text/identity.
The last review fixed unconditional message caching and validated a separately
rooted nine-resource pool. Reduced tests preserve every discovered behavior.
No unresolved actionable findings remain within the bounded supported profile.

The completion boundary is the compiler-library API and packed Int32/status/origin
entry. It does not expand the existing CLI or establish a public struct/delegate/
exception ABI. It excludes inline struct heap fields, primitive/enum/nullable boxing,
generic dispatch and nominal/dependent constraints, generic delegates, private
compiler-generated closures, multicast/variance, general filters, arbitrary exception
APIs, localization and interface-target cast messages. Native mobile and WebAssembly
remain untested. The broader production design in ADRs 0001–0004 remains staged.

The working branch contains local main; no shared history was rewritten. All
implementation slices were committed after review. No push, hosted CI, paid work,
long-running service or additional agent was used. Subsequent work begins with the
required library/turn-kernel scope of milestone 6 under a separate instruction.
