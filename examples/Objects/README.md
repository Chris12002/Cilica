# Objects, arrays, strings and dispatch example

Run from a checkout with the SDK pinned by `global.json`, Bash, `diff`, and a
C++20 compiler accepting Clang flags and the address/undefined-behaviour sanitizers:

```bash
bash examples/Objects/run.sh
```

This independent [inventory simulation](Kernel/Inventory.cs) combines the bounded
object features in one application. Three items have names and scores. One derived
item adds a bonus and returns an allocated label through a virtual override. Turns
use interface and class dispatch, update integer/reference arrays, and concatenate
a transcript. A type test identifies the bonus item, a covariant Object[] view
stores a string into String[], and an ordered UTF-16 checksum makes transcript
contents observable through the Int32 native boundary.

Twelve fixed probes cover zero/normal/maximum turns, different selected items,
null and bounds failures, checked overflow, recovery on subsequent calls, and the
sample's explicit 0–12 turn limit. Representative output:

```text
empty: success 3
normal: success -50221840
null: fault 4 method=0x<MethodDef> il=1 image=<SHA-256>
after-null: success -94256154
overflow: fault 2 method=0x<MethodDef> il=2 image=<SHA-256>
after-overflow: success 169891
Native output matches .NET; generated source: <checkout>/out/objects/generated.cpp
```

The `Prepare` process executes only the authored kernel referenced at build time
to write .NET reference results. It checks the small fault helpers' IL shapes
before recording their method/IL origins. The separate `Emit` process references
Cilica's compiler API and treats the kernel and selected runtime library as metadata;
it does not reference or execute the kernel. Cilica translates the 12-body closure,
then Clang compiles its generated C++ and the small driver. The driver checks that
faults leave result storage unchanged and successes clear fault origins. Every
native value, fault status, method/IL origin and image hash must match the oracle.
Each packed call owns a fresh runtime instance. The driver additionally repeats the
same probes through private helpers in one persistent instance with collection
before every eligible allocation. It compares values and fault origins, checks
frame unwinding, and collects after each call. Recovery remains across calls, not
managed catch; the private stress path does not define a public persistent ABI.

Outputs are ignored under `out/objects`: generated C++, executable, expected/actual
text, metadata input paths/token, and entry/case headers. They can be inspected or
rerun locally. The script exits on build, translation, native compilation or
comparison failure. It starts no development service or hosted job.

The default is a Release build and native `-O2`. Use `--optimization O0` to inspect
unoptimized native execution; both modes use ASan/UBSan, warnings as errors, the
2 KiB frame warning, and disabled C++ exceptions/RTTI. `--configuration Debug`
selects Debug .NET outputs; the authored kernel remains IL-optimized in both
configurations to stay within the supported local/constructor profile.
`--output DIR` selects another directory (relative to the repository), `--no-build`
uses existing outputs, and `--no-restore` builds with previously restored packages.
`CILICA_CXX` selects a compiler executable path, without extra flags.

Recorded validation: .NET SDK 10.0.400/runtime 10.0.11 and Apple Clang 17.0.0 on
macOS ARM64; Debug O0/O2 and Release O2 match .NET. Other targets remain untested.
This demonstrates the [bounded native object profile](../../docs/compatibility.md),
including collection stress. General String/library support, a stable embedding
API, browser/mobile support and the Alchemy application remain outside this example.
Private retained handles are covered separately by the native runtime tests.
