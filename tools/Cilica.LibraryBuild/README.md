# Library build evidence producer

This local build-host tool compiles **trusted managed source** with an explicitly
selected Roslyn compiler. It is separate from Cilica's input-assembly compiler,
which continues to treat assemblies as data. It never loads or executes the
resulting library.

After building the solution, invoke the tool with explicit paths:

```sh
dotnet tools/Cilica.LibraryBuild/bin/Debug/net10.0/Cilica.LibraryBuild.dll \
  --compiler-directory /path/to/dotnet/sdk/10.0.400/Roslyn/bincore \
  --reference /path/to/dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/netstandard.dll \
  --source library/Cilica.Library/IntegerFormatting.cs \
  --output-directory artifacts/library-build/example
```

Repeat `--source` and `--reference` for additional explicit inputs. The optional
`--assembly-name` defaults to `Cilica.Library`. The output directory must not
exist. Fixed C# 9, optimized, deterministic, safe-code library options replace
MSBuild discovery. There are no implicit references, response files, analyzers,
source generators, project targets or generated assembly attributes. If version
attributes are needed, supply them as an explicit source file. The current
formatter-only invocation emits version 0.0.0.0; the normal solution build has
its own project-generated attributes and different output evidence.

The published directory contains copied source/reference/compiler inputs,
`build-options.json`, a bounded compiler log, the assembly and
`library-build.json`. The manifest records the actual command arguments, assembly
identity/hash and hashed inputs. Snapshot paths are relative to this directory;
installed build-host/runtime/producer paths are absolute. The options record
preserves original input paths and the temporary working directory used by the
actual invocation. A new build uses a fresh directory; its manifest digest can
change even when its deterministic assembly bytes match.

The tool uses its own installed .NET host/runtime, disables runtime roll-forward,
clears inherited process environment, hashes host/runtime bytes before and after
compilation and snapshots Roslyn inputs. It assumes a trusted build host and
compiler; it is not a hermetic sandbox or protection against a hostile host.
Input counts, byte sizes, captured output and process duration are bounded. A
failure cleans its own staging directory and publishes no successful evidence.

The default empty grant list and displayed digest are **not approval**. A separate review
must approve the exact manifest digest and any declaration-specific grants
before `CreateTrustedLibraryCompilationScope` may grant intrinsic capabilities.
The tool never updates the trusted-artifact baseline automatically.

`--profile core` explicitly builds with zero references and a fixed CLI metadata
version. It requires source declarations for the compiler's needed core types;
it does not substitute an installed CoreLib. The default `library` profile still
requires explicit references.

`--intrinsic-requests /path/to/requests.json` adds requested declaration evidence
to the manifest. Each JSON array entry must explicitly contain `Namespace`,
`Type`, `Member`, `ParameterCount` and the string-valued `Capability`. Use null for
both member fields to select a type layout. Otherwise a name and parameter count
must select exactly one method emitted by this build. Overloads with the same
count are rejected; no first-match selection occurs. Duplicate requests,
duplicate JSON properties, unknown capabilities and missing members fail the
build without publishing output. The effective requests are included in hashed
`build-options.json`; tokens and signature hashes come from the emitted image.
These requests describe the evidence for later review and do not approve it.
