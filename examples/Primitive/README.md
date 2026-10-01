# Primitive execution example

Run from a checkout with the SDK pinned by `global.json`, Bash, `diff`, and a
C++20 compiler accepting Clang flags and the undefined-behaviour sanitizer:

```bash
bash examples/Primitive/run.sh
```

Recorded local target: .NET SDK 10.0.400 / runtime 10.0.11, Apple Clang 17.0.0
on macOS ARM64. Other targets remain untested. `CILICA_CXX` can select a compiler
executable path; it does not accept extra flags. The script builds in Release
with one MSBuild worker and no compiler/build servers, then compiles native code
at `-O2` with UBSan, warnings as errors, and C++ exceptions/RTTI disabled.

The authored [kernel](Kernel/IntegerKernel.cs) repeatedly calls a checked Int32
addition. Four fixed probes exercise a loop, a direct static call, overflow,
recovery on the next native invocation, and an empty loop that avoids overflow:

```text
normal: success 25
overflow: Overflow method=0x<Step MethodDef> il=2 image=<SHA-256>
recovery: success 4
empty: success 2147483647
Native output matches .NET; generated source: <checkout>/out/primitive/generated.cpp
```

The helper runs this known, build-time-referenced kernel under .NET to produce
reference results and derives its root token and image hash. Cilica runs in a
separate process and treats that assembly as data. It translates the complete
`Calculate` → `Step` closure to C++; Clang builds the generated source with the
small native driver. The driver calls the status entry, checks unchanged result
storage on fault and cleared origin storage on success, and prints values or
fault origins. The script requires native output to match .NET exactly.

Outputs are in the ignored `out/primitive` directory: `generated.cpp`, the
`primitive` executable, `expected.txt`, `actual.txt`, and prepared entry/case
headers. You can inspect the source or run `out/primitive/primitive` again.
Tokens and hashes come from each build; no identifier needs hand editing.

Use `--configuration Debug` for Debug outputs, `--output DIR` for another output
directory (relative paths resolve from the repository), or `--no-build` after
building the solution/helper in the selected configuration. `--no-restore` builds
using previously restored dependencies, useful offline when NuGet's audit feed
is unavailable. The normal build retains package audit checks. Local Release
validation used `--no-restore`; Debug validation used `--no-build`. Missing tools
and unsupported compilation fail the command. No development service is required.

This is the [bounded Int32 profile](../../docs/compatibility.md), with an evolving
status ABI. Fault tags and origins do not implement managed exception objects,
catch/filter/finally, allocation, libraries, or the Alchemy application. The
reference helper accepts an output directory only; it is not an arbitrary
assembly execution tool.
