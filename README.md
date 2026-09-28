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
IL without executing input assemblies, and produce stable diagnostics.

The eventual flagship demonstration is the Alchemy C# turn engine running
offline inside Expo on iOS, Android and web, with equivalent results for the
same command transcript. Alchemy source and assets are not included in this
repository; independent compiler examples must remain usable without them.

## Development

- See [CONTRIBUTING.md](CONTRIBUTING.md) before starting an implementation slice.
- See [AGENTS.md](AGENTS.md) for repository-specific agent instructions.
- No build or test commands exist yet. Add reproducible local commands when
  the first implementation lands.
- Hosted CI is intentionally unconfigured. Adding a workflow or enabling paid
  hosted work requires explicit maintainer authorization and a runner budget.

## License

[MIT](LICENSE). Third-party code must retain its applicable notices and license.
This is an independent project, not affiliated with Unity or Microsoft.
