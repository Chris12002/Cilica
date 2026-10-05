# ADR 0110: Exact closed class virtual targets

Status: Accepted

## Decision

An opt-in metadata entry point resolves a public class virtual declaration for
one concrete closed receiver. It authenticates the supplied declaration against
metadata, validates closed owner constraints and preserves constructed identities
in the declaration, canonical slot, receiver and selected implementation.

Class and interface dispatch share closed-type provenance/constraint validation
and the existing bounded slot walk. Hidden new slots remain independent; reuse
slots select the nearest override and cannot replace final methods. Interface
MethodImpl rows are checked without remapping class slots. The metadata ledger
covers declaration, argument and hierarchy inspection. No input method is executed.

Abstract hierarchies, generic methods, differing return signatures and class
MethodImpl remapping remain diagnostics. This slice supplies metadata choices;
composed CIL discovery and native execution are the next gate.

## Evidence and review

All 53 focused metadata/plan checks pass. Authored closed Int32, Int64 and String
receivers match .NET callvirt and canonical base definitions across overrides,
hidden methods, new slots and final methods. Exact/short budgets, forged/open
inputs, constraints and foreign-reader provenance are covered. Legacy class and
closed interface tests remain green. Review found no remaining actionable issue.
