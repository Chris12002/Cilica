# ADR 0011: Explicit primitive runtime type mapping

Status: accepted for the slice 1c metadata audit. Recorded 2026-09-29.

## Context

Primitive signatures encode built-in types without naming an assembly. A closed
`constrained.` receiver such as `int` therefore cannot use named dispatch without
an explicit library choice. String signatures and `ldstr` also introduce possible
receivers without ordinary named type operands. Guessing a library from the host
installation or a matching short name would make dependency evidence unreliable.

## Decision

The caller selects an owned library image through the optional `runtimeLibrary`
argument to reachability/audit APIs, or `--runtime-library <assembly>` in the CLI.
The selection is per analysis, not mutable resolver-wide state. Opening the image
does not add its directory to the reference catalogue. Forwarded targets and other
dependencies still require explicit `--reference` directories. Selection cannot
override a conflicting catalogue identity or content hash.

`ResolveRuntimeType` maps the CLI codes Boolean, Char, SByte, Byte, Int16, UInt16,
Int32, UInt32, Int64, UInt64, Single, Double, IntPtr, UIntPtr, Object and String to
the corresponding `System` type in that image. These built-in correspondences
come from [ECMA-335, I.8.2.2 and II.23.1.16](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
Normal strict resolution follows type forwarders. A same-named type in another
image is never a fallback. Mapped definitions must be nongeneric, nonabstract and
noninterface; this minimal shape check does not authenticate a framework or
validate class/value-kind consistency, inheritance, size or layout.

The walker caches each requested mapping, including failure, within one analysis.
Closed primitive constraints can use existing named instance-dispatch rules and
retain `ConstrainedType` evidence. Primitive type operands can add `RuntimeType`
dependencies when a library is selected. Void and TypedReference do not acquire
receiver definitions from ordinary operands; explicitly requiring either as a
primitive receiver is unsupported. In particular `void*` does not request a
nonexistent `System.Void` receiver.

All reached string signatures (including fields, locals, return types and generic
arguments) and string literals add the selected String definition as a possible
named receiver. Without a successful mapping, they preserve the synthetic-receiver
gap at relevant dispatch sites. A sealed named constraint still has the exact
receiver exception described in ADR 0010. Reference-only images remain usable for
binding but cannot supply executable body/filter evidence.

The detached audit adds `runtimeLibrary`, an optional assembly-table index, and
`runtimeMappings`, entries containing a primitive code name, actual definition
assembly index and TypeDef token. Image hashes and reference markers remain in
the assembly table, including forwarding images. The analysis profile advances
to `explicit-metadata-closure-v3`; JSON format version 1 retains its additive-field
contract. Text reports expose the same selection and mappings without paths.

## Boundaries and alternatives

This is not global primitive/named signature normalization, framework assembly
retargeting, an execution representation or a complete receiver universe. Numeric
signatures alone do not imply boxed receivers. Existing graph, dispatch and body
budgets apply; successful mapping is not proof that the corresponding target body
was visited. No compiler input is executed.

Arrays retain explicit gaps. Treating every array as an ordinary System.Array
instance would omit runtime-provided generic interface behavior; the runtime uses
special handling and helper bodies (see its
[array interface dependency model](https://source.dot.net/ILCompiler.ReadyToRun/Compiler/DependencyAnalysis/ArrayInterfaceMethodsNode.cs.html)).
An array representation and library contract need a separate decision. Host probing,
short-name fallback and silently raising graph limits were rejected because they
would weaken reproducibility or hide remaining analysis gaps.

## Validation and review

Independent metadata fixtures cover all 16 mappings, forwarding, namesake exclusion,
missing definitions, invalid shapes, conflicting content, reader ownership and
reference-only bodies. Reduced graphs assert primitive constrained targets, string
receiver discovery from each source, array gaps, detached report evidence and CLI
failures. Void-pointer token/sizeof regressions preserve valid nonreceiver operands.

An authored .NET primitive probe checks reference behavior and compiled constraint/
interface-declaration identities. Its bounded real-library graph stops before
implementation dispatch; the test explicitly preserves that limit. Exact target
expansion is established by the independent reduced graphs, not claimed from this
probe. The local Alchemy run likewise records mappings but reaches the node cap
before dispatch. The next block should schedule ordinary and dispatch work fairly
within the existing bounds before adding array receiver support. Neither run clears
ADR 0004's ported-library general-filter gate.
