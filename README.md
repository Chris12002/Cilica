# Cilica

An independent, open-source .NET IL-to-C++ compiler and managed runtime, designed
for embedding C# libraries in native and browser applications.

**Status: design stage.** No compiler, runtime or Expo integration is implemented
yet. Platform and language support described here are planned capabilities,
not compatibility guarantees. Cilica is a working name, pending a naming check.

## Intended pipeline

```text
C# → existing C# compiler → .NET IL → Cilica → C++ + Cilica runtime
                                              ├─ Clang → native library
                                              └─ Emscripten → WebAssembly
```

Cilica will own IL translation, managed semantics and runtime support. Roslyn
and native platform compilers supply the stages on either side. NativeAOT may
serve as a comparison baseline; it will not silently replace Cilica's backend.

## Design

Read the [design specification](docs/design-specification.md) for architecture,
compatibility boundaries, memory management, the embedding API, validation and
milestone exit criteria.

The first milestone is an assembly explorer: inspect metadata, dependencies and
IL without executing input assemblies, and produce stable diagnostics. Its plan
and progress are in [docs/milestones](docs/milestones/01-assembly-explorer.md).

The eventual flagship demonstration is the Alchemy C# turn engine running
offline inside Expo on iOS, Android and web, with equivalent results for the
same command transcript. Alchemy source and assets are not included in this
repository; independent compiler examples must remain usable without them.

## Development

Prerequisite: the .NET SDK pinned in [global.json](global.json) (10.0.400, with
patch roll-forward). Build and test locally:

```bash
dotnet build Cilica.slnx
```

```bash
dotnet test Cilica.slnx
```

Inspect an assembly's metadata without loading or running it:

```bash
dotnet run --project src/Cilica.Cli -- inspect path/to/Assembly.dll --format text
```

Decode and validate method bodies (instructions, basic blocks, exception
regions and stack depths), optionally for one method:

```bash
dotnet run --project src/Cilica.Cli -- il path/to/Assembly.dll --method Type::Name
```

Golden-file tests compare output with reviewed files in
`tests/Cilica.Metadata.Tests/Golden`. After an intended output change, rerun the
tests with `CILICA_UPDATE_GOLDEN=1` and review the diff before committing.
Corruption tests run 500 iterations by default; set `CILICA_FUZZ_ITERATIONS`
for a longer local run.

- See [CONTRIBUTING.md](CONTRIBUTING.md) before starting an implementation slice.
- See [AGENTS.md](AGENTS.md) for repository-specific agent instructions.
- Hosted CI is intentionally unconfigured. Adding a workflow or enabling paid
  hosted work requires explicit maintainer authorization and a runner budget.

## License

[MIT](LICENSE). Third-party code must retain its applicable notices and license.
This is an independent project, not affiliated with Unity or Microsoft.
